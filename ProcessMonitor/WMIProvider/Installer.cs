using System.ComponentModel;
using System.Management.Instrumentation;


// Security Descriptor string that allows non-admin users to publish WMI events
[assembly: Instrumented(@"root\AmbreCorp", "O:BAG:BAD:(A;;0x1;;;S-1-1-0)")]

///<summary>
///</summary>
[RunInstaller(true)]
public class EventInstaller : DefaultManagementProjectInstaller { }
