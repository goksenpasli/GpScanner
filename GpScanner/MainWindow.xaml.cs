using GpScanner.ViewModel;
using System;
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

        private void MW_ContentRendered(object sender, EventArgs e)
        {
            if (StillImageHelper.ShouldScan)
            {
                TwainCtrl.ScanImage.Execute(null);
            }
        }

        private void TwainCtrl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "Tarandı")
            {
                GpScannerViewModel gpScannerViewModel = DataContext as GpScannerViewModel;
                gpScannerViewModel.Dosyalar = gpScannerViewModel.GetScannerFileData();
                gpScannerViewModel.ChartData = gpScannerViewModel.GetChartsData();
            }

            if (e.PropertyName is "ImgData")
            {
                (DataContext as GpScannerViewModel)?.Ocr(TwainCtrl.ImgData);
                TwainCtrl.ImgData = null;
            }
        }
    }
}