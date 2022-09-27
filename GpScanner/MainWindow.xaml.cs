using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using GpScanner.ViewModel;
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
            GpScannerViewModel ViewModel = DataContext as GpScannerViewModel;
            if (e.PropertyName is "Resimler")
            {
                ViewModel.Dosyalar = ViewModel.GetScannerFileData();
                ViewModel.ChartData = ViewModel.GetChartsData();
            }

            if (e.PropertyName is "DetectPageSeperator")
            {
                if (ViewModel.DetectBarCode)
                {
                    ViewModel.BarcodeContent = ViewModel.GetImageBarcodeResult(TwainCtrl.Scanner.Resimler.LastOrDefault().Resim.BitmapSourceToBitmap());
                }
                if (ViewModel.DetectBarCode && ViewModel.DetectPageSeperator)
                {
                    TwainCtrl.Scanner.FileName = ViewModel.GetPatchCodeResult(ViewModel.BarcodeContent);
                }
            }

            if (e.PropertyName is "ImgData" && TwainCtrl.ImgData is not null)
            {
                ViewModel.BarcodeContent = ViewModel.GetImageBarcodeResult(TwainCtrl.ImgData);
                _ = ViewModel.Ocr(TwainCtrl.ImgData);
                TwainCtrl.ImgData = null;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (TwainCtrl.pdfsavetask?.IsCompleted == false)
            {
                e.Cancel = true;
            }
        }
    }
}