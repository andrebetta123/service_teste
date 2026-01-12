
using System.Web.Http;
using ups_Business;
using ups_Entities;

namespace ups_Work_Job_API.Controllers
{
    [RoutePrefix("api/jobs")]
    public class JobsController : ApiController
    {
        private readonly JobsService _service = new JobsService();

        // GET api/jobs
        [HttpGet, Route("")]
        public IHttpActionResult GetAll()
        {
            return Ok(_service.GetAll());
        }

        // GET api/jobs/5
        [HttpGet, Route("{id:int}")]
        public IHttpActionResult Get(int id)
        {
            var job = _service.GetById(id);
            return job == null ? (IHttpActionResult)NotFound() : Ok(job);
        }

        // POST api/jobs
        [HttpPost, Route("")]
        public IHttpActionResult Create([FromBody] Job job)
        {
            var res = _service.Create(job);

            if (!res.Success)
                return BadRequest(res.Error);

            return Created($"api/jobs/{res.Data.JobId}", res.Data);
        }

        // PUT api/jobs/5
        [HttpPut, Route("{id:int}")]
        public IHttpActionResult Update(int id, [FromBody] Job job)
        {
            job.JobId = id;
            var res = _service.Update(job);

            if (!res.Success)
                return BadRequest(res.Error);

            return Ok();
        }

        // DELETE api/jobs/5
        [HttpDelete, Route("{id:int}")]
        public IHttpActionResult Delete(int id)
        {
            var res = _service.Delete(id);

            if (!res.Success)
                return BadRequest(res.Error);

            return Ok();
        }

        // POST api/jobs/5/run
        [HttpPost, Route("{id:int}/run")]
        public IHttpActionResult Run(int id)
        {
            var res = _service.RunJob(id);

            if (!res.Success)
                return BadRequest(res.Error);

            return Ok(new
            {
                message = "Execução concluída. Verifique o histórico."
            });
        }
    }
}
