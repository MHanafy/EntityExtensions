using System;
using System.Collections.Generic;
using System.Data.Entity;
using EntityExtensions.Common;

namespace EntityExtensions.SqlServer
{
    /// <summary>
    /// Provides EF bulk processing extensions for SQL Server, utilizing SqlBulkCopy
    ///<para/>
    /// Contains convenience methods that call EntityExtensions.Common.BulkExtensions.
    /// </summary>
    public static class BulkExtensions
    {

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
            context.BulkUpdate(new SqlBulkProvider(), entities, refreshMode);
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
            context.BulkUpdate(new SqlBulkProvider(), inserts, updates, deletes, refreshMode);
        }

        /// <summary>
        /// Performs a bulk update/insert/delete process for a given list of entities, Takes in a combined update/insert list and a delete list.
        /// <para/>
        /// Infers inserts based on identity values (equals zero), Use the Inserts/Updates/Deletes overload if you already have separate lists.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="updateList"></param>
        /// <param name="deleteList"></param>
        [Obsolete("Use the Insert/Update/Delete overload instead! It might cause unexpected issues with identity refresh.")]
        public static void BulkUpdate<T>(this DbContext context, ICollection<T> updateList, ICollection<T> deleteList)
            where T : class 
        {
            context.BulkUpdate(new SqlBulkProvider(), updateList, deleteList);
        }
    }
}
