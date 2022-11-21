using System;
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
                    StillImageHelper.ShouldScan = true;
                }
            }
        }
    }
}