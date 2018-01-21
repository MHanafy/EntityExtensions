using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using EntityExtensions.Common;

namespace EntityExtensions.SqlServer
{
    public class SqlBulkProvider : IBulkProvider
    {
        public void WriteToServer(DbConnection connection, string destTableName, DataTable data)
        {
            if (!(connection is SqlConnection))
            {
                throw new NotSupportedException("Only SQL Server connections are supported!");
            }
            var bulk = new SqlBulkCopy((SqlConnection) connection) {DestinationTableName = destTableName};
            bulk.WriteToServer(data);
        }
    }
}
