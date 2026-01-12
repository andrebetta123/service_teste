
using System.Configuration;
using System.Data.SqlClient;

namespace ups_Common
{
    public static class Db
    {
        public static string ConnectionString =>
            ConfigurationManager.ConnectionStrings["SqlServer"].ConnectionString;

        public static SqlConnection CreateConnection() =>
            new SqlConnection(ConnectionString);
    }
}
