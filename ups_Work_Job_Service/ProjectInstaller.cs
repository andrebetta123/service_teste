
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace ups_Work_Job_Service
{
    [RunInstaller(true)]
    public sealed class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            var process = new ServiceProcessInstaller
            {
                Account = ServiceAccount.LocalSystem // ajuste para conta de serviço/usuário quando necessário
            };

            var service = new ServiceInstaller
            {
                ServiceName = "UpsWorkJobService",
                DisplayName = "UPS Work Job Service",
                Description = "Serviço de execução de Jobs parametrizados via SQL",
                StartType = ServiceStartMode.Automatic
            };

            Installers.Add(process);
            Installers.Add(service);
        }
    }
}
