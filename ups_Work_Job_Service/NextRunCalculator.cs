
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace ups_Work_Job_Service
{
    internal static class NextRunCalculator
    {
        public static async Task<DateTime?> ComputeNextRunUtcAsync(int scheduleId, int jobId)
        {
            var s = await LoadScheduleAsync(scheduleId).ConfigureAwait(false);
            if (!s.Enabled) return null;

            DateTime nowUtc = DateTime.UtcNow;
            var tz = TimeZoneInfo.FindSystemTimeZoneById(s.TimeZoneId ?? "UTC");
            DateTime nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);

            DateTime? nextLocal = s.RecurrenceType switch
            {
                0 => s.FixedDateTimeUtc.HasValue
                        ? (s.FixedDateTimeUtc.Value <= nowUtc ? (DateTime?)null
                           : TimeZoneInfo.ConvertTimeFromUtc(s.FixedDateTimeUtc.Value, tz))
                        : null,

                1 => s.IntervalN.HasValue ? nowLocal.AddMinutes(s.IntervalN.Value) : (DateTime?)null, // every N minutes
                2 => s.IntervalN.HasValue ? nowLocal.AddHours(s.IntervalN.Value) : (DateTime?)null, // every N hours
                3 => s.TimeOfDay.HasValue ? NextDaily(nowLocal, s.TimeOfDay.Value) : (DateTime?)null, // daily
                4 => (s.TimeOfDay.HasValue && !string.IsNullOrWhiteSpace(s.DaysOfWeek))
                        ? NextWeekly(nowLocal, s.TimeOfDay.Value, ParseDays(s.DaysOfWeek))
                        : (DateTime?)null,
                5 => (s.TimeOfDay.HasValue && s.DayOfMonth.HasValue)
                        ? NextMonthly(nowLocal, s.DayOfMonth.Value, s.TimeOfDay.Value)
                        : (DateTime?)null,
                6 => (s.TimeOfDay.HasValue && s.DayOfMonth.HasValue && s.MonthOfYear.HasValue)
                        ? NextYearly(nowLocal, s.MonthOfYear.Value, s.DayOfMonth.Value, s.TimeOfDay.Value)
                        : (DateTime?)null,
                _ => null
            };

            if (!nextLocal.HasValue) return null;

            var nextUtc = TimeZoneInfo.ConvertTimeToUtc(nextLocal.Value, tz);
            if (s.StartDateUtc.HasValue && nextUtc < s.StartDateUtc.Value) nextUtc = s.StartDateUtc.Value;
            if (s.EndDateUtc.HasValue && nextUtc > s.EndDateUtc.Value) return null;

            return nextUtc;
        }

        #region Helpers
        private static DateTime NextDaily(DateTime nowLocal, TimeSpan tod)
        {
            var candidate = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day).Add(tod);
            return candidate <= nowLocal ? candidate.AddDays(1) : candidate;
        }

        private static DateTime NextWeekly(DateTime nowLocal, TimeSpan tod, int[] days)
        {
            for (int i = 0; i <= 7; i++)
            {
                var d = nowLocal.AddDays(i);
                if (days.Contains((int)d.DayOfWeek))
                {
                    var candidate = new DateTime(d.Year, d.Month, d.Day).Add(tod);
                    if (candidate > nowLocal) return candidate;
                }
            }
            var nextWeek = nowLocal.AddDays(7);
            return new DateTime(nextWeek.Year, nextWeek.Month, nextWeek.Day).Add(tod);
        }

        private static DateTime NextMonthly(DateTime nowLocal, int day, TimeSpan tod)
        {
            int y = nowLocal.Year, m = nowLocal.Month;
            int dim = DateTime.DaysInMonth(y, m);
            int d = Math.Min(day, dim);
            var candidate = new DateTime(y, m, d).Add(tod);
            if (candidate <= nowLocal)
            {
                m = m == 12 ? 1 : m + 1;
                y = m == 1 ? y + 1 : y;
                dim = DateTime.DaysInMonth(y, m);
                d = Math.Min(day, dim);
                candidate = new DateTime(y, m, d).Add(tod);
            }
            return candidate;
        }

        private static DateTime NextYearly(DateTime nowLocal, int month, int day, TimeSpan tod)
        {
            int y = nowLocal.Year;
            int dim = DateTime.DaysInMonth(y, month);
            int d = Math.Min(day, dim);
            var candidate = new DateTime(y, month, d).Add(tod);
            if (candidate <= nowLocal)
            {
                y += 1;
                dim = DateTime.DaysInMonth(y, month);
                d = Math.Min(day, dim);
                candidate = new DateTime(y, month, d).Add(tod);
            }
            return candidate;
        }

        private static int[] ParseDays(string csv) =>
            string.IsNullOrWhiteSpace(csv) ? Array.Empty<int>()
                                           : csv.Split(',').Select(s => int.Parse(s.Trim())).ToArray();
        #endregion

        private static async Task<ScheduleRow> LoadScheduleAsync(int sid)
        {
            using (var conn = new SqlConnection(ConfigurationManager.ConnectionStrings[ConfigurationManager.AppSettings["DbConnName"] ?? "Default"].ConnectionString))
            using (var cmd = new SqlCommand(@"
SELECT Enabled, RecurrenceType, IntervalN, TimeOfDay, DaysOfWeek, DayOfMonth, MonthOfYear,
       FixedDateTimeUtc, StartDateUtc, EndDateUtc, TimeZoneId
FROM dbo.JobSchedules WHERE ScheduleId = @sid;", conn))
            {
                cmd.Parameters.Add("@sid", SqlDbType.Int).Value = sid;
                await conn.OpenAsync().ConfigureAwait(false);
                using (var rd = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    if (!rd.Read()) throw new InvalidOperationException("Schedule não encontrado");
                    return new ScheduleRow
                    {
                        Enabled = rd.GetBoolean(0),
                        RecurrenceType = rd.GetByte(1),
                        IntervalN = rd.IsDBNull(2) ? (int?)null : rd.GetInt32(2),
                        TimeOfDay = rd.IsDBNull(3) ? (TimeSpan?)null : rd.GetTimeSpan(3),
                        DaysOfWeek = rd.IsDBNull(4) ? null : rd.GetString(4),
                        DayOfMonth = rd.IsDBNull(5) ? (byte?)null : rd.GetByte(5),
                        MonthOfYear = rd.IsDBNull(6) ? (byte?)null : rd.GetByte(6),
                        FixedDateTimeUtc = rd.IsDBNull(7) ? (DateTime?)null : rd.GetDateTime(7),
                        StartDateUtc = rd.IsDBNull(8) ? (DateTime?)null : rd.GetDateTime(8),
                        EndDateUtc = rd.IsDBNull(9) ? (DateTime?)null : rd.GetDateTime(9),
                        TimeZoneId = rd.IsDBNull(10) ? "UTC" : rd.GetString(10)
                    };
                }
            }
        }
    }

    internal sealed class ScheduleRow
    {
        public bool Enabled { get; set; }
        public byte RecurrenceType { get; set; }
        public int? IntervalN { get; set; }
        public TimeSpan? TimeOfDay { get; set; }
        public string DaysOfWeek { get; set; }
        public byte? DayOfMonth { get; set; }
        public byte? MonthOfYear { get; set; }
        public DateTime? FixedDateTimeUtc { get; set; }
        public DateTime? StartDateUtc { get; set; }
        public DateTime? EndDateUtc { get; set; }
        public string TimeZoneId { get; set; }
    }
}
