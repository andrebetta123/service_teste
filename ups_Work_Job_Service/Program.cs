using System;
using System.Reflection;
using System.ServiceProcess;

namespace ups_Work_Job_Service
{
    static class Program
    {
        static void Main()
        {
            // Se rodar pelo Visual Studio (F5), ficará em modo console
            if (Environment.UserInteractive)
            {
                var svc = new UpsWindowsServiceWorkJob();
                // invoca OnStart/OnStop via reflexão para reaproveitar a lógica
                svc.GetType().GetMethod("OnStart", BindingFlags.Instance | BindingFlags.NonPublic)
                   ?.Invoke(svc, new object[] { new string[0] });

                Console.WriteLine("Serviço em modo console. Pressione ENTER para encerrar...");
                Console.ReadLine();

//                svc.GetType().GetMethod("OnStop", BindingFlags.Instance | BindingFlags.NonPublic)
//                   ?.Invoke(svc, null);
            }
            else
            {
                // Execução como Serviço Windows
                ServiceBase.Run(new ServiceBase[] { new UpsWindowsServiceWorkJob() });
            }
        }
    }
}

