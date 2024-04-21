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
        DataContext = new TesseractViewModel();
    }
}