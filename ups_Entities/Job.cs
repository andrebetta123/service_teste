
namespace ups_Entities
{
    public class Job
    {
        public int JobId { get; set; }

        public string Name { get; set; }

        public bool Enabled { get; set; } = true;

        public int MaxRetries { get; set; } = 3;

        public int RetryDelaySec { get; set; } = 60;

        public string ConcurrencyKey { get; set; }

        public string LastRunStatus { get; set; }

        public System.DateTime? LastRunUtc { get; set; }
    }
}
