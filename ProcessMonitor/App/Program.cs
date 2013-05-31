using System;
using System.ServiceProcess;
using WMIProvider;

namespace ProcessMonitor
{
    public class Program
    {
       /// <summary>
       /// ProcessMonitorInterface instance
       /// </summary>
       private static ProcessMonitorInterface processMonitorInterface = new ProcessMonitorInterface();

       /// <summary>
       /// Main entry point of the application
       /// </summary>
       static void Main(string[] args)
        {            
            // When current process is running in user interactive mode
            if (Environment.UserInteractive)
            {
                // ApplicationExitHandler to handle exit events
                ProcessMonitorInterface.ApplicationExitHandler(processMonitorInterface.ApplicationExitEvent, true);
                // Run process monitor
                processMonitorInterface.Run(args);
            }
            // When current process is running as Windows service
            else
            {
                ServiceBase.Run(new PMService());
            }         
        }
    }
}