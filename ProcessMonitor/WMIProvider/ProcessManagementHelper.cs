using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace WMIProvider
{
    // Helper methods for Application management(Processes that are of not Windows Service type i.e .exe)
    internal class ProcessManagementHelper
    {
        /// <summary>
        /// Method to Start a process
        /// </summary>
        /// <param name="machineName"></param>
        /// <param name="dirPath"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        public static ProcessReturnCode StartProcess(string machineName, string dirPath, string command)
        {
            using (var processTask = new ManagementClass(@"\\" + machineName + @"\root\CIMV2", "Win32_Process", null))
            {
                var methodParams = processTask.GetMethodParameters("Create");
                methodParams["CurrentDirectory"] = System.IO.Path.GetFullPath(dirPath);
                methodParams["CommandLine"] = System.IO.Path.GetFullPath(dirPath) + "\\" + command;
                var outParams = processTask.InvokeMethod("Create", methodParams, null);
                if (outParams != null)
                    return(ProcessReturnCode) Enum.Parse(typeof (ProcessReturnCode), outParams["ReturnValue"].ToString());
                return ProcessReturnCode.UnknownFailure;
            }
        }

        /// <summary>
        /// Method to end a process
        /// </summary>
        /// <param name="connectionScope"></param>
        /// <param name="processName"></param>
        /// <returns></returns>
        public static ProcessReturnCode KillProcess(ManagementScope connectionScope, string processName)
        {
            var wmiProcessQuery = new SelectQuery("SELECT * FROM Win32_Process Where Name = '" + processName + "'");
            using (var wmiObjectSearcher = new ManagementObjectSearcher(connectionScope, wmiProcessQuery))
            {
                foreach (ManagementObject managementObject in wmiObjectSearcher.Get())
                {
                    // adding a ReturnValue on the terminate method requires to use ManagementBaseObject
                    ManagementBaseObject inputParams = managementObject.GetMethodParameters("Terminate");
                    ManagementBaseObject outputParams = managementObject.InvokeMethod("Terminate", inputParams, null);
                    if (outputParams != null)
                        return (ProcessReturnCode)Enum.Parse(typeof (ProcessReturnCode), outputParams["ReturnValue"].ToString());
                }
                return ProcessReturnCode.UnknownFailure;
            }
        }
        
        /// <summary>
        /// Returns if process is currently running or not
        /// </summary>
        /// <param name="connectionScope"></param>
        /// <param name="processName"></param>
        /// <returns></returns>
        public static bool IsProcessRunning(ManagementScope connectionScope,string processName)
        {
            var wmiProcessQuery = new SelectQuery("SELECT * FROM Win32_Process Where Name = '" + processName + "'");
            using (var wmiObjectSearcher = new ManagementObjectSearcher(connectionScope, wmiProcessQuery))
            {
                return wmiObjectSearcher.Get().Cast<ManagementObject>().Any(process => process["Name"] != null && process["Name"].ToString() == processName);
            }
        }

        /// <summary>
        /// Returns a list of properties for each process
        /// </summary>
        /// <param name="connectionScope"></param>
        /// <param name="processName"></param>
        /// <returns></returns>
        public static List<string> ProcessProperties(ManagementScope connectionScope, string processName)
        {
            var processProperties = new List<string>();
            var wmiProcessQuery = new SelectQuery("SELECT * FROM Win32_Process Where Name = '" + processName + "'");
            var wmiObjectSearcher = new ManagementObjectSearcher(connectionScope, wmiProcessQuery);

            foreach (ManagementObject process in wmiObjectSearcher.Get())
            {
                // get list of required properties of the given Process. There are many more properties on a windows process but they are not needed right now.
             
                if (process["CommandLine"]!=null)
                    processProperties.Add("CommandLine; " + process["CommandLine"]);

                if (process["Description"] != null)
                    processProperties.Add("Description: " + process["Description"]);

                if (process["WorkingSetSize"] != null)
                    processProperties.Add("Memory Usage: " + TranslateMemoryUsage(process["WorkingSetSize"].ToString()));

                if (process["Name"] != null)
                    processProperties.Add("Name: " + process["Name"]);

                if (process["ProcessId"] != null)
                    processProperties.Add("ProcessId: " + process["ProcessId"]);
            }
            return processProperties;
        }

        /// <summary>
        /// Translates the memory usage from bytes to KB
        /// </summary>
        /// <param name="workingSet"></param>
        /// <returns></returns>
        private static string TranslateMemoryUsage(string workingSet)
        {
            var calc = Convert.ToInt32(workingSet);
            calc = calc/1024;
            return calc.ToString();
        }
    }
}