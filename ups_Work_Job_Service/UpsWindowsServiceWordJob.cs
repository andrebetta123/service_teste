
using System;
using System.Configuration;
using System.Diagnostics;
using System.ServiceProcess;

namespace ups_Work_Job_Service
{
    public sealed class UpsWindowsServiceWorkJob : ServiceBase
    {
        private Scheduler _scheduler;

        public UpsWindowsServiceWorkJob()
        {
            ServiceName = "UpsWorkJobService";
            CanStop = true;
            CanPauseAndContinue = false;
            AutoLog = true; // usa EventLog padrão do serviço
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                Trace.TraceInformation("UpsWorkJobService iniciando...");

                int poll = int.TryParse(ConfigurationManager.AppSettings["PollIntervalSec"], out var p) ? p : 15;
                int par = int.TryParse(ConfigurationManager.AppSettings["MaxDegreeOfParallelism"], out var m) ? m : 2;
                string connName = ConfigurationManager.AppSettings["SqlServer"] ?? "Default";

                _scheduler = new Scheduler(poll, par, connName);
                _scheduler.Start();

                Trace.TraceInformation("UpsWorkJobService iniciado.");
            }
            catch (Exception ex)
            {
                Trace.TraceError("Falha ao iniciar serviço: " + ex);
                throw;
            }
        }

        protected override void OnStop()
        {
            Trace.TraceInformation("UpsWorkJobService parando...");
            _scheduler?.Stop();
            Trace.TraceInformation("UpsWorkJobService parado.");
        }
    }

}
