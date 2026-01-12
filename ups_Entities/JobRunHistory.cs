
namespace ups_Entities
{
    public class JobRunHistory
    {
        public long RunId { get; set; }

        public int JobId { get; set; }

        public System.DateTime StartedUtc { get; set; }

        public System.DateTime? FinishedUtc { get; set; }

        public string Status { get; set; }

        public string Message { get; set; }

        public string HostName { get; set; }
    }
}
