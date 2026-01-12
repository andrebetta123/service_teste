
using System;
using System.Data;
using System.Data.SqlClient;
using ups_Common;
using ups_Entities;

namespace ups_DAO
{
    public class JobRunHistoryDao
    {
        public long Insert(JobRunHistory h)
        {
            using (var conn = Db.CreateConnection())
            using (var cmd = new SqlCommand(
                "INSERT INTO dbo.JobRunHistory " +
                "(JobId, StartedUtc, FinishedUtc, Status, Message, HostName) " +
                "VALUES (@JobId, @StartedUtc, @FinishedUtc, @Status, @Message, @HostName); " +
                "SELECT CAST(SCOPE_IDENTITY() AS BIGINT);",
                conn))
            {
                cmd.Parameters.Add("@JobId", SqlDbType.Int).Value = h.JobId;
                cmd.Parameters.Add("@StartedUtc", SqlDbType.DateTime2).Value =
                    (object)h.StartedUtc ?? DateTime.UtcNow;
                cmd.Parameters.Add("@FinishedUtc", SqlDbType.DateTime2).Value =
                    (object)h.FinishedUtc ?? DBNull.Value;
                cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 50).Value =
                    (object)h.Status ?? DBNull.Value;
                cmd.Parameters.Add("@Message", SqlDbType.NVarChar).Value =
                    (object)h.Message ?? DBNull.Value;
                cmd.Parameters.Add("@HostName", SqlDbType.NVarChar, 200).Value =
                    (object)h.HostName ?? DBNull.Value;

                conn.Open();
                return (long)cmd.ExecuteScalar();
            }
        }

        public void Mark(long runId, string status, DateTime? finishedUtc, string message)
        {
            using (var conn = Db.CreateConnection())
            using (var cmd = new SqlCommand(
                "UPDATE dbo.JobRunHistory " +
                "SET Status = @s, FinishedUtc = @f, Message = @m " +
                "WHERE RunId = @id",
                conn))
            {
                cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = runId;
                cmd.Parameters.Add("@s", SqlDbType.NVarChar, 50).Value =
                    (object)status ?? DBNull.Value;
                cmd.Parameters.Add("@f", SqlDbType.DateTime2).Value =
                    (object)finishedUtc ?? DBNull.Value;
                cmd.Parameters.Add("@m", SqlDbType.NVarChar).Value =
                    (object)message ?? DBNull.Value;

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
