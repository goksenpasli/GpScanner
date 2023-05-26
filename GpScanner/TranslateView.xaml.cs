using GpScanner.ViewModel;
using System.Windows.Controls;

namespace GpScanner;

/// <summary>
///     Interaction logic for TranslateView.xaml
/// </summary>
public partial class TranslateView : UserControl
{
    public TranslateView()
    {
        InitializeComponent();
        DataContext = new TranslateViewModel();
    }
}