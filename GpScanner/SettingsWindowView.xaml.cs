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
}