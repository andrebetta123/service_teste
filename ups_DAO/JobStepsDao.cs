
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using ups_Common;
using ups_Entities;

namespace ups_DAO
{
    public class JobStepsDao
    {
        private JobStep Map(SqlDataReader r) =>
            new JobStep
            {
                StepId = r.GetInt32(r.GetOrdinal("StepId")),
                JobId = r.GetInt32(r.GetOrdinal("JobId")),
                StepNo = r.GetInt32(r.GetOrdinal("StepNo")),
                Script = r.GetString(r.GetOrdinal("Script")),
                TimeoutSec = r.IsDBNull(r.GetOrdinal("TimeoutSec"))
                    ? (int?)null
                    : r.GetInt32(r.GetOrdinal("TimeoutSec"))
            };

        public IEnumerable<JobStep> GetByJobId(int jobId)
        {
            var list = new List<JobStep>();

            using (var conn = Db.CreateConnection())
            using (var cmd = new SqlCommand(
                "SELECT StepId, JobId, StepNo, Script, TimeoutSec " +
                "FROM dbo.JobSteps WHERE JobId = @id ORDER BY StepNo",
                conn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = jobId;

                conn.Open();
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        list.Add(Map(r));
            }

            return list;
        }
    }
}
