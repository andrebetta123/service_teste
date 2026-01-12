
using System;

namespace ups_Entities
{
    public class JobSchedule
    {
        public int ScheduleId { get; set; }

        public int JobId { get; set; }

        public bool Enabled { get; set; } = true;

        public RecurrenceType RecurrenceType { get; set; }

        public int? IntervalN { get; set; }

        public TimeSpan? TimeOfDay { get; set; }

        public string DaysOfWeek { get; set; }

        public byte? DayOfMonth { get; set; }

        public byte? MonthOfYear { get; set; }

        public DateTime? FixedDateTimeUtc { get; set; }

        public DateTime? StartDateUtc { get; set; }

        public DateTime? EndDateUtc { get; set; }

        public string TimeZoneId { get; set; } = "UTC";

        public DateTime? NextRunUtc { get; set; }

        public DateTime? LastEvaluatedUtc { get; set; }
    }
}
