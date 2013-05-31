using System.ServiceProcess;
using System.Threading;

namespace ProcessMonitor
{
    /// <summary>
    /// The service class for Process Monitor Service.
    /// </summary>
    /// /// <remarks>
    /// The actual work is done in ProcessMonitorService class.
    /// This class isn't used when we're running as application.
    /// </remarks>
    partial class PMService : ServiceBase
    {
        public const string ServiceNameConst = "ProcessMonitorService";

        private ProcessMonitorService processMonitorService;
     
        public PMService()
        {
            this.ServiceName = ServiceNameConst;
            this.CanStop = true;
            // Create an instance of ProcessMonitorService
            this.processMonitorService = new ProcessMonitorService();
        }

        protected override void OnStart(string[] args)
        {
           // Start processMonitorService on a separate thread
           ThreadPool.QueueUserWorkItem(mainthread=> this.processMonitorService.Start());
        }

        protected override void OnStop()
        {
            this.processMonitorService.Dispose();
        }
    }
}
