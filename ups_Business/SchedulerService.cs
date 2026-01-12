
using System;
using System.Linq;
using ups_DAO;
using ups_Entities;

namespace ups_Business
{
    public class SchedulerService
    {
        private readonly JobSchedulesDao _dao = new JobSchedulesDao();

        public void EvaluateAndUpdateNextRuns(DateTime utcNow)
        {
            foreach (var sch in _dao.GetDue(utcNow))
            {
                var next = ComputeNextRunUtc(sch, utcNow);
                _dao.UpdateNextRunAndEval(sch.ScheduleId, next, utcNow);
            }
        }

        public DateTime? ComputeNextRunUtc(JobSchedule sch, DateTime utcNow)
        {
            if (sch.EndDateUtc.HasValue && utcNow > sch.EndDateUtc.Value)
                return null;

            var timeOfDay = sch.TimeOfDay ?? TimeSpan.Zero;

            switch (sch.RecurrenceType)
            {
                case RecurrenceType.Once:
                    return (sch.FixedDateTimeUtc.HasValue && sch.FixedDateTimeUtc.Value > utcNow)
                        ? sch.FixedDateTimeUtc.Value
                        : (DateTime?)null;

                case RecurrenceType.Minute:
                    return utcNow.AddMinutes(sch.IntervalN.GetValueOrDefault(1));

                case RecurrenceType.Hour:
                    return utcNow.AddHours(sch.IntervalN.GetValueOrDefault(1));

                case RecurrenceType.Daily:
                {
                    int n = sch.IntervalN.GetValueOrDefault(1);
                    var candidate = utcNow.Date.AddDays(n).Add(timeOfDay);
                    if (sch.StartDateUtc.HasValue && candidate < sch.StartDateUtc.Value)
                        candidate = sch.StartDateUtc.Value.Date.Add(timeOfDay);
                    return candidate;
                }

                case RecurrenceType.Weekly:
                {
                    var days = (sch.DaysOfWeek ?? string.Empty)
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s, out var d) ? d : -1)
                        .Where(d => d >= 0 && d <= 6)
                        .ToArray();

                    if (days.Length == 0) return null;

                    int today = (int)utcNow.DayOfWeek;
                    int nWeeks = sch.IntervalN.GetValueOrDefault(1);

                    for (int offset = 0; offset <= 7 * nWeeks; offset++)
                    {
                        var d = (today + offset) % 7;
                        if (days.Contains(d))
                        {
                            var candidate = utcNow.Date.AddDays(offset).Add(timeOfDay);
                            if (candidate > utcNow) return candidate;
                        }
                    }

                    return utcNow.Date.AddDays(7 * nWeeks).Add(timeOfDay);
                }

                case RecurrenceType.Monthly:
                {
                    int n = sch.IntervalN.GetValueOrDefault(1);
                    byte day = sch.DayOfMonth.GetValueOrDefault(1);

                    var nextMonth = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                        .AddMonths(n);

                    int dim = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
                    int d = Math.Min(day, (byte)dim);

                    return new DateTime(nextMonth.Year, nextMonth.Month, d, 0, 0, 0, DateTimeKind.Utc)
                        .Add(timeOfDay);
                }

                case RecurrenceType.Yearly:
                {
                    int n = sch.IntervalN.GetValueOrDefault(1);
                    byte month = sch.MonthOfYear.GetValueOrDefault(1);
                    byte day = sch.DayOfMonth.GetValueOrDefault(1);

                    int targetYear = utcNow.Year + n;
                    int dim = DateTime.DaysInMonth(targetYear, month);
                    int d = Math.Min(day, (byte)dim);

                    return new DateTime(targetYear, month, d, 0, 0, 0, DateTimeKind.Utc)
                        .Add(timeOfDay);
                }

                default:
                    return null;
            }
        }
    }
}
