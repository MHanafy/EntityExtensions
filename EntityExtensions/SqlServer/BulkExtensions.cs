using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.Linq;
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
            Type entityType = typeof(T);
            //Randome number from 1 to 999
            return "#" + entityType.Name + DateTime.Now.Millisecond % 1000;
        }

        /// <summary>
        /// Performs a bulk update/insert/delete process for a given list of entities, utilizes EF change tracking to determine the state of entities.
        /// Uses the SqlBulkCopy and temp tables to perform the action.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="entities"></param>
        public static void BulkUpdate<T>(this DbContext context, ICollection<T> entities)
            where T : class
        {
            if (!context.Configuration.AutoDetectChangesEnabled)
            {
                throw new NotSupportedException("You must enable EF change tracking to call this function");
            }
            //We restrict updates to the provided list
            List<T> updateList = new List<T>();
            List<T> deleteList = new List<T>();
            foreach (var entity in entities)
            {
                switch (context.Entry(entity).State)
                {
                    case EntityState.Detached:
                        throw new EntityException("Entities must be added to context before saving.");
                    case EntityState.Added:
                    case EntityState.Modified:
                        updateList.Add(entity);
                        break;
                    case EntityState.Deleted:
                        deleteList.Add(entity);
                        break;
                }
            }

            BulkUpdate(context, updateList, deleteList);

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
        /// <param name="updateList"></param>
        /// <param name="deleteList"></param>
        public static void BulkUpdate<T>(this DbContext context, ICollection<T> updateList, ICollection<T> deleteList)
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
                throw new NotSupportedException("Only SQL Server connections are currently supported!");
            }

            //Tracking the original connection state to return it in the same state.
            var connectionState = context.Database.Connection.State;
            if (connectionState != ConnectionState.Open) context.Database.Connection.Open();

            SqlBulkCopy bulk = new SqlBulkCopy((SqlConnection)context.Database.Connection);

            if (updateList != null && updateList.Count > 0)
            {
                DataTable table = context.GetDatatable(updateList, columns);
                string sql = context.GetTableDdl(tmpTableName, columns);
                //Create a temp table to insert modified/inserted rows.
                context.Database.ExecuteSqlCommand(sql);

                //Use BulkCopy to bulk insert records to temp table.
                bulk.DestinationTableName = tmpTableName;
                bulk.WriteToServer(table);

                //Get computed columns because we can't insert/update them
                var computedColumns = context.GetComputedColumnNames<T>();
                sql = context.GetMergeSql(tmpTableName, tableName, columns.Keys.ToList(), keys.Keys.ToList(),
                    computedColumns);

                context.Database.ExecuteSqlCommand(sql);

                //drop the temp table
                sql = "drop table " + tmpTableName;
                context.Database.ExecuteSqlCommand(sql);
            }

            if (deleteList != null && deleteList.Count > 0)
            {
                //Create a temp table to store deleted entities keys
                string tmpDeleteTableName = tmpTableName + "DelKeys";
                string sql = context.GetTableDdl(tmpDeleteTableName, keys);
                context.Database.ExecuteSqlCommand(sql);

                DataTable table = context.GetDatatable(deleteList, keys);
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
    }
}
