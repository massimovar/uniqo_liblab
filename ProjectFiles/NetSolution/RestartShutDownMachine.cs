#region Using directives
using System;
using QPlatform.Core;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using QPlatform.HMIProject;
using QPlatform.OPCUAServer;
using QPlatform.UI;
using QPlatform.NativeUI;
using QPlatform.CoreBase;
using QPlatform.NetLogic;
using System.Diagnostics;
using QPlatform.CommunicationDriver;
using QPlatform.Modbus;
#endregion

public class RestartShutDownMachine : BaseNetLogic
{
    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

    [ExportMethod]
    public void Restart()
    {
        Process process = new Process();
        if (System.Runtime.InteropServices.RuntimeInformation
                                               .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            process.StartInfo.FileName = "shutdown";
            process.StartInfo.Arguments = "-r -f -t 0";
        }
        else if (System.Runtime.InteropServices.RuntimeInformation
                                               .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
        {
            process.StartInfo.FileName = "/usr/bin/sudo";
            process.StartInfo.Arguments = "/sbin/shutdown -r now";
        }
        process.Start();
    }

    [ExportMethod]
    public void Shutdown()
    {
        Process process = new Process();
        if (System.Runtime.InteropServices.RuntimeInformation
                                               .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            process.StartInfo.FileName = "shutdown";
            process.StartInfo.Arguments = "-f -t 0";
        }
        else if (System.Runtime.InteropServices.RuntimeInformation
                                               .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
        {
            process.StartInfo.FileName = "/usr/bin/sudo";
            process.StartInfo.Arguments = "/sbin/shutdown -h now";
        }
        process.Start();
    }
}
