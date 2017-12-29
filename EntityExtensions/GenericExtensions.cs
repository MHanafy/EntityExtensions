using System;
using System.Data.Entity;
using System.Linq;
using EntityExtensions.Internal;

namespace EntityExtensions
{
    /// <summary>
    /// This class provide EF extension methods that aren't DB vendor specific, i.e. applies to all databases.
    /// </summary>
    public static class GenericExtensions
    {
        /// <summary>
        /// Directly deletes all objects that has a property set to a given value, Property doesn't have to be a key.
        /// Please use caution! this can be used to bulk delete objects from database.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        public static void DirectDeleteByProperty<T>(this DbContext context, string propertyName, object value)
        {
            var type = typeof(T);
            var property = type.GetProperty(propertyName);
            if (property == null) throw new ArgumentOutOfRangeException($"Can't find property: {propertyName}!");

            var columnName = context.GetColumnName<T>(propertyName);
            if (columnName == null)
                throw new ArgumentOutOfRangeException($"Can't find database column name for property: {propertyName}!");

            var tableName = context.GetTableName<T>();
            var sql = $"Delete from {tableName} where [{columnName}] = @p0";

            context.Database.ExecuteSqlCommand(sql, value);
        }

        /// <summary>
        /// Either creates the object, or updates its non key properties if exists.
        /// Utilizies a SQL merge statement to do the process in one DB call.
        /// Doen't refresh the object with DB changed values, i.e. identity columns.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="entity"></param>
        public static void InsertOrUpdate<T>(this DbContext context, T entity)
        {
            var columnNames = context.GetPropertyColumnNames<T>();
            var type = typeof(T);
            var properties = columnNames.Keys.Select(x => type.GetProperty(x)).ToList();
            var paramList = properties.Select(x => x.GetValue(entity, null)).ToArray();
            var sqlInsertOrUpdate = context.GetInsertOrUpdateSql<T>();
            context.Database.ExecuteSqlCommand(sqlInsertOrUpdate, paramList);
        }
    }
}
