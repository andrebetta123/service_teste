using System.Web.Http;
using Swashbuckle.Application;

namespace ups_Work_Job_API
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Rotas Web API
            config.MapHttpAttributeRoutes();
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            // JSON
            var json = config.Formatters.JsonFormatter;
            json.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;

            // Swagger: registrar apenas uma vez
            //config.EnableSwagger(c =>
            //{
            //    c.SingleApiVersion("v31", "ups_Work_Job_API");
            //})
            //.EnableSwaggerUi();
        }
    }
}
