using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using ProcessMonitor.Properties;
using WMIProvider;
using Process = WMIProvider.Process;

namespace ProcessMonitor
{
    public class ProcessMonitorService : IDisposable
    {
        /// <summary>
        /// Used to indicate if main thread should stop running.
        /// </summary>
        private static bool stopRunning;

        /// <summary>
        /// ProcessManager instance.
        /// </summary>
        private ProcessManager processManager;

        /// <summary>
        /// Used to store number of restart attempts.
        /// </summary>
        private static int numberOfRestartAttempts;

        /// <summary>
        /// Worker thread instance.
        /// </summary>
        private Thread workerThread;        

        /// <summary>
        /// Wait handle used to sync between main thread and worker thread.
        /// </summary>
        private AutoResetEvent waitHandle;

        /// <summary>
        /// List of currently available processes on the system
        /// </summary>
        private List<Process> currentListOfAvailableProcesses;

        /// <summary>
        /// List of currently unavailable processes on the system
        /// </summary>
        private List<Process> currentListOfUnAvailableProcesses;

        /// <summary>
        /// Default timeout when stopping or starting a process
        /// </summary>
        private readonly int defaultRestartTimeout = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["DefaultRestartTimeout"] ?? "30");

        /// <summary>
        /// Default allowed number of restart attempts
        /// </summary>
        private readonly int defaultNumberOfRestartAttempts = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["DefaultNumberOfRestartAttempts"] ?? "3");

        /// <summary>
        /// Default time span after system reboot within which StartupPlan can be executed
        /// </summary>
        private readonly int defaultStartupPlanTimeWindow = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["DefaultStartupPlanTimeWindow"] ?? "5");

        /// <summary>
        /// File used to keep status of system reboot by Process Monitor(i.e. if reboot fixed the process issue or not)
        /// </summary>
        private const string processMonitorRebootFileName = "Reboot.txt";

        /// <summary>
        /// List of failed processes
        /// </summary>
        private List<Process> failedProcesses;

        /// <summary>
        /// NotificationBox instance
        /// </summary>
        private NotificationBox notificationBox; 

        /// <summary>
        /// Background worker class used to show splash screen UI on system start up
        /// </summary>
        private BackgroundWorker startupPlanProgressUIDispatcher;            

        /// <summary>
        /// ProcessMonitorService
        /// </summary>
        /// <param name="logger">ProcessMonitorLogWriter</param>
        public ProcessMonitorService()
        {            
            this.InitializeParams();
        }

        /// <summary>
        /// Called by Process Monitor Service(PMService)
        /// </summary>
        public void Start()
        { 
           this.InitializeParams();
           this.Execute();
        }

        /// <summary>
        /// Executes the logic to check the health of the applications and take appropriate actions
        /// </summary>
        public void Execute()
        {
            try
            {
                // First get the process list from Monitoring.Xml file
                var processes = ReadProcessConfiguration();

                while (!stopRunning)
                {
                    Console.WriteLine("Starting Main Thread...");                   

                    // Get list of processes available on the machine.This list will be less than or equal to the list of processes in monitoring xml
                    currentListOfAvailableProcesses = AvailableProcesses(processes);

                    // Get list of processes not available on the machine.
                    // This list is used to limit logging unavailable processes only once. If the process already exists in this list then don't log message for it.
                    currentListOfUnAvailableProcesses = processes.Except(currentListOfAvailableProcesses).ToList();

                    // Add states for available processes as Running to summary log
                    var availableProcessesAndStates = currentListOfAvailableProcesses.ToDictionary(currentListOfAvailableProcess => currentListOfAvailableProcess.Name, currentListOfAvailableProcess => "Running");                   

                    // Add states for unavailable processes as Stopped to summary log
                    var unavailableProcessesAndStates = currentListOfUnAvailableProcesses.ToDictionary(currentListOfUnAvailableProcess => currentListOfUnAvailableProcess.Name, currentListOfUnAvailableProcess => "Stopped");                  

                    // If the Lastbootuptime is within DefaultStartupTimeWindow then execute initial startup plan
                    var lastBootUpTime = processManager.GetLastBootUpTime();
                    
                    foreach (var process in currentListOfAvailableProcesses)
                    {                           
                        CheckForDeadProcess(process);
                        // If process is not happy add it to the list of failed processes
                        if ((process.CurrentState == State.IsDead) && !failedProcesses.Exists(p => p.Name == process.Name))
                        {
                            failedProcesses.Add(process);
                        }
                    }

                    if (failedProcesses.Count > 0)
                    {
                        // Start action plan if there are failed processes
                        StartActionPlan(failedProcesses);
                    }                   

                    Thread.Sleep(10000);
                }
            }
            catch (Exception)
            {
                // log something
            }
        }

        /// <summary>
        /// releases resources used by ProcessMonitorService
        /// </summary>
        public void Dispose()
        {
            this.Disposed(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Initialize parameters
        /// </summary>
        private void InitializeParams()
        {
            processManager = new ProcessManager();
            currentListOfAvailableProcesses = new List<Process>();
            failedProcesses = new List<Process>();
            notificationBox = new NotificationBox();            
            waitHandle = new AutoResetEvent(true);            
        }        
        
        /// <summary>
        /// Checks health of processes that don't have heartbeat setup. Also if processes with heartbeat are hung then checks to see if those processes are dead
        /// </summary>
        /// <param name="process"></param>
        private void CheckForDeadProcess(Process process)
        {
            // If process doesnot have heartbeat setup
            if(!process.HasHeartBeat)
            {
                // Check if process is running i.e this in short means if process is showing in taskmanager
                if(!processManager.IsProcessRunning(process))
                {
                    Console.WriteLine(process.Name + ": Process state is Dead");                   

                    process.CurrentState = State.IsDead;
                }
                else
                {
                    Console.WriteLine(process.Name + ": Process state is Happy");

                    process.CurrentState = State.IsHappy;
                }
            }
            else
            {
                // Process has heartbeat setup
                // Heartbeat was missed but now checking if process is running
                if (process.CurrentState == State.IsHung)
                {
                     // Check if process is running i.e this in short means if process is showing in taskmanager
                    if (!processManager.IsProcessRunning(process))
                    {
                        Console.WriteLine(process.Name + ": Process state is Dead");                       

                        // Heartbeat missed and process is not there hence dead
                        process.CurrentState = State.IsDead;
                    }
                }
            }
        }

        /// <summary>
        /// Checks the list of non-working processes and executes a Restart or Reboot plan or End plan
        /// </summary>
        /// <param name="nonWorkingProcesses">List of processes that are not happy</param>
        private void StartActionPlan(List<Process> nonWorkingProcesses)
        {
            waitHandle.WaitOne();

            try
            {                
                // Start Plan Thread
                Console.WriteLine("Starting Worker Thread...");   
                   
                // Check if reboot file exists and IsValid=1. If IsValid=1 then reboot didnot fix the problem
                if (GetRebootIsValidFlag())
                {
                    // Execute Final plan
                    var threadStart = new ThreadStart(ExecuteFinalPlan);
                    workerThread = new Thread(threadStart);
                }
                else
                {
                    // Continue with normal plan execution
                    if (numberOfRestartAttempts < defaultNumberOfRestartAttempts)
                    {
                        // Make an attempt to restart the process
                        Console.WriteLine(string.Format("Restart Attempt:{0}", numberOfRestartAttempts));
                       
                        ThreadStart threadStart = () => ExecuteRestartPlan(nonWorkingProcesses);
                        workerThread = new Thread(threadStart);
                        numberOfRestartAttempts++;
                    }
                    else
                    {
                        // Reboot the station
                        Console.WriteLine("Exceeded number of restart attempts. Rebooting the computer");
                                
                        ThreadStart threadStart = () => ExecuteRebootPlan(nonWorkingProcesses);
                        workerThread = new Thread(threadStart);
                    }
                }
                       
                workerThread.Start();
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);               
            }
        }

        /// <summary>
        ///  Startup plan is executed when system boots up. This will allow all services to startup without Process Monitor interfering the start of services
        /// </summary>
        private void ExecuteStartupPlan()
        {
            waitHandle.WaitOne();

            try
            {
                Console.WriteLine("Execute startup plan that allows system to load up services when system starts up");                

                if (!startupPlanProgressUIDispatcher.IsBusy)
                {
                    startupPlanProgressUIDispatcher.RunWorkerAsync();                   
                }               
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);               
            }
        }

        /// <summary>
        /// Executes a restart plan on all the non-working processes
        /// </summary>
        /// <param name="nonWorkingProcesses"></param>
        private void ExecuteRestartPlan(List<Process> nonWorkingProcesses)
        {          

            // Assign non-working process list to a local variable.
            var nonWorkingProcessesLocal = nonWorkingProcesses;

            Console.WriteLine("Starting Restart Plan");
            // Log the steps of the plan that we are about to execute
            var sbPlan = new StringBuilder();
            sbPlan.AppendLine("Starting Restart Plan: ")
                .AppendLine("Step 1: Convert xml of processes and dependencies into a list of processes starting from least dependent to most dependent;Use dependency tree structure listed in Monitoring xml;")
                .AppendLine("Step 2: The processes with issues are:");
            foreach (var process in nonWorkingProcessesLocal)
            {
                sbPlan.Append(process.Name).Append(",");
            }
            sbPlan.AppendLine("Step 3: From ordered list of dependencies only take those processes that have issue and add them to restart list;")
                .AppendLine("Step 4: One by one start restaring each process from the restart list.");
           

            // Restart Plan:

            // Step 1: Write an algorithm that converts xml of processes and dependencies into a list of processes starting from least dependent to most dependent.            

            var orderedListOfProcesses = CreateOrderedProcessList();

            // Step 2: From ordered list of dependencies take those processes that have issue and add them to restart list           

            // Create a list of non-working processes and their dependencies
            var nonWorkingProcessesAndDependencies = new List<Process>();
            nonWorkingProcessesAndDependencies.AddRange(nonWorkingProcessesLocal);
            foreach (var nonWorkingProcess in nonWorkingProcessesLocal)
            {
                foreach (var process in from process in orderedListOfProcesses
                                        from dependency in process.Dependencies
                                        where dependency.Name == nonWorkingProcess.Name
                                        where !nonWorkingProcessesAndDependencies.Contains(process)
                                        select process)
                {
                    nonWorkingProcessesAndDependencies.Add(process);
                }
            }

            // Select nonWorkingProcessesAndDependencies from ordered list of processes to get ordered list of processes to restart
            var listOfProcessesToRestart = (from process in orderedListOfProcesses
                                            from nonWorkingProcess in nonWorkingProcessesAndDependencies
                                            where process.Name == nonWorkingProcess.Name
                                            select process).ToList();

            // Step 3: Restart the processes that are in restart list
            Console.WriteLine("Restarting each process from the list");            
       
            foreach (var process in listOfProcessesToRestart)
            {
                RestartProcess(process);
            }

            // Step 4: Stop Restart plan Execution
            numberOfRestartAttempts = 0;

            TerminateThread();
        }

        /// <summary>
        /// Creates a list of processes in the order of least dependant to most dependant process.
        /// </summary>
        /// <returns></returns>
        private IEnumerable<Process> CreateOrderedProcessList()
        {
            // ALGORITHM:
            //  1. Initialize orderedlist of processes with processess from monitoring xml
            //  2. Loop through the initial orderedlist of processes
            //  3. for each given process loop through its dependencies
            //  4. for each dependency check if position of dependency element in the ordered list is greater than process element in the ordered list.
            //  5. If condition in step 4 above is true then swap dependency element with position element in the ordered list. This will make sure that dependencies always are placed earlier in the list.

            // write each process from monitoredprocesses list to an ordered list of processes. The ordered list contains processes in the order in which they should be restarted
            var orderedListOfProcesses = currentListOfAvailableProcesses.ToList();

            // Loop through initial available list of processes
            foreach (var orderedProcess in orderedListOfProcesses.ToArray())
            {
                var process = orderedProcess;
                // For each process in the list loop through dependencies
                foreach (var dependency in process.Dependencies)
                {
                    // For each dependency check 
                    // If Dependency.Position>Process.Position
                    // swap(Dependency,Process)

                    // Find position of dependency element in the list
                    var dependencyElement = dependency;
                    var dependencyElementPosition = orderedListOfProcesses.FindIndex(p => p.Name == dependencyElement.Name);

                    // Find position of process in the list
                    var processElementPosition = orderedListOfProcesses.FindIndex(p => p.Name == process.Name);

                    if (dependencyElementPosition > processElementPosition)
                    {
                        // swap(DependencyElement,ProcessElement)
                        var temp = dependencyElement;
                        orderedListOfProcesses[dependencyElementPosition] = process;
                        orderedListOfProcesses[processElementPosition] = currentListOfAvailableProcesses.Find(p => p.Name == temp.Name);
                    }
                }
            }
            return orderedListOfProcesses;
        }

        /// <summary>
        /// Steps to restart each process
        /// </summary>
        /// <param name="process"></param>
        private void RestartProcess(Process process)
        {
            // Wait time is configured in xml for each process. Each process can have different time to restart successfully. Default time is 30 seconds
            var restartTimeout = process.RestartTimeout != 0 ? process.RestartTimeout : defaultRestartTimeout;

            Console.WriteLine(string.Format("Stopping the process:{0}", process.Name));
           
            var stopFailed = true;
            
            // Stop the process
            var returnStopCode = processManager.StopProcess(process);
            
            // Wait until process is stopped successfully
            var lastStopDateTime = DateTime.Now;
            while (DateTime.Now.Subtract(lastStopDateTime).TotalSeconds < restartTimeout)
            {
                if(processManager.IsProcessStopped(process))
                {
                    Console.WriteLine(string.Format("{0} {1}:Stopped.", process.Name, "STATE"));                  
                    stopFailed = false;                    
                    break;
                }
                Thread.Sleep(2000);
            }

            if (stopFailed)
            {
                Console.WriteLine(string.Format("Failed to stopped the process:{0}", process.Name));                
                // Fix for CR 29838: when ConnexCS became unkillable have to make upto 3 restart attempts and then reboot if needed.
                if (process.Type == "Application")
                {
                    if (numberOfRestartAttempts < defaultNumberOfRestartAttempts)
                    {
                        numberOfRestartAttempts++;
                        RestartProcess(process);
                    }
                    else
                    {
                        var nonWorkingProcesses = new List<Process> { process };
                        ExecuteRebootPlan(nonWorkingProcesses);
                    }
                }
                // Abort the thread if restartFailed
                TerminateThread();
            }

            Console.WriteLine(string.Format("Starting the process:{0}", process.Name));            

            var startFailed = true;
            
            // If process was Stopped successfully then Start the process
            var returnStartCode = processManager.StartProcess(process);
            
            // Wait until process is started successfully
            var lastStartDateTime = DateTime.Now;
            while (DateTime.Now.Subtract(lastStartDateTime).TotalSeconds < restartTimeout)
            {
                if (processManager.IsProcessRunning(process))
                {
                    Console.WriteLine(string.Format("{0} {1}:Running.", process.Name, "STATE"));                  
                    startFailed = false;                    
                    break;
                }
                Thread.Sleep(2000);
            }
            if (startFailed)
            {
                Console.WriteLine(string.Format("Failed to start the process:{0}", process.Name));               

                // Abort the thread if restartFailed
                TerminateThread();
            }
        }

        /// <summary>
        /// Executes a Reboot plan.This is called when restart plan fails.
        /// </summary>
        private void ExecuteRebootPlan(List<Process> nonWorkingProcesses)
        {
            try
            {
                // Reboot Plan
                // If Restart Plan fails 3 times in a row then reboot the machine

                // Log the steps of the plan that we are about to execute
               
                // Initially set the IsValid flag in reboot file to "1".This means reboot file check is still valid and reboot hasn't fixed the problem yet.
                UpdateRebootIsValidFlag("1");

                stopRunning = true;

                if (Environment.UserInteractive)
                {
                    var sbMessage = new StringBuilder();
                    sbMessage.Append(Resources.SystemEncounteredErrorsInProcessMonitor_SUM).AppendLine().AppendLine().Append(Resources.FollowingApplicationsErrorsInProcessMonitor_SUM).Append(":");
                    foreach (var process in nonWorkingProcesses)
                    {
                        sbMessage.AppendLine().Append(process.Name);
                    }
                    notificationBox.Show(sbMessage.ToString(), Resources.ForcedRestartInProcessMonitor_TC, 30, false);
                }

                var processReturnCode = processManager.RebootComputer();
                if (processReturnCode == -1)
                {
                    Console.WriteLine("Failed to reboot the system");                   
                    if (Environment.UserInteractive)
                    {
                        notificationBox.Show(Resources.FailedToRebootInProcessMonitor_SUM , Resources.Error_TC);
                    }
                }
            }
            catch (Exception)
            {               
            }

            TerminateThread();
        }

        /// <summary>
        /// Execute a final plan(i.e show a message window explaining why the system cannot be restored to working state).This is called when reboot plan fails.
        /// </summary>
        private void ExecuteFinalPlan()
        {           
            // stopRunning flag is set to true to stop the main thread from running
            stopRunning = true;
            if(Environment.UserInteractive)
                notificationBox.Show(Resources.FinalPlanTextInProcessMonitor_SUM, Resources.Error_TC);
           
            TerminateThread();
        }

        /// <summary>
        /// Aborts the worker thread
        /// </summary>
        private void TerminateThread()
        {
            try
            {
                // Set the state of wait handle to signaled
                waitHandle.Set();
                // Reset list of failed processes
                failedProcesses.Clear();
                // Abort thread
                Thread.CurrentThread.Abort();
            }
            catch (ThreadAbortException abortException)
            {
                Console.WriteLine((string)abortException.ExceptionState);
            }
        }

        /// <summary>
        /// Deserialize Monitoring.xml
        /// </summary>
        /// <returns>List of processes</returns>
        private static IEnumerable<Process> ReadProcessConfiguration()
        {
            var processes = new List<Process>();
            var currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (currentDirectory != null)
            {
                var xmlpath = Path.Combine(currentDirectory, "MonitoredProcesses.xml");
                var processXml = new StreamReader(xmlpath);
                var xSerializer = new XmlSerializer(typeof(List<Process>), new XmlRootAttribute("Processes"));
                processes = (List<Process>)xSerializer.Deserialize(processXml);
                processXml.Close();
            }
            return processes;
        }

        private static bool IncludeDemoProcess(Process process)
        {
           var demoMode = bool.Parse(System.Configuration.ConfigurationSettings.AppSettings["DemoMode"] ?? "false");
            
            // If process has Mode=Demo attribute and is set to true then only that process is to be run
           if(process.Mode==null)
           {
               return true;
           }
           else if(process.Mode=="Demo" && demoMode)
           {
               return true;
           }
            return false;
        }

        /// <summary>
        /// Looks at the list of processes in the Monitoring xml and returns the processes that exist on the machine.
        /// Also if the process monitor is running in the demo mode then adds demo applications to available process list
        /// </summary>
        /// <param name="processes">List of all processes in the Monitoring xml</param>
        /// <param name="logger">log writer instance</param>
        /// <returns>List of all available processes</returns>
        private List<Process> AvailableProcesses(IEnumerable<Process> processes)
        {
            return processes.Where(IncludeDemoProcess).Where(process => process.Exists(currentListOfUnAvailableProcesses)).ToList();
        }

        /// <summary>
        /// Reset Reboot file with IsValid=0
        /// </summary>
        private void ResetRebootFile()
        {
            try
            {
                var rebootFilePath = Path.Combine(GetCurrentExecutingDirectory(), processMonitorRebootFileName);
                // Check if reboot file exists.
                if (File.Exists(rebootFilePath))
                {
                    // If file exists read contents of the file and check if IsValid=1. If IsValid=1 then reset it to 0.
                    var content = File.ReadAllText(rebootFilePath);
                    // Parse the pipe delimited text
                    var substrings = content.Split('|');
                    if (substrings[1] != null)
                    {
                        var isValidSubstrings = substrings[1].Split('=');
                        if (isValidSubstrings[0] == "IsValid" && isValidSubstrings[1] == "1")
                        {
                            UpdateRebootIsValidFlag("0");
                        }
                    }
                }
            }
            catch (Exception)
            {               
            }
        }

        /// <summary>
        /// Updates IsValid flag in Reboot file
        /// </summary>
        /// <param name="isValid"></param>
        private void UpdateRebootIsValidFlag(string isValid)
        {
            try
            {
                var rebootFilePath = Path.Combine(GetCurrentExecutingDirectory(), processMonitorRebootFileName);
                var content = new StringBuilder();
                content.Append("Timestamp=").Append(DateTime.Now.ToString()).Append("|").Append("IsValid=").Append(isValid);
                File.WriteAllText(rebootFilePath, content.ToString());
            }
            catch (Exception)
            {                
            }
        }

        /// <summary>
        /// Gets IsValid flag from Reboot file. IsValid flag indicates if reboot of the computer fixed the issue
        /// </summary>
        /// <returns></returns>
        private bool GetRebootIsValidFlag()
        {
            try
            {
                var rebootFilePath = Path.Combine(GetCurrentExecutingDirectory(), processMonitorRebootFileName);
                if (File.Exists(rebootFilePath))
                {
                    // If file exists read contents of the file and return boolean for IsValid
                    var content = File.ReadAllText(rebootFilePath);
                    // Parse the pipe delimited text
                    var substrings = content.Split('|');
                    if (substrings[1] != null)
                    {
                        var isValidSubstrings = substrings[1].Split('=');
                        if (isValidSubstrings[0].Trim() == "IsValid" && isValidSubstrings[1].Trim() == "1")
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (Exception)
            {               
                return false;
            }
        }

        /// <summary>
        /// Returns the current path of the assembly
        /// </summary>
        /// <returns></returns>
        private static string GetCurrentExecutingDirectory()
        {
            var filePath = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            return Path.GetDirectoryName(filePath);
        } 

        /// <summary>
        /// Method used to dispose of the resources being utilized by this class.
        /// </summary>
        /// <param name="disposing">A flag to determine whether the request to dispose was explicit or not.</param>
        protected void Disposed(bool disposing)
        {
            if (disposing)
            {
                stopRunning = true;
                processManager = null;               
                workerThread = null;
                if (failedProcesses.Count > 0)
                    failedProcesses.Clear();
                startupPlanProgressUIDispatcher = null;               
                waitHandle.Close();
            }
        }
    }
}