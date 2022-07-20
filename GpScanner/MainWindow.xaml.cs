using GpScanner.ViewModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using TwainControl;

namespace GpScanner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static CollectionViewSource cvs;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new GpScannerViewModel();
            cvs = TryFindResource("Veriler") as CollectionViewSource;
            TwainCtrl.PropertyChanged += TwainCtrl_PropertyChanged;
        }

        private void Calendar_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.Captured is CalendarItem)
            {
                _ = Mouse.Capture(null);
            }
        }

        private void TwainCtrl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "Tarandı")
            {
                GpScannerViewModel gpScannerViewModel = DataContext as GpScannerViewModel;
                gpScannerViewModel.LoadData();
            }

            if (e.PropertyName is "ImgData")
            {
                TesseractViewModel tesseractViewModel = (DataContext as GpScannerViewModel)?.TesseractViewModel;
                tesseractViewModel.Ocr(TwainCtrl.ImgData);
                TwainCtrl.ImgData = null;
            }
        }
    }
}