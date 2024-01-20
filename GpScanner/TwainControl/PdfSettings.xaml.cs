using System.Windows;
using System.Windows.Controls;

namespace TwainControl;

/// <summary>
/// Interaction logic for PdfSettings.xaml
/// </summary>
public partial class PdfSettings : UserControl
{
    public PdfSettings() { InitializeComponent(); }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is TwainCtrl twainCtrl && sender is PasswordBox passwordBox)
        {
            twainCtrl.Scanner.PdfPassword = passwordBox.Password;
        }
    }
}