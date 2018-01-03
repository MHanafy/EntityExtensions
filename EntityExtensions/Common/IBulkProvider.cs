using System.Data;
using System.Data.Common;

namespace EntityExtensions.Common
{
    public interface IBulkProvider
    {
        /// <summary>
        /// Writes the given datatable to the DB server using the given connection.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="destTableName"></param>
        /// <param name="data"></param>
        void WriteToServer(DbConnection connection, string destTableName, DataTable data);
    }
}
