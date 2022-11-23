﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using GpScanner.ViewModel;

namespace GpScanner
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            foreach (string arg in e.Args)
            {
                if (arg.StartsWith(StillImageHelper.DEVICE_PREFIX, StringComparison.InvariantCultureIgnoreCase))
                {
                    IEnumerable<Process> processes = StillImageHelper.GetAllGPScannerProcess();
                    if (processes.Count() == 0)
                    {
                        StillImageHelper.FirstLanuchScan = true;
                        return;
                    }
                    if (processes.Count() > 0)
                    {
                        StillImageHelper.FirstLanuchScan = false;
                        foreach (Process process in processes)
                        {
                            StillImageHelper.ActivateProcess(process);
                            if (StillImageHelper.SendMessage(process, StillImageHelper.DEVICE_PREFIX))
                            {
                                Environment.Exit(0);
                            }
                        }
                    }
                }
            }
        }
    }
}