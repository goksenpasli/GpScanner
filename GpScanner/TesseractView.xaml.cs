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
        if (windowService?.GetFirstWindow() is MainWindow mainWindow)
        {
            ITwainService twainService = new TwainService(mainWindow?.TwainCtrl);
            DataContext = new TesseractViewModel(windowService, twainService);
        }
    }
}