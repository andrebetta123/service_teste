
using System;
using System.Data;
using System.Data.SqlClient;

namespace ups_Work_Job_Service
{
    internal static class Locking
    {
        public static bool TryAcquireJobLock(SqlConnection conn, SqlTransaction tx, string concurrencyKey, int jobId)
        {
            string key = concurrencyKey ?? $"JobId_{jobId}";
            using (var cmd = new SqlCommand("sp_getapplock", conn, tx))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                // parâmetro de retorno (código da sp)
                SqlParameter ret = cmd.Parameters.Add("@RETURN_VALUE", SqlDbType.Int);
                ret.Direction = ParameterDirection.ReturnValue;

                cmd.Parameters.AddWithValue("@Resource", key);
                cmd.Parameters.AddWithValue("@LockMode", "Exclusive");
                cmd.Parameters.AddWithValue("@LockOwner", "Session");
                cmd.Parameters.AddWithValue("@LockTimeout", 0);

                cmd.ExecuteNonQuery();
                int code = (int)ret.Value; // >=0 obteve; <0 falhou
                return code >= 0;
            }
        }
    }
}
