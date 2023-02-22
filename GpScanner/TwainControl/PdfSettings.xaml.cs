using System.Windows;
using System.Windows.Controls;

namespace TwainControl {
    /// <summary>
    /// Interaction logic for PdfSettings.xaml
    /// </summary>
    public partial class PdfSettings : UserControl {
        public PdfSettings() {
            InitializeComponent();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e) {
            if (DataContext is TwainCtrl twainCtrl) {
                twainCtrl.Scanner.PdfPassword = ((PasswordBox)sender).SecurePassword;
            }
        }
    }
}