using GpScanner.ViewModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

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
                IEnumerable<Process> processes = StillImageHelper.GetAllGPScannerProcess();
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

    private void Current_DispatcherUnhandledException(DispatcherUnhandledExceptionEventArgs e)
    {
        _ = MessageBox.Show(e.Exception.Message);
        e.Handled = true;
    }
}