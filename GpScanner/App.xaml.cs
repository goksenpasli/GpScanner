using GpScanner.ViewModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace GpScanner;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
#if !DEBUG
        Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
#endif
        foreach(string arg in e.Args)
        {
            if(arg.StartsWith(StillImageHelper.DEVICE_PREFIX, StringComparison.InvariantCultureIgnoreCase))
            {
                List<Process> processes = StillImageHelper.GetAllGPScannerProcess().ToList();
                if(!processes.Any())
                {
                    StillImageHelper.FirstLanuchScan = true;
                    return;
                }

                if(processes.Any())
                {
                    StillImageHelper.FirstLanuchScan = false;
                    foreach(Process process in processes)
                    {
                        StillImageHelper.ActivateProcess(process);
                        if(StillImageHelper.SendMessage(process, StillImageHelper.DEVICE_PREFIX))
                        {
                            Environment.Exit(0);
                        }
                    }
                }
            }
        }
    }
}