using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace ProcessMonitor
{
    /// <summary>
    /// ProcessMonitorService Installer
    /// </summary>
    [RunInstaller(true)]
    public partial class PMServiceInstaller : Installer
    {
        /// <summary>
        /// ServiceProcessInstaller
        /// </summary>
        private ServiceProcessInstaller process;

        /// <summary>
        /// ServiceInstaller
        /// </summary>
        private ServiceInstaller service;

        /// <summary>
        /// ServiceDisplayName
        /// </summary>
        private const string ServiceDisplayName = "Process Monitor Service";

        /// <summary>
        /// ServiceDescription
        /// </summary>
        private const string ServiceDescription = "Monitors health of applications and services.";

        public PMServiceInstaller()
        {
            this.process = new ServiceProcessInstaller {Account = ServiceAccount.LocalSystem};
            this.service = new ServiceInstaller
                               {
                                   StartType = ServiceStartMode.Automatic,
                                   ServiceName = PMService.ServiceNameConst,
                                   Description = ServiceDescription,
                                   DisplayName = ServiceDisplayName
                               };
            this.Installers.Add(this.process);
            this.Installers.Add(this.service);
        }
    }
}
