using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Reflection;
using System.Text;

namespace EntityExtensions.Internal
{
    /// <summary>
    /// Generates Sql statements for consumption by other EF extension classes, Marked internal to avoid listing non commonly used functions as DBContext extensions.
    /// </summary>
    internal static class SqlHelper
    {

        /// <summary>
        /// Returns a merge SQL statement that either inserts or updates an entity based on the primary key.
        /// The target table is named dest and the source is an inline table based on parameters named @p[i], where i is a zero based index.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static string GetInsertOrUpdateSql<T>(this DbContext context)
        {
            var columns = context.GetTableColumns<T>();
            var keys = context.GetTableKeyColumns<T>();
            var tableName = context.GetTableName<T>();

            var sb = new StringBuilder($"MERGE INTO {tableName} dest ");
            sb.Append("USING (select ");
            var i = 0;
            foreach (var column in columns.Keys)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"@p{i} [{column}]");
                i++;
            }
            sb.AppendLine(") src");
            sb.Append("ON ");
            var addSeparator = false;
            foreach (var key in keys.Keys)
            {
                if (addSeparator)
                {
                    sb.Append(" and ");
                }
                else
                {
                    addSeparator = true;
                }
                sb.Append($"src.[{key}] = dest.[{key}]");
            }

            sb.Append(" WHEN MATCHED THEN UPDATE SET ");
            addSeparator = false;
            foreach (var column in columns.Keys)
            {
                //No need to update the keys
                if (keys.ContainsKey(column)) continue;
                if (addSeparator)
                {
                    sb.Append(", ");
                }
                else
                {
                    addSeparator = true;
                }
                sb.Append($"[{column}] = src.[{column}]");
            }

            sb.Append(" WHEN NOT MATCHED THEN INSERT VALUES(");
            addSeparator = false;
            foreach (var column in columns.Keys)
            {
                if (addSeparator)
                {
                    sb.Append(", ");
                }
                else
                {
                    addSeparator = true;
                }
                sb.Append($"[{column}]");
            }
            sb.AppendLine(");");

            return sb.ToString();
        }

        public static string GetDeleteSql(this DbContext context, string srcTable, string destTable, List<string> keys)
        {
            var sb = new StringBuilder();
            sb.Append("Delete from ");
            sb.AppendLine(destTable);
            sb.Append("Where Exists(Select 1 from ");
            sb.Append(srcTable);
            sb.Append(" where ");
            sb.Append(string.Join(" And ", keys.Select(x => $"[{x}] = {destTable}.[{x}]")));
            sb.Append(")");
            return sb.ToString();
        }

        public static string GetMergeSql(this DbContext context, string srcTable, string destTable, List<string> colNames, List<string> keys,
            Dictionary<string, bool> computedCols, string keysTable = null, ICollection<string> returnCols = null)
        {
            var sb = new StringBuilder();
            var nonComputedCols = colNames.Where(x => !computedCols.ContainsKey(x)).ToList();
            sb.AppendLine($"Merge into {destTable} dest using(select * from {srcTable}) src");
            sb.Append("on (");
            sb.Append(string.Join(" and ", keys.Select(x => $"src.[{x}] = dest.[{x}]")));
            sb.AppendLine(")");
            sb.AppendLine("when matched then update set");
            sb.AppendLine(string.Join(", ", nonComputedCols.Select(x => $"{x} = src.[{x}]")));
            sb.AppendLine("when not matched then ");
            sb.Append("insert(");
            sb.Append(string.Join(",", nonComputedCols.Select(x => $"[{x}]")));
            sb.AppendLine(")");
            sb.Append("values(");
            sb.Append(string.Join(",", nonComputedCols.Select(x => $"src.[{x}]")));
            sb.Append(")");
            //If return cols isn't provided, we return only identity columns.
            var identityCols = returnCols ?? computedCols.Where(x => x.Value).Select(x => x.Key).ToList();
            if (identityCols.Any() && keysTable != null)
            {
                sb.AppendLine();
                sb.Append("output ");
                sb.Append(string.Join(", ", identityCols.Select(x => $"src.[{x}]")));
                sb.Append(",");
                sb.Append(string.Join(", ", identityCols.Select(x => $"inserted.[{x}]")));
                sb.Append(" into ");
                sb.Append(keysTable);
            }
            sb.Append(";");
            return sb.ToString();
        }
        
        public static string GetTableDdl(this DbContext context, string tableName, IDictionary<string, PropertyInfo> tabCols)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Create Table {tableName}(");

            sb.AppendLine(string.Join(",\r\n",
                tabCols.Select(x => $"[{x.Key}] {Helper.GetSqlServerType(x.Value.PropertyType)}")));

            sb.Append(")");
            return sb.ToString();
        }
    }
}
