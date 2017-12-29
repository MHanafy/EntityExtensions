using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using EntityExtensions.Common;
using EntityExtensions.Internal;

namespace EntityExtensions.SqlServer
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
        /// <param name="entities">The list of entities to update</param>
        /// <param name="refreshMode">Controls which values are read back from DB, Can't be None</param>
        public static void BulkUpdate<T>(this DbContext context, ICollection<T> entities, RefreshMode refreshMode = RefreshMode.All)
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

            BulkUpdate(context, insertList, updateList, deleteList, refreshMode);

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
        /// <param name="inserts">The list of entities to insert</param>
        /// <param name="updates">The list of entities to update</param>
        /// <param name="deletes">The list of entities to delete</param>
        /// <param name="refreshMode">Controls which values are read back from DB</param>
        public static void BulkUpdate<T>(this DbContext context, ICollection<T> inserts, ICollection<T> updates,
            ICollection<T> deletes, RefreshMode refreshMode = RefreshMode.None)
            where T : class
        {
            //Todo: write code to refresh identity columns
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

            if (!(context.Database.Connection is SqlConnection))
            {
                throw new NotSupportedException("Only SQL Server connections are supported!");
            }

            var hasInserts = inserts != null && inserts.Count > 0;
            var hasUpdates = updates != null && updates.Count > 0;

            //Tracking the original connection state to return it in the same state.
            var connectionState = context.Database.Connection.State;
            if (connectionState != ConnectionState.Open) context.Database.Connection.Open();

            var computedCols = context.GetComputedColumnNames<T>();

            var outSettings = GetOutputColumns<T>(context, hasInserts, hasUpdates, refreshMode, columns, keys.Keys,
                computedCols);


            var bulk = new SqlBulkCopy((SqlConnection) context.Database.Connection);

            if (hasInserts || hasUpdates)
            {
                var table = context.GetDatatable(updates, columns);
                var sql = context.GetTableDdl(tmpTableName, columns);
                //Create a temp table to insert modified/inserted rows.
                context.Database.ExecuteSqlCommand(sql);

                if (outSettings != null)
                {
                    context.Database.ExecuteSqlCommand(outSettings.TableSql);
                }

                //Use BulkCopy to bulk insert records to temp table.
                bulk.DestinationTableName = tmpTableName;
                bulk.WriteToServer(table);

                //Get computed columns because we can't insert/update them
                sql = context.GetMergeSql(tmpTableName, tableName, columns.Keys.ToList(), keys.Keys.ToList(),
                    computedCols, outSettings?.TableName, outSettings?.Columns.Keys);

                context.Database.ExecuteSqlCommand(sql);

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
                bulk.DestinationTableName = tmpDeleteTableName;
                bulk.WriteToServer(table);

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

        internal class OutputColumns
        {
            public readonly Dictionary<string, PropertyInfo> Keys;
            public readonly Dictionary<string, PropertyInfo> Columns;
            public readonly string TableName;
            public readonly string TableSql;

            public OutputColumns(Dictionary<string, PropertyInfo> keys, Dictionary<string, PropertyInfo> columns,
                string tableName, string tableSql)
            {
                Keys = keys;
                Columns = columns;
                TableSql = tableSql;
                TableName = tableName;
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
                        if (computedColumns.Count > 0 && keyList !=null && keyList.Count == 0)
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

            var allCols = new Dictionary<string, PropertyInfo>(outKeys);
            if (outCols != null)
            {
                foreach (var key in outCols.Keys)
                {
                    allCols.Add(key, outCols[key]);
                }
            }

            var tableName = GetTempTableName<T>() + "OutValues";
            var tableSql = context.GetTableDdl(tableName, allCols);

            return new OutputColumns(outKeys, outCols, tableName, tableSql);
        }

        /// <summary>
        /// Performs a bulk update/insert/delete process for a given list of entities, Takes in an update/insert list and a delete list.
        /// Uses the SqlBulkCopy and temp tables to perform the action.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="updateList"></param>
        /// <param name="deleteList"></param>
        [Obsolete("Use the Insert/Update/Delete overload instead!")]
        public static void BulkUpdate<T>(this DbContext context, ICollection<T> updateList, ICollection<T> deleteList)
            where T : class 
        {
            //split updateList to insert/update based on identity key value to maintain backward compability
            var keyColName = context.GetComputedColumnNames<T>().First(x => x.Value).Key;
            if (keyColName==null)
            {
                //There're no identit columns, hence can't infer entities state.
                BulkUpdate(context, null, updateList, deleteList);
                return;
            }
            var keyCol = context.GetTableColumns<T>()[keyColName];
            var insertList = new List<T>();
            var newUpdateList = new List<T>();
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
            BulkUpdate(context, insertList, newUpdateList, deleteList);
        }
    }
}
