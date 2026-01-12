
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using ups_Common;
using ups_Entities;

namespace ups_DAO
{
    public class JobSchedulesDao
    {
        private JobSchedule Map(SqlDataReader r) =>
            new JobSchedule
            {
                ScheduleId = r.GetInt32(r.GetOrdinal("ScheduleId")),
                JobId = r.GetInt32(r.GetOrdinal("JobId")),
                Enabled = r.GetBoolean(r.GetOrdinal("Enabled")),
                RecurrenceType = (ups_Entities.RecurrenceType)r.GetByte(r.GetOrdinal("RecurrenceType")),
                IntervalN = r.IsDBNull(r.GetOrdinal("IntervalN"))
                    ? (int?)null
                    : r.GetInt32(r.GetOrdinal("IntervalN")),
                TimeOfDay = r.IsDBNull(r.GetOrdinal("TimeOfDay"))
                    ? (TimeSpan?)null
                    : r.GetTimeSpan(r.GetOrdinal("TimeOfDay")),
                DaysOfWeek = r.IsDBNull(r.GetOrdinal("DaysOfWeek"))
                    ? null
                    : r.GetString(r.GetOrdinal("DaysOfWeek")),
                DayOfMonth = r.IsDBNull(r.GetOrdinal("DayOfMonth"))
                    ? (byte?)null
                    : r.GetByte(r.GetOrdinal("DayOfMonth")),
                MonthOfYear = r.IsDBNull(r.GetOrdinal("MonthOfYear"))
                    ? (byte?)null
                    : r.GetByte(r.GetOrdinal("MonthOfYear")),
                FixedDateTimeUtc = r.IsDBNull(r.GetOrdinal("FixedDateTimeUtc"))
                    ? (DateTime?)null
                    : r.GetDateTime(r.GetOrdinal("FixedDateTimeUtc")),
                StartDateUtc = r.IsDBNull(r.GetOrdinal("StartDateUtc"))
                    ? (DateTime?)null
                    : r.GetDateTime(r.GetOrdinal("StartDateUtc")),
                EndDateUtc = r.IsDBNull(r.GetOrdinal("EndDateUtc"))
                    ? (DateTime?)null
                    : r.GetDateTime(r.GetOrdinal("EndDateUtc")),
                TimeZoneId = r.GetString(r.GetOrdinal("TimeZoneId")),
                NextRunUtc = r.IsDBNull(r.GetOrdinal("NextRunUtc"))
                    ? (DateTime?)null
                    : r.GetDateTime(r.GetOrdinal("NextRunUtc")),
                LastEvaluatedUtc = r.IsDBNull(r.GetOrdinal("LastEvaluatedUtc"))
                    ? (DateTime?)null
                    : r.GetDateTime(r.GetOrdinal("LastEvaluatedUtc"))
            };

        public IEnumerable<JobSchedule> GetDue(DateTime utcNow)
        {
            var list = new List<JobSchedule>();

            using (var conn = Db.CreateConnection())
            using (var cmd = new SqlCommand(
                "SELECT * FROM dbo.JobSchedules " +
                "WHERE Enabled = 1 AND NextRunUtc IS NOT NULL AND NextRunUtc <= @now",
                conn))
            {
                cmd.Parameters.Add("@now", SqlDbType.DateTime2).Value = utcNow;

                conn.Open();
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        list.Add(Map(r));
            }

            return list;
        }

        public IEnumerable<JobSchedule> GetByJobId(int jobId)
        {
            var list = new List<JobSchedule>();

            using (var conn = Db.CreateConnection())
            using (var cmd = new SqlCommand(
                "SELECT * FROM dbo.JobSchedules WHERE JobId = @id",
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

        public void UpdateNextRunAndEval(int scheduleId, DateTime? nextRunUtc, DateTime evalUtc)
        {
            using (var conn = Db.CreateConnection())
            using (var cmd = new SqlCommand(
                "UPDATE dbo.JobSchedules " +
                "SET NextRunUtc = @next, LastEvaluatedUtc = @eval " +
                "WHERE ScheduleId = @id",
                conn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = scheduleId;
                cmd.Parameters.Add("@next", SqlDbType.DateTime2).Value =
                    (object)nextRunUtc ?? DBNull.Value;
                cmd.Parameters.Add("@eval", SqlDbType.DateTime2).Value = evalUtc;

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
