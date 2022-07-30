using GpScanner.ViewModel;
using System;
using System.Threading;
using System.Windows;
using TwainControl;

namespace GpScanner
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private Mutex scannermutex;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            scannermutex = new Mutex(true, "GpScannerApplication", out bool aIsNewInstance);
            if (!aIsNewInstance)
            {
                var result = MessageBox.Show(Translation.GetResStringValue("APPRUNNING"), Translation.GetResStringValue("SCANNER"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Current.Shutdown();
            }

            foreach (string arg in e.Args)
            {
                if (arg.StartsWith(StillImageHelper.DEVICE_PREFIX, StringComparison.InvariantCultureIgnoreCase))
                {
                    StillImageHelper.ShouldScan = true;
                }
            }
        }
    }
}