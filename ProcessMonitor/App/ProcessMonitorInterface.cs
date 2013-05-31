using System;
using System.IO;
using System.Runtime.InteropServices;
using WMIProvider;

namespace ProcessMonitor
{
    public class ProcessMonitorInterface
    {
        /// <summary>
        /// Shows a Window
        /// </summary>
        /// <param name="hWnd">Handle to the window.</param>
        /// <param name="nCmdShow">Specifies how the window is to be shown. </param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        /// <summary>
        /// Used to restore a window to its original size.
        /// </summary>
        private const int SW_RESTORE = 9;

        /// <summary>
        /// Used to hide a window.
        /// </summary>
        private const int SW_HIDE = 0;

        /// <summary>
        /// ProcessMonitorService instance
        /// </summary>
        private ProcessMonitorService processMonitorService;
        
        /// <summary>
        /// Handles exit event
        /// </summary>
        /// <param name="ctrlType">An enumerated type for the control messages</param>
        /// <returns></returns>
        public bool ApplicationExitEvent(CtrlTypes ctrlType)
        {
            switch (ctrlType)
            {
                case CtrlTypes.CTRL_CLOSE_EVENT:
                    Console.WriteLine("Program being closed");
                    Dispose();
                    break;

                case CtrlTypes.CTRL_LOGOFF_EVENT:
                    Console.WriteLine("System logging off");
                    Dispose();
                    break;

                case CtrlTypes.CTRL_SHUTDOWN_EVENT:
                    Console.WriteLine("System shutting down");
                    Dispose();
                    break;
            }
            return true;
        }

        /// <summary>
        /// Declare the SetConsoleCtrlHandler function as external and receiving a delegate.
        /// </summary>
        /// <param name="Handler">HandlerRoutine</param>
        /// <param name="Add">If true handler is added.If false handler is removed</param>
        /// <returns>bool</returns>
        [DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

        /// <summary>
        /// A delegate type to be used as the handler routine for SetConsoleCtrlHandler.
        /// </summary>
        /// <param name="CtrlType">control messages for exit</param>
        /// <returns>bool</returns>
        public delegate bool HandlerRoutine(CtrlTypes CtrlType);

        /// <summary>
        /// An enumerated type for the control messages sent to the handler routine.
        /// </summary>
        public enum CtrlTypes
        {
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }
        
        /// <summary>
        /// Run Process Monitor
        /// </summary>
        /// <param name="args"></param>
        public void Run(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0].Equals("-debug"))
                {
                    // show console window when arg="debug" is passed
                    var winHandle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                    ShowWindow(winHandle, SW_RESTORE);
                }
                else if (args.Length == 0)
                {
                    // hide the console window when arg is not passed
                    var winHandle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                    ShowWindow(winHandle, SW_HIDE);
                }

                // Set the console background.
                Console.BackgroundColor = ConsoleColor.White;
                // Set the foreground color.
                Console.ForegroundColor = ConsoleColor.Black;
                // Clear the console.
                Console.Clear();

                Console.WriteLine("Starting Process Monitor");
                                
                processMonitorService = new ProcessMonitorService();
                processMonitorService.Execute();
                
            }
            catch (Exception ex)
            {
                Console.WriteLine("An Error Occurred: {0}", ex);               
            }
        }

        /// <summary>
        /// ApplicationExitHandler
        /// </summary>
        /// <param name="Handler">HandlerRoutine</param>
        /// <param name="Add">If true handler is added.If false handler is removed</param>
        /// <returns></returns>
        public static  bool ApplicationExitHandler(HandlerRoutine Handler, bool Add)
        {
          return SetConsoleCtrlHandler(Handler, Add);
        }

        /// <summary>
        /// Method used to dispose of the resources when application exits
        /// </summary>
        public void Dispose()
        {
            if(processMonitorService!=null)
            {
                processMonitorService.Dispose();
                processMonitorService = null;
            }
        }
    }
}
