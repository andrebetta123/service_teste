using System.Web;
using System.Web.Http;

namespace ups_Work_Job_API
{
    public class WebApiApplication : HttpApplication
    {
        protected void Application_Start()
        {
            // Registra toda configuração da Web API (inclui Swagger) em um único ponto
            GlobalConfiguration.Configure(WebApiConfig.Register);
        }
    }
}
