using GpScanner.ViewModel;
using System.Windows;

namespace GpScanner
{
    /// <summary>
    /// Interaction logic for SplashWindow.xaml
    /// </summary>
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
            DataContext = new SplashViewModel();
        }
    }
}
