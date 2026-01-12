
using System.ComponentModel;
using System.Configuration.Install;  // necessário para Installer
using System.ServiceProcess;

namespace ups_Work_Job_Service
{
    [RunInstaller(true)]
    public class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            var processInstaller = new ServiceProcessInstaller { Account = ServiceAccount.LocalSystem };
            var serviceInstaller = new ServiceInstaller
            {
                ServiceName = "ups_Work_Job_Service",
                DisplayName = "UPS Work Job Service",
                Description = "Serviço Windows para execução de Jobs e agendamentos.",
                StartType = ServiceStartMode.Automatic
            };
            Installers.Add(processInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}
