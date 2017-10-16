using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Reflection;

namespace EntityExtensions.Internal
{
    internal static class Helper
    {
        internal static string GetSqlServerType(Type type)
        {
            if (type.IsGenericType)
            {
                //This is nullable
                type = type.GetGenericArguments()[0];
            }
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Int16:
                    return "smallint";
                case TypeCode.Int32:
                    return "int";
                case TypeCode.Decimal:
                    return "decimal";
                case TypeCode.DateTime:
                    return "datetime";
                case TypeCode.String:
                    return "nvarchar(MAX)";
                case TypeCode.Boolean:
                    return "bit";
                default:
                    if (type == typeof(Guid))
                    {
                        return "uniqueidentifier";
                    }
                    else
                    {
                        throw new Exception($"Unsupported database column type: {type.Name}");
                    }
            }
        }
        /// <summary>
        /// Converts a list of entities to a DataTable object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="entities"></param>
        /// <param name="tabCols"></param>
        /// <returns></returns>
        internal static DataTable GetDatatable<T>(this DbContext context, ICollection<T> entities, IDictionary<string, PropertyInfo> tabCols = null)
        {
            if (tabCols == null) tabCols = context.GetTableColumns<T>();
            var table = new DataTable();
            foreach (string colName in tabCols.Keys)
            {
                var colType = tabCols[colName].PropertyType.IsGenericType
                    ? tabCols[colName].PropertyType.GetGenericArguments()[0]
                    : tabCols[colName].PropertyType;
                table.Columns.Add(colName, colType);
            }
            table.BeginLoadData();
            foreach (var entity in entities)
            {
                var row = table.NewRow();
                foreach (string column in tabCols.Keys)
                {
                    row[column] = tabCols[column].GetValue(entity, null);
                }
                table.Rows.Add(row);
            }
            table.EndLoadData();
            return table;
        }

    }
}
