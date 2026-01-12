
namespace ups_Entities
{
    public class JobStep
    {
        public int StepId { get; set; }

        public int JobId { get; set; }

        public int StepNo { get; set; }

        public string Script { get; set; }

        public int? TimeoutSec { get; set; }
    }
}
