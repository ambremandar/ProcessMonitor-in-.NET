using System;
using System.Management;

namespace WMIProvider
{
    /// <summary>
    /// State of the windows service
    /// </summary>
    public enum ServiceState
    {
        /// <summary>
        /// Running
        /// </summary>
        Running,
        /// <summary>
        /// Stopped
        /// </summary>
        Stopped,
        /// <summary>
        /// Paused
        /// </summary>
        Paused,
        /// <summary>
        /// StartPending
        /// </summary>
        StartPending,
        /// <summary>
        /// StopPending
        /// </summary>
        StopPending,
        /// <summary>
        /// PausePending
        /// </summary>
        PausePending,
        /// <summary>
        /// ContinuePending
        /// </summary>
        ContinuePending,
        /// <summary>
        /// NotFound
        /// </summary>
        NotFound
    }

    /// <summary>
    /// StartMode of the windows service
    /// </summary>
    public enum ServiceStartMode
    {
        /// <summary>
        /// Automatic
        /// </summary>
        Automatic,
        /// <summary>
        /// Manual
        /// </summary>
        Manual,
        /// <summary>
        /// Disabled
        /// </summary>
        Disabled
    }

    /// <summary>
    /// This class handles all service management related tasks
    /// </summary>
    public class ServiceManager
    {
        #region methods
        /// <summary>
        /// Gets the state of a Windows Service
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        public ServiceState GetServiceState(Process process)
        {
            var serviceState = ServiceState.Stopped;

            var objPath = string.Format("Win32_Service.Name='{0}'", GetServiceName(process));
            using (var service = new ManagementObject(new ManagementPath(objPath)))
            {
                try
                {
                    var state = service.Properties["State"].Value.ToString().Trim();
                    switch (state)
                    {
                        case "Running":
                            serviceState = ServiceState.Running;
                            break;
                        case "Stopped":
                            serviceState = ServiceState.Stopped;
                            break;
                        case "Paused":
                            serviceState = ServiceState.Paused;
                            break;
                        case "Start Pending":
                            serviceState = ServiceState.StartPending;
                            break;
                        case "Stop Pending":
                            serviceState = ServiceState.StopPending;
                            break;
                        case "Continue Pending":
                            serviceState = ServiceState.ContinuePending;
                            break;
                        case "Pause Pending":
                            serviceState = ServiceState.PausePending;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    if (ex.Message.ToLower().Trim() == "not found")
                        return ServiceState.NotFound;
                    throw;
                }
            }
            return serviceState;
        }

        /// <summary>
        /// Gets the start mode of a Windows Service
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        public ServiceStartMode GetServiceStartMode(Process process)
        {
            var serviceStartMode = ServiceStartMode.Disabled;

            var objPath = string.Format("Win32_Service.Name='{0}'", GetServiceName(process));
            using (var service = new ManagementObject(new ManagementPath(objPath)))
            {
                try
                {
                    var state = service.Properties["StartMode"].Value.ToString().Trim();
                    switch (state)
                    {
                        case "Auto":
                            serviceStartMode = ServiceStartMode.Automatic;
                            break;
                        case "Manual":
                            serviceStartMode = ServiceStartMode.Manual;
                            break;
                        case "Disabled":
                            serviceStartMode = ServiceStartMode.Disabled;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    return ServiceStartMode.Disabled;
                }
            }
            return serviceStartMode;
        }

        /// <summary>
        /// Restarts a Windows Service
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        public ProcessReturnCode RestartService(Process process)
        {
            // Stop the service
            StopService(process);
            
            // Start the service
            var returnstatus = StartService(process);
           
            return returnstatus;
        }

        /// <summary>
        /// Starts a Windows Service
        /// </summary>
        /// <param name="process"></param>
        /// <returns>return status after Starting service</returns>
        public ProcessReturnCode StartService(Process process)
        {
            var objPath = string.Format("Win32_Service.Name='{0}'", GetServiceName(process));
            using (var service = new ManagementObject(new ManagementPath(objPath)))
            {
                try
                {
                    var outParams = service.InvokeMethod("StartService",null, null);

                    if (outParams != null)
                        return (ProcessReturnCode)Enum.Parse(typeof(ProcessReturnCode),outParams["ReturnValue"].ToString());
                    return ProcessReturnCode.UnknownFailure;
                }
                catch (Exception ex)
                {
                    if (ex.Message.ToLower().Trim() == "not found")
                        return ProcessReturnCode.ServiceNotFound;
                    throw;
                }
            }
        }

        /// <summary>
        /// Stops a Windows Service
        /// </summary>
        /// <param name="process"></param>
        /// <returns>return status after stopping service</returns>
        public ProcessReturnCode StopService(Process process)
        {
            var objPath = string.Format("Win32_Service.Name='{0}'", GetServiceName(process));
            using (var service = new ManagementObject(new ManagementPath(objPath)))
            {
                try
                {
                    var outParams = service.InvokeMethod("StopService", null, null);

                    if (outParams != null)
                        return (ProcessReturnCode)Enum.Parse(typeof(ProcessReturnCode), outParams["ReturnValue"].ToString());
                    return ProcessReturnCode.UnknownFailure;
                }
                catch (Exception ex)
                {
                    if (ex.Message.ToLower().Trim() == "not found")
                        return ProcessReturnCode.ServiceNotFound;
                    throw;
                }
            }
        }

        /// <summary>
        /// Get service name
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        private string GetServiceName(Process process)
        {
            return process.ServiceName ?? process.Name;
        }

        #endregion
    }
}
