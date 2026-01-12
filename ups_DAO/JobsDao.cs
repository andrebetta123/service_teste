
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using ups_Common;
using ups_Entities;

namespace ups_DAO
{
    public class JobsDao
    {
        private Job Map(SqlDataReader r) =>
            new Job
            {
                JobId = r.GetInt32(r.GetOrdinal("JobId")),
                Name = r.GetString(r.GetOrdinal("Name")),
                Enabled = r.GetBoolean(r.GetOrdinal("Enabled")),
                MaxRetries = r.GetInt32(r.GetOrdinal("MaxRetries")),
                RetryDelaySec = r.GetInt32(r.GetOrdinal("RetryDelaySec")),
                ConcurrencyKey = r.IsDBNull(r.GetOrdinal("ConcurrencyKey")) ? null : r.GetString(r.GetOrdinal("ConcurrencyKey")),
                LastRunStatus = r.IsDBNull(r.GetOrdinal("LastRunStatus")) ? null : r.GetString(r.GetOrdinal("LastRunStatus")),
                LastRunUtc = r.IsDBNull(r.GetOrdinal("LastRunUtc")) ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("LastRunUtc"))
            };

        public IEnumerable<Job> GetAll()
        {
            var list = new List<Job>();

            using (var conn = Db.CreateConnection())
            using (var cmd = new SqlCommand(
                "SELECT JobId, Name, Enabled, MaxRetries, RetryDelaySec, ConcurrencyKey, LastRunStatus, LastRunUtc FROM dbo.Jobs", conn))
            {
                conn.Open();
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        list.Add(Map(r));
            }

            return list;
        }

        public Job GetById(int id)
        {
            using (var conn = Db.CreateConnection())
            using (var cmd = new SqlCommand(
                "SELECT JobId, Name, Enabled, MaxRetries, RetryDelaySec, ConcurrencyKey, LastRunStatus, LastRunUtc FROM dbo.Jobs WHERE JobId=@id", conn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
                conn.Open();

                using (var r = cmd.ExecuteReader())
                    return r.Read() ? Map(r) : null;
            }
        }

        public int Insert(Job job)
        {
            using (var conn = Db.CreateConnection())
            using (var cmd = new SqlCommand(
                "INSERT INTO dbo.Jobs (Name, Enabled, MaxRetries, RetryDelaySec, ConcurrencyKey, LastRunStatus, LastRunUtc) " +
                "VALUES (@Name, @Enabled, @MaxRetries, @RetryDelaySec, @ConcurrencyKey, @LastRunStatus, @LastRunUtc); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);", conn))
            {
                cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = job.Name;
                cmd.Parameters.Add("@Enabled", SqlDbType.Bit).Value = job.Enabled;
                cmd.Parameters.Add("@MaxRetries", SqlDbType.Int).Value = job.MaxRetries;
                cmd.Parameters.Add("@RetryDelaySec", SqlDbType.Int).Value = job.RetryDelaySec;
                cmd.Parameters.Add("@ConcurrencyKey", SqlDbType.NVarChar, 100).Value = (object)job.ConcurrencyKey ?? DBNull.Value;
                cmd.Parameters.Add("@LastRunStatus", SqlDbType.NVarChar, 50).Value = (object)job.LastRunStatus ?? DBNull.Value;
                cmd.Parameters.Add("@LastRunUtc", SqlDbType.DateTime2).Value = (object)job.LastRunUtc ?? DBNull.Value;

                conn.Open();
                return (int)cmd.ExecuteScalar();
            }
        }

        public void Update(Job job)
        {
            using (var conn = Db.CreateConnection())
            using (var cmd = new SqlCommand(
                "UPDATE dbo.Jobs SET Name=@Name, Enabled=@Enabled, MaxRetries=@MaxRetries, RetryDelaySec=@RetryDelaySec, " +
                "ConcurrencyKey=@ConcurrencyKey, LastRunStatus=@LastRunStatus, LastRunUtc=@LastRunUtc WHERE JobId=@JobId", conn))
            {
                cmd.Parameters.Add("@JobId", SqlDbType.Int).Value = job.JobId;
                cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = job.Name;
                cmd.Parameters.Add("@Enabled", SqlDbType.Bit).Value = job.Enabled;
                cmd.Parameters.Add("@MaxRetries", SqlDbType.Int).Value = job.MaxRetries;
                cmd.Parameters.Add("@RetryDelaySec", SqlDbType.Int).Value = job.RetryDelaySec;
                cmd.Parameters.Add("@ConcurrencyKey", SqlDbType.NVarChar, 100).Value = (object)job.ConcurrencyKey ?? DBNull.Value;
                cmd.Parameters.Add("@LastRunStatus", SqlDbType.NVarChar, 50).Value = (object)job.LastRunStatus ?? DBNull.Value;
                cmd.Parameters.Add("@LastRunUtc", SqlDbType.DateTime2).Value = (object)job.LastRunUtc ?? DBNull.Value;

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void Delete(int id)
        {
            using (var conn = Db.CreateConnection())
            using (var cmd = new SqlCommand(
                "DELETE FROM dbo.Jobs WHERE JobId=@id", conn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void UpdateLastRun(int jobId, string status, DateTime? lastRunUtc)
        {
            using (var conn = Db.CreateConnection())
            using (var cmd = new SqlCommand(
                "UPDATE dbo.Jobs SET LastRunStatus=@s, LastRunUtc=@d WHERE JobId=@id", conn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = jobId;
                cmd.Parameters.Add("@s", SqlDbType.NVarChar, 50).Value = (object)status ?? DBNull.Value;
                cmd.Parameters.Add("@d", SqlDbType.DateTime2).Value = (object)lastRunUtc ?? DBNull.Value;

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
