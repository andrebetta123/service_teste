
using System.Web;
using System.Web.Http;

namespace ups_Work_Job_API
{
    public class WebApiApplication : HttpApplication
    {
        protected void Application_Start()
        {
            // Apenas registra a WebApiConfig
            GlobalConfiguration.Configure(WebApiConfig.Register);

            // ❌ Não chame EnableSwagger aqui.
            // ❌ Não chame EnableSwaggerUi aqui.
        }
    }
}
