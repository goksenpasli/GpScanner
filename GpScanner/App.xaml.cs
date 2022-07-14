using System.Threading;
using System.Windows;

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
                _ = MessageBox.Show("Uygulama Zaten Çalışıyor.", "Tarayıcı", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Current.Shutdown();
            }
        }
    }
}