using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;

namespace WMIProvider
{
    /// <summary>
    /// Gives the status of a process
    /// </summary>
    public enum ProcessState
    {
        ///<summary>
        /// Process is Running
        ///</summary>
        Running,

        ///<summary>
        /// Process is Stopped
        ///</summary>
        Stopped,

        ///<summary>
        /// Process has errored
        ///</summary>
        Error
    }

    /// <summary>
    /// This class initializes various connection params
    /// </summary>
    internal class ProcessConnection
    {
        /// <summary>
        /// Sets various process connection options
        /// </summary>
        /// <returns></returns>
        public static ConnectionOptions ProcessConnectionOptions()
        {
            var options = new ConnectionOptions
            {
                Impersonation = ImpersonationLevel.Impersonate,
                Authentication = AuthenticationLevel.Default,
                EnablePrivileges = true
            };
            return options;
        }

        /// <summary>
        /// Sets the scope of the connection i.e what is the default WMI namespace for a process, what is the machine name etc
        /// </summary>
        /// <param name="machineName"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static ManagementScope ConnectionScope(string machineName, ConnectionOptions options)
        {
            var connectScope = new ManagementScope { Path = new ManagementPath(@"\\" + machineName + @"\root\CIMV2"), Options = options };
            try
            {
                connectScope.Connect();
            }
            catch (ManagementException ex)
            {
                Console.WriteLine("An Error Occurred: {0}", ex.ToString());
            }
            return connectScope;
        }

        /// <summary>
        /// Sets the scope of the connection using given machine name,WMI namespace and connection options
        /// </summary>
        /// <param name="machineName"></param>
        /// <param name="path"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static ManagementScope ConnectionScope(string machineName, string path, ConnectionOptions options)
        {
            var connectScope = new ManagementScope { Path = new ManagementPath(path), Options = options };
            try
            {
                connectScope.Connect();
            }
            catch (ManagementException ex)
            {
                Console.WriteLine("An Error Occurred: {0}", ex.ToString());
            }
            return connectScope;
        }
    }

    /// <summary>
    /// This class handles all process management related tasks
    /// </summary>
    public class ProcessManager : IProcessManagement
    {
        #region "fields"
        /// <summary>
        /// ServiceManager instance
        /// </summary>
        private ServiceManager serviceManager;

        /// <summary>
        /// ConnectionOptions instance
        /// </summary>
        readonly ConnectionOptions options;

        /// <summary>
        /// ManagementScope instance
        /// </summary>
        readonly ManagementScope connectionScope;

        /// <summary>
        /// Default timeout interval for checking heartbeat
        /// </summary>
        private readonly int defaultHeartbeatTimeout = Convert.ToInt32(System.Configuration.ConfigurationSettings.AppSettings["DefaultHeartbeatTimeout"] ?? "15");        

        /// <summary>
        /// MonitoredProcessName
        /// </summary>
        private string monitoredProcessName;

        /// <summary>
        /// CurrentProcessState
        /// </summary>
        private ProcessState currentProcessState;
        #endregion

        #region "constructors"
        ///<summary>
        /// ProcessManager
        ///</summary>
        public ProcessManager()
        {
            serviceManager = new ServiceManager();
            options = ProcessConnection.ProcessConnectionOptions();
            connectionScope = ProcessConnection.ConnectionScope(Environment.MachineName, options);            
        }
        #endregion

        #region "methods"
        /// <summary>
        /// Returns if the process is running,stopped or errored
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        public ProcessState GetProcessState(Process process)
        {
            var isProcessRunning = ProcessManagementHelper.IsProcessRunning(connectionScope, process.Name);
            return isProcessRunning ? ProcessState.Running : ProcessState.Stopped;
        }

        /// <summary>
        /// Checks if the process is running
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        public bool IsProcessRunning(Process process)
        {
            if (process.Type.Equals("Service"))
            {
                var serviceState = serviceManager.GetServiceState(process);
                return serviceState == ServiceState.Running ? true : false;
            }
            else
            {
                var processState = GetProcessState(process);
                return processState == ProcessState.Running ? true : false;
            }
        }

        /// <summary>
        /// Checks if the process is stopped
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        public bool IsProcessStopped(Process process)
        {
            if (process.Type.Equals("Service"))
            {
                var serviceState = serviceManager.GetServiceState(process);
                return serviceState == ServiceState.Stopped ? true : false;
            }
            else
            {
                var processState = GetProcessState(process);
                return processState == ProcessState.Stopped ? true : false;
            }
        }

        /// <summary>
        /// Checks if the heartbeat message from the app is recieved
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        public ProcessState CheckHeartbeat(Process process)
        {
            this.monitoredProcessName = process.Name;

            var heartbeatTimeout = process.HeartbeatTimeout != 0 ? process.HeartbeatTimeout : defaultHeartbeatTimeout;

            // Check for heartbeat from the application using WMI provider
            // Timeout on eventwatcher should be more than the frequency with which app publishes event to WMI
            var eventWatcherOptions = new EventWatcherOptions(null, new TimeSpan(0, 0, heartbeatTimeout), 1);

            // Initialize an event watcher and subscribe to timer events 
            var watcher = new ManagementEventWatcher(@"\\" + Environment.MachineName + @"\root\AmbreCorp", "SELECT * FROM ProcessMonitor WHERE ProcessName = '" + this.monitoredProcessName + "'", eventWatcherOptions);

            try
            {
                // Initially set currentProcessState to Stopped.
                this.currentProcessState = ProcessState.Stopped;
              
                // Check if windows error reporting is showing
                this.CheckWindowsErrorReportingMessage();

                // Set up a listener for events
                watcher.EventArrived += HandleEventWatcher;

                var stopWatch = Stopwatch.StartNew();

                // Start listening
                watcher.Start();

                // If currentProcessState is stopped keep checking if elapsedtime has exceeded heartbeat timeout of the process

                while (this.currentProcessState == ProcessState.Stopped)
                {
                    var elapsedTicks = stopWatch.ElapsedTicks;

                    var elapsedTimeInSeconds = elapsedTicks/Stopwatch.Frequency;

                    if ((int)elapsedTimeInSeconds > heartbeatTimeout)
                    {
                        break;
                    }

                    System.Threading.Thread.Sleep(2000);
                }

                stopWatch.Stop();
                
                return this.currentProcessState;
            }
            catch (Exception ex)
            {
                // for some reason when eventwatcher times out it throws a timed out exception so that needs to be caught.This tells us that event was not receieved in set time
                if (ex.Message.ToLower().Trim() == "timed out")
                {
                    Console.WriteLine(string.Format("Event was not received from {0}.Event watcher timed out.", this.monitoredProcessName));
                    return ProcessState.Error;
                }
                else
                {
                    return ProcessState.Error;
                }
            }
            finally
            {
                if (watcher != null)
                {
                    // for some reason if fault window is open then watcher.stop() does not work hence need to close that error window first so call CheckWindowsErrorReportingMessage again.
                    this.CheckWindowsErrorReportingMessage();

                    // Stop listening
                    watcher.Stop();

                    // Remove listener
                    watcher.EventArrived -= HandleEventWatcher;

                    watcher.Dispose();
                }
            }
        }

        /// <summary>
        /// EventWatcher handler to get state of the monitored process
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HandleEventWatcher(object sender, EventArrivedEventArgs e)
        {
            var obj = e.NewEvent;
            if (obj != null)
            {
                if (obj.GetPropertyValue("ProcessName").ToString().Equals(this.monitoredProcessName))
                {
                    // Using ManagementDateTimeConverter class to convert datetime string to more readable format
                    Console.WriteLine("Event received. TimeStamp: {0}", ManagementDateTimeConverter.ToDateTime(obj.GetPropertyValue("Heartbeat").ToString()));

                    this.currentProcessState = ProcessState.Running;
                    return;
                }
            }
            else
            {
                this.currentProcessState = ProcessState.Error;
                return;
            }
        }

        /// <summary>
        /// If windows error reporting is blocking ConnexCS app then terminate the error window.
        /// Detect the windows error reporting processid.
        /// Detect the parent processid for wer process and check if this id is the same as the CS app process id.
        /// This will help us determine if windows error is blocking CS app.
        /// </summary>
        private void CheckWindowsErrorReportingMessage()
        {
            try
            {
                // Check if fault window is blocking UI thread
                const string connexCSProcess = "ConnexCS.exe";
                const string windowsFaultProcess = "WerFault.exe";

                var parentProcessProperties = ProcessProperties(connexCSProcess);
                string parentProcessId = null;
                var windowsFaultProcessProperties = ProcessProperties(windowsFaultProcess);
                string windowsFaultProcessId = null;

                // get ProcessId for Parent Process(whose status is being monitored)
                foreach (var parentProcessProperty in parentProcessProperties)
                {
                    var property = parentProcessProperty.Split(':');

                    if (property[0].Trim() == "ProcessId")
                    {
                        parentProcessId = property[1].Trim();
                        break;
                    }
                }

                // get ProcessId for fault windows
                foreach (var windowsFaultProcessProperty in windowsFaultProcessProperties)
                {
                    var property = windowsFaultProcessProperty.Split(';');

                    // CommandLine: C:\Windows\SysWOW64\WerFault.exe -u -p 3484 -s 2528
                    if (property[0].Trim() == "CommandLine")
                    {
                        var command = property[1];

                        if (command.IndexOf("-p") != -1)
                        {
                            var substring = command.Substring(command.IndexOf("-p"));
                            var substrarr = substring.Split(' ');
                            if (substrarr[0] == "-p")
                            {
                                // this gives the processid that the fault windows is attached to
                                windowsFaultProcessId = substrarr[1].Trim();
                                break;
                            }
                        }
                    }
                }

                if (parentProcessId != null && windowsFaultProcessId != null && parentProcessId == windowsFaultProcessId)
                {
                    // this means that error window is hanging the application. Close the fault window.
                    Console.WriteLine("Found Windows error window hanging CS.");
                    Console.WriteLine(string.Format("Stopping the process: {0}", windowsFaultProcess));                  

                    this.TerminateProcess(windowsFaultProcess);
                }
            }
            catch (Exception)
            {               
            }
        }

        /// <summary>
        /// Returns a list of properties for a given process
        /// </summary>
        /// <param name="processName"></param>
        /// <returns></returns>
        public List<string> ProcessProperties(string processName)
        {
            var processProperties = ProcessManagementHelper.ProcessProperties(connectionScope, processName);
            return processProperties;
        }

        /// <summary>
        /// Method to start a process
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        public ProcessReturnCode CreateProcess(Process process)
        {
            return ProcessManagementHelper.StartProcess(Environment.MachineName, process.DirPath, process.Command);
        }

        /// <summary>
        /// Method to end a process
        /// </summary>
        /// <param name="processName"></param>
        /// <returns></returns>
        public ProcessReturnCode TerminateProcess(string processName)
        {
            return ProcessManagementHelper.KillProcess(connectionScope, processName);
        }

        /// <summary>
        /// Stops a process
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        public ProcessReturnCode StopProcess(Process process)
        {
            ProcessReturnCode returnstatus;
            if (process.Type.Equals("Service"))
            {
                returnstatus = serviceManager.StopService(process);
            }
            else
            {
                returnstatus = TerminateProcess(process.Name);
            }
            return returnstatus;
        }

        /// <summary>
        /// Starts a process
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        public ProcessReturnCode StartProcess(Process process)
        {
            ProcessReturnCode returnstatus;
            if (process.Type.Equals("Service"))
            {
                returnstatus = serviceManager.StartService(process);
            }
            else
            {
                returnstatus = CreateProcess(process);
            }
            return returnstatus;
        }

        /// <summary>
        /// Reboots a computer
        /// </summary>
        /// <returns>0 if success</returns>
        public int RebootComputer()
        {
            try
            {
                var processStartInfo = new ProcessStartInfo("shutdown.exe", "/r /f /t 000");
                System.Diagnostics.Process.Start(processStartInfo);
                return 0;
            }
            catch (Exception)
            {               
                return -1;
            }
        }

        /// <summary>
        /// Gives the time when the computer last restarted
        /// </summary>
        /// <returns></returns>
        public DateTime GetLastBootUpTime()
        {
            var wmiProcessQuery = new SelectQuery("SELECT * FROM  Win32_OperatingSystem");
            using (var wmiObjectSearcher = new ManagementObjectSearcher(connectionScope, wmiProcessQuery))
            {
                return (from ManagementObject managementObject in wmiObjectSearcher.Get()
                        select ManagementDateTimeConverter.ToDateTime(managementObject.GetPropertyValue("LastBootUpTime").ToString())).FirstOrDefault();
            }
        }
        #endregion
    }
}
