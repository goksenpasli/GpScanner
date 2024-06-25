using System.Windows;

namespace GpScanner;

/// <summary>
/// Interaction logic for SettingsWindowView.xaml
/// </summary>
public partial class SettingsWindowView : Window
{
    public SettingsWindowView() { InitializeComponent(); }

    private void ListBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) => e.Handled = true;
}