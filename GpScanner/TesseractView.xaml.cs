using GpScanner.ViewModel;
using System.Windows.Controls;
using System.Windows.Data;

namespace GpScanner;

/// <summary>
/// Interaction logic for TesseractView.xaml
/// </summary>
public partial class TesseractView : UserControl
{
    public static CollectionViewSource cvs;

    public TesseractView()
    {
        InitializeComponent();
        cvs = TryFindResource("Files") as CollectionViewSource;
        IWindowService windowService = new WindowService();
        if (windowService?.GetActiveWindow() is MainWindow mainWindow)
        {
            DataContext = new TesseractViewModel(windowService, mainWindow?.TwainCtrl);
        }
    }
}