using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace WMIProvider
{
    ///<summary>
    ///</summary>
    public class Process
    {
        /// <summary>
        /// Type of Process (Service or Application)
        /// </summary>
        [XmlAttribute]
        public string Type { get; set; }

        /// <summary>
        /// Name of Process
        /// </summary>
        [XmlAttribute]
        public string Name { get; set; }

        /// <summary>
        /// Service Name
        /// </summary>
        [XmlAttribute]
        public string ServiceName { get; set; }

        /// <summary>
        /// Start command of Process
        /// </summary>
        [XmlAttribute]
        public string Command { get; set; }

        /// <summary>
        /// Directory Path of Process
        /// </summary>
        [XmlAttribute]
        public string DirPath { get; set; }

        /// <summary>
        /// Process is sending heart beat if set to true
        /// </summary>
        [XmlAttribute]
        public bool HasHeartBeat { get; set; }

        /// <summary>
        /// Maximum time to wait for a heartbeat
        /// </summary>
        [XmlAttribute]
        public int HeartbeatTimeout { get; set; }

        /// <summary>
        /// If DemoMode is set to true for a process then its only for demo purposes otherwise set to false
        /// </summary>
        [XmlAttribute]
        public string Mode { get; set; }

        /// <summary>
        /// Maximum time to wait for the process to restart
        /// </summary>
        [XmlAttribute]
        public int RestartTimeout { get; set; }

        /// <summary>
        /// Dependencies for each process
        /// </summary>
        [XmlElementAttribute("Dependency", Form = System.Xml.Schema.XmlSchemaForm.Unqualified)]
        public List<Dependency> Dependencies { get; set; }

        /// <summary>
        /// State of the process. Happy,Dead or Hung
        /// </summary>
        public State CurrentState { get; set; }

    }

    ///<summary>
    ///</summary>
    public class Dependency
    {
        /// <summary>
        /// Name of Dependent Process
        /// </summary>
        [XmlAttribute]
        public string Name { get; set; }
    }

    /// <summary>
    /// Static class with extension methods for Process
    /// </summary>
    public static class ProcessHelper
    {
        ///<summary>
        /// Checks if the process exists on the computer.
        ///</summary>
        ///<param name="process">process</param>
        ///<param name="logWriter">log writer instance</param>
        ///<param name="currentListOfUnAvailableProcesses">currentListOfUnAvailableProcesses</param>
        ///<returns></returns>
        public static bool Exists(this Process process, List<Process> currentListOfUnAvailableProcesses)
        {
            if (process.Type.Equals("Service"))
            {
                // Check if the processtype=service exists or is disabled.If service doesnot exist or is disabled then donot attempt restart. This is possible in client-server scenarios where some services may not exist on server machines or disabled on purpose
                var serviceManager = new ServiceManager();
                var serviceState = serviceManager.GetServiceState(process);
               
                if (serviceState == ServiceState.NotFound)
                {
                    // Log the serviceState
                    // If the process already exists in currentListOfUnAvailableProcesses then don't log message for it.
                    if (currentListOfUnAvailableProcesses == null || !currentListOfUnAvailableProcesses.Contains(process))
                    {
                        // log
                    }

                    return false;
                }
                else
                {
                    var serviceStartMode = serviceManager.GetServiceStartMode(process);
                    if (serviceStartMode == ServiceStartMode.Disabled)
                    {
                        // Log the serviceStartMode
                        // If the process already exists in currentListOfUnAvailableProcesses then don't log message for it.
                        if (currentListOfUnAvailableProcesses == null || !currentListOfUnAvailableProcesses.Contains(process))
                        {
                            // log
                        }

                        return false;
                    }
                }
                return true;
            }
            else
            {
                // Check if directory path for applications is correct
                if (!Directory.Exists(Path.GetFullPath(process.DirPath)))
                {
                    // Log the error message
                    // If the process already exists in currentListOfUnAvailableProcesses then don't log message for it.
                    if (currentListOfUnAvailableProcesses == null || !currentListOfUnAvailableProcesses.Contains(process))
                    {
                        // log
                    }

                    return false;
                }
                return true;
            }
        }
    }

    ///<summary>
    /// 
    ///</summary>
    public enum State
    {
        /// <summary>
        /// Process is running
        /// </summary>
        IsHappy,

        /// <summary>
        /// If process doesnot have heartbeat and is not in system services then the process is dead
        /// </summary>
        IsDead,

        /// <summary>
        /// If process doesnot have heartbeat but is in system services then the process is hung
        /// </summary>
        IsHung
    }
}