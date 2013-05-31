Process Monitor
==============
Easily monitor processes on a computer and restart them if they are not in running state. If the process still doesn't restart
after three successful restarts then reboot the machine to fix the hanged process.


Features:
--------------

1. This is a .NET console application that can be used to monitor the status of any process(service or .exe) running on the computer.
2. If the process is stopped or hung then this application will also restart the process until it comes back to working state.
3. The application has XML file that allows you to define which processes you want to monitor. The XML can also define dependencies
   for a given process so that if one of the dependencies has to be restarted by the process monitor then the parent process
   will also get restarted by process monitor. This way the dependent processes get started in correct order.
4. This application can also be run as windows service by using a service installer.
