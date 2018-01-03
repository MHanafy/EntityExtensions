using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core;
using System.Linq;
using System.Reflection;
using EntityExtensions.Helpers;
using EntityExtensions.Internal;

namespace EntityExtensions.Common
{
    /// <summary>
    /// Provides EF bulk processing extensions for SQL Server, utilizing SqlBulkCopy
    /// </summary>
    public static class BulkExtensions
    {
        /// <summary>
        /// Generates a random Temp table name based on the entity type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private static string GetTempTableName<T>()
        {
            var entityType = typeof(T);
            //Random number from 1 to 999
            return "#" + entityType.Name + DateTime.Now.Millisecond % 1000;
        }

        /// <summary>
        /// Performs a bulk update/insert/delete process for a given list of entities, utilizes EF change tracking to determine the state of entities.
        /// Uses the SqlBulkCopy and temp tables to perform the action.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="bulkProvider">The bulk provider to use for direct DB insertion</param>
        /// <param name="entities">The list of entities to update</param>
        /// <param name="refreshMode">Controls which values are read back from DB, Can't be None</param>
        public static void BulkUpdate<T>(this DbContext context, IBulkProvider bulkProvider, ICollection<T> entities, RefreshMode refreshMode = RefreshMode.All)
            where T : class
        {
            if (!context.Configuration.AutoDetectChangesEnabled)
            {
                throw new NotSupportedException("You must enable EF change tracking to call this function");
            }

            //Identity refresh is mandatory when using EF tracking, otherwise we can't tell EF to AcceptChanges after insert.
            if (refreshMode == RefreshMode.None) refreshMode = RefreshMode.Identity;

            //We restrict updates to the provided list
            var insertList = new List<T>();
            var updateList = new List<T>();
            var deleteList = new List<T>();
            foreach (var entity in entities)
            {
                switch (context.Entry(entity).State)
                {
                    case EntityState.Detached:
                        throw new EntityException("Entities must be added to context before saving.");
                    case EntityState.Added:
                        insertList.Add(entity);
                        break;
                    case EntityState.Modified:
                        updateList.Add(entity);
                        break;
                    case EntityState.Deleted:
                        deleteList.Add(entity);
                        break;
                }
            }

            BulkUpdate(context, bulkProvider, insertList, updateList, deleteList, refreshMode);

            //Update entries state
            foreach (var entity in updateList)
            {
                context.Entry(entity).State = EntityState.Unchanged;
            }
            foreach (var entity in deleteList)
            {
                context.Entry(entity).State = EntityState.Detached;
            }
        }

        /// <summary>
        /// Performs a bulk update/insert/delete process for a given list of entities, Takes in an update/insert list and a delete list.
        /// Uses the SqlBulkCopy and temp tables to perform the action.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="bulkProvider"></param>
        /// <param name="inserts">The list of entities to insert</param>
        /// <param name="updates">The list of entities to update</param>
        /// <param name="deletes">The list of entities to delete</param>
        /// <param name="refreshMode">Controls which values are read back from DB</param>
        public static void BulkUpdate<T>(this DbContext context, IBulkProvider bulkProvider, ICollection<T> inserts, ICollection<T> updates,
            ICollection<T> deletes, RefreshMode refreshMode = RefreshMode.None)
            where T : class
        {
            /*
             * 1. Create temp table for sql bulk update (inserts/updates)
             * 2. Bulk insert
             * 3. Execute merge statements
             * 4. Create temp table for deletes
             * 5. Bulk insert keys for deletion
             * 6. Execute delete statement
             */

            var columns = context.GetTableColumns<T>();
            var tmpTableName = GetTempTableName<T>();
            var tableName = context.GetTableName<T>();
            var keys = context.GetTableKeyColumns<T>();

            var hasInserts = inserts != null && inserts.Count > 0;
            var hasUpdates = updates != null && updates.Count > 0;

            //Tracking the original connection state to return it in the same state.
            var connectionState = context.Database.Connection.State;
            if (connectionState != ConnectionState.Open) context.Database.Connection.Open();

            var computedCols = context.GetComputedColumnNames<T>();

            var outSettings = GetOutputColumns<T>(context, hasInserts, hasUpdates, refreshMode, columns, keys.Keys,
                computedCols);

            if (hasInserts || hasUpdates)
            {
                var allUpdates = new List<T>();
                if (hasInserts) allUpdates.AddRange(inserts);
                if (hasUpdates) allUpdates.AddRange(updates);

                var sql = context.GetTableDdl(tmpTableName, columns);
                //Create a temp table to insert modified/inserted rows.
                context.Database.ExecuteSqlCommand(sql);

                if (outSettings != null)
                {
                    context.Database.ExecuteSqlCommand(outSettings.TableSql);
                    var identityProps = computedCols.Where(x => x.Value).Select(x => columns[x.Key]).ToList();
                    SetTempIdentity(inserts, identityProps);
                }

                var table = context.GetDatatable(allUpdates, columns);

                //Use BulkCopy to bulk insert records to temp table.
                bulkProvider.WriteToServer(context.Database.Connection, tmpTableName, table);

                //Get computed columns because we can't insert/update them
                sql = context.GetMergeSql(tmpTableName, tableName, columns.Keys.ToList(), keys.Keys.ToList(),
                    computedCols, outSettings?.TableName, outSettings?.AllColumns.Keys);

                context.Database.ExecuteSqlCommand(sql);

                if (outSettings != null)
                {
                    RefreshEntities(context, allUpdates, outSettings);
                    sql = "drop table " + outSettings.TableName;
                    context.Database.ExecuteSqlCommand(sql);
                }

                //drop the temp table
                sql = "drop table " + tmpTableName;
                context.Database.ExecuteSqlCommand(sql);
            }

            if (deletes != null && deletes.Count > 0)
            {
                //Create a temp table to store deleted entities keys
                var tmpDeleteTableName = tmpTableName + "DelKeys";
                var sql = context.GetTableDdl(tmpDeleteTableName, keys);
                context.Database.ExecuteSqlCommand(sql);

                var table = context.GetDatatable(deletes, keys);
                bulkProvider.WriteToServer(context.Database.Connection, tmpDeleteTableName, table);

                sql = context.GetDeleteSql(tmpDeleteTableName, tableName, keys.Keys.ToList());

                context.Database.ExecuteSqlCommand(sql);

                //drop the temp table
                sql = "drop table " + tmpDeleteTableName;
                context.Database.ExecuteSqlCommand(sql);
            }

            if (connectionState == ConnectionState.Closed)
            {
                context.Database.Connection.Close();
            }
        }

        private const int IdentitySeed = -100;
        private const int IdentityIncrement = -1;

        /// <summary>
        /// Sets unique temporary identity values to enable reading back the actual identities from DB.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entities"></param>
        /// <param name="identityProps"></param>
        internal static void SetTempIdentity<T>(ICollection<T> entities, ICollection<PropertyInfo> identityProps)
        {
            var id = IdentitySeed;
            foreach (var entity in entities)
            {
                foreach (var prop in identityProps)
                {
                    if (Convert.ToInt64(prop.GetValue(entity, null)) != 0)
                    {
                        continue;
                    }
                    prop.SetValue(entity, id, null);
                    id += IdentityIncrement;
                }
            }
        }

        /// <summary>
        /// Reads the database generated identity/computed columns.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="entities"></param>
        /// <param name="outSettings"></param>
        internal static void RefreshEntities<T>(DbContext context, ICollection<T> entities, OutputColumns outSettings)
        {
            //storing values into a dictionary based on keys hash to facilitate fast lookup.
            var values = new Dictionary<int, object[]>();
            var command = context.Database.Connection.CreateCommand();
            command.CommandText = outSettings.SelectSql;
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    // ReSharper disable once AccessToDisposedClosure
                    var keys = outSettings.Keys.Select(x => reader[SqlHelper.OldColumnPrefix + x.Key]).ToArray();
                    var vals = new object[outSettings.AllColumns.Count];
                    var i = 0;
                    foreach (var col in outSettings.AllColumns.Keys)
                    {
                        vals[i] = reader[col];
                        i++;
                    }
                    values.Add(HashHelper.CombineHashCodes(keys), vals);
                }
                reader.Close();
            }

            foreach (var entity in entities)
            {
                var key = HashHelper.CombineHashCodes(outSettings.Keys.Select(x => x.Value.GetValue(entity, null)).ToArray());
                var i = 0;
                foreach (var prop in outSettings.AllColumns.Values)
                {
                    //Values are stored in the object array in the same order as AllColumns, hence it's safe to use the index.
                    prop.SetValue(entity, values[key][i], null);
                    i++;
                }
            }
        }

        internal class OutputColumns
        {
            public readonly Dictionary<string, PropertyInfo> Keys;
            public readonly Dictionary<string, PropertyInfo> Columns;
            public readonly Dictionary<string, PropertyInfo> AllColumns;
            public readonly string TableName;
            public readonly string TableSql;
            public string SelectSql => $"Select * From {TableName}";

            public OutputColumns(Dictionary<string, PropertyInfo> keys, Dictionary<string, PropertyInfo> columns,
                string tableName, string tableSql)
            {
                Keys = keys;
                Columns = columns;
                TableName = tableName;
                TableSql = tableSql;
                AllColumns = new Dictionary<string, PropertyInfo>(keys);
                if (columns == null) return;
                foreach (var key in columns.Keys)
                {
                    AllColumns.Add(key, columns[key]);
                }
            }
        }

        internal static OutputColumns GetOutputColumns<T>(DbContext context, bool hasInserts, bool hasUpdates,
            RefreshMode refreshMode, Dictionary<string, PropertyInfo> columns, ICollection<string> keys,
            Dictionary<string, bool> computedCols)
        {
            List<string> keyList = null;
            List<string> colList = null;
            switch (refreshMode)
            {
                case RefreshMode.None:
                    return null;
                case RefreshMode.Identity:
                    //No need to return identity if there're no inserts
                    if (hasInserts)
                    {
                        keyList = computedCols.Where(x => x.Value).Select(x => x.Key).ToList();
                    }
                    break;
                case RefreshMode.All:
                    if (hasInserts)
                    {
                        keyList = computedCols.Where(x => x.Value).Select(x => x.Key).ToList();
                    }
                    //Return computed columns if there're an inserts/updates
                    if (hasInserts || hasUpdates)
                    {
                        var computedColumns = computedCols.Where(x => !x.Value).Select(x => x.Key).ToList();
                        if (computedColumns.Count > 0 && keyList != null && keyList.Count == 0)
                        {
                            //If there're only computed columns and no identities, we need to include the primary key to identify the values.
                            keyList = keys.ToList();
                        }
                        colList = computedColumns;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(refreshMode), refreshMode, null);
            }

            if (keyList == null || keyList.Count == 0) return null;

            var outKeys = keyList.ToDictionary(x => x, y => columns[y]);
            var outCols = colList?.ToDictionary(x => x, y => columns[y]);

            var tableName = GetTempTableName<T>() + "OutValues";
            var tableSql = context.GetOutTableDdl(tableName, outKeys, outCols);
            var result = new OutputColumns(outKeys, outCols, tableName, tableSql);

            return result;
        }

        /// <summary>
        /// Performs a bulk update/insert/delete process for a given list of entities, Takes in a combined update/insert list and a delete list.
        /// <para/>
        /// Infers inserts based on identity values (equals zero), Use the Inserts/Updates/Deletes overload if you already have separate lists.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="bulkProvider">The bulk provider to use for direct DB insertion</param>
        /// <param name="updateList"></param>
        /// <param name="deleteList"></param>
        public static void BulkUpdate<T>(this DbContext context, IBulkProvider bulkProvider, ICollection<T> updateList, ICollection<T> deleteList)
            where T : class
        {
            //split updateList to insert/update based on identity key value to maintain backward compability
            var keyColName = context.GetComputedColumnNames<T>().First(x => x.Value).Key;
            if (keyColName == null)
            {
                //There're no identity columns, hence can't infer entities state.
                BulkUpdate(context, bulkProvider, null, updateList, deleteList);
                return;
            }
            var keyCol = context.GetTableColumns<T>()[keyColName];
            var insertList = new List<T>();
            var newUpdateList = new List<T>();
            if (updateList != null)
            {
                foreach (var item in updateList)
                {
                    //Below should be safe, since identity columns are always integers, using 64 to account for large ids.
                    if (Convert.ToInt64(keyCol.GetValue(item, null)) == 0)
                    {
                        //If identity value is 0 it's assumed to be a new record
                        insertList.Add(item);
                    }
                    else
                    {
                        newUpdateList.Add(item);
                    }
                }
            }
            else
            {
                newUpdateList = null;
            }
            BulkUpdate(context, bulkProvider, insertList, newUpdateList, deleteList);
        }
    }
}
