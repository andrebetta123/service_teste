
using System;
using System.ServiceProcess;

namespace ups_Work_Job_Service
{
    static class Program
    {
        static void Main()
        {
            ServiceBase.Run(new ServiceBase[]
            {
                new UpsWindowsServiceWordJob()
            });
        }
    }
}
