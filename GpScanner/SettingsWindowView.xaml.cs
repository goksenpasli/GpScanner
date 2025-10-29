using GpScanner.Properties;
using GpScanner.ViewModel;
using System.Windows;
using System.Windows.Input;

namespace GpScanner;

/// <summary>
/// Interaction logic for SettingsWindowView.xaml
/// </summary>
public partial class SettingsWindowView : Window
{
    public SettingsWindowView() { InitializeComponent(); }

    private void ListBox_PreviewKeyDown(object sender, KeyEventArgs e) => e.Handled = true;

    private void SharePointPasswordBox_PasswordChanged(object sender, RoutedEventArgs e) => Settings.Default.SharePointUserPassword = string.IsNullOrWhiteSpace(SharePointPasswordBox.Password) ? string.Empty : SharePointPasswordBox.Password.Encrypt();

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(Settings.Default.SharePointUserPassword))
        {
            SharePointPasswordBox.Password = Settings.Default.SharePointUserPassword.Decrypt();
        }
    }
}