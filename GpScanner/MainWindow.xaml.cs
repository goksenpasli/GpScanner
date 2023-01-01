using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Extensions;
using GpScanner.Properties;
using GpScanner.ViewModel;
using Ocr;
using TwainControl;
using ZXing;
using static Extensions.ExtensionMethods;

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
            cvs = TryFindResource("Veriler") as CollectionViewSource;
            DataContext = new GpScannerViewModel();
            TwainCtrl.PropertyChanged += TwainCtrl_PropertyChanged;
        }

        private void ContentControl_Drop(object sender, DragEventArgs e)
        {
            if (e.OriginalSource is Image image && e.Data.GetData(typeof(ScannedImage)) is ScannedImage droppedData && image.TemplatedParent is PdfViewer.PdfViewer pdfviewer)
            {
                try
                {
                    string temporarypdf = Path.GetTempPath() + Guid.NewGuid() + ".pdf";
                    string pdfFilePath = pdfviewer.PdfFilePath;
                    PdfGeneration.GeneratePdf(droppedData.Resim, null, Format.Jpg, TwainCtrl.SelectedPaper).Save(temporarypdf);
                    string[] pdffiles = (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) ? new string[] { pdfFilePath, temporarypdf } : new string[] { temporarypdf, pdfFilePath };
                    PdfGeneration.MergePdf(pdffiles).Save(pdfFilePath);
                    File.Delete(temporarypdf);
                    pdfviewer.PdfFilePath = null;
                    pdfviewer.PdfFilePath = pdfFilePath;
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message);
                }
            }
        }

        private void GridSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is GpScannerViewModel ViewModel)
            {
                ViewModel.MainWindowDocumentGuiControlLength = new(1, GridUnitType.Star);
                ViewModel.MainWindowGuiControlLength = new(3, GridUnitType.Star);
            }
        }

        private void GridSplitter_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is GpScannerViewModel ViewModel)
            {
                ViewModel.MainWindowDocumentGuiControlLength = new(0, GridUnitType.Star);
                ViewModel.MainWindowGuiControlLength = new(1, GridUnitType.Star);
            }
        }

        private void MW_ContentRendered(object sender, EventArgs e)
        {
            WindowExtensions.SystemMenu(this);

            if (Settings.Default.IsFirstRun)
            {
                WindowExtensions.OpenSettings.Execute(null);
                Settings.Default.IsFirstRun = false;
            }

            if (Environment.GetCommandLineArgs().Length > 1)
            {
                TwainCtrl.AddFiles(Environment.GetCommandLineArgs(), TwainCtrl.DecodeHeight);
                GC.Collect();
            }

            if (StillImageHelper.FirstLanuchScan)
            {
                switch (Settings.Default.ButtonScanMode)
                {
                    case 0 when TwainCtrl.ScanImage.CanExecute(null):
                        TwainCtrl.ScanImage.Execute(null);
                        break;

                    case 1 when TwainCtrl.FastScanImage.CanExecute(null):
                        TwainCtrl.FastScanImage.Execute(null);
                        break;
                }
            }

            StillImageHelper.StartServer(msg =>
            {
                if (msg.StartsWith(StillImageHelper.DEVICE_PREFIX, StringComparison.InvariantCulture))
                {
                    switch (Settings.Default.ButtonScanMode)
                    {
                        case 0 when TwainCtrl.ScanImage.CanExecute(null):
                            Dispatcher.Invoke(() => TwainCtrl.ScanImage.Execute(null));
                            break;

                        case 1 when TwainCtrl.FastScanImage.CanExecute(null):
                            Dispatcher.Invoke(() => TwainCtrl.FastScanImage.Execute(null));
                            break;
                    }
                }
            });
        }

        private void QrListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(ScannedImage)) is ScannedImage scannedImage && DataContext is GpScannerViewModel ViewModel && ViewModel.GetMultipleImageBarcodeResult(scannedImage.Resim) is Result[] barcodes)
            {
                foreach (Result barcode in barcodes)
                {
                    ViewModel.BarcodeList.Add(barcode.Text);
                }
            }
        }

        private async void TwainCtrl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (DataContext is GpScannerViewModel ViewModel)
            {
                if (e.PropertyName is "Resimler")
                {
                    GpScannerViewModel.ReloadFileDatas(ViewModel);
                }

                if (e.PropertyName is "DetectPageSeperator" && ViewModel.DetectBarCode)
                {
                    Result result = GpScannerViewModel.GetImageBarcodeResult(TwainCtrl?.Scanner?.Resimler?.LastOrDefault()?.Resim);
                    ViewModel.BarcodeContent = result?.Text;
                    ViewModel.BarcodePosition = result?.ResultPoints;
                    GpScannerViewModel.AddBarcodeToList(ViewModel);

                    if (ViewModel.DetectPageSeperator && ViewModel.BarcodeContent is not null)
                    {
                        TwainCtrl.Scanner.FileName = ViewModel.GetPatchCodeResult(ViewModel.BarcodeContent);
                    }
                }

                if (e.PropertyName is "ImgData" && TwainCtrl.ImgData is not null)
                {
                    ViewModel.BarcodeContent = ViewModel.GetImageBarcodeResult(TwainCtrl?.ImgData)?.Text;
                    GpScannerViewModel.AddBarcodeToList(ViewModel);
                    Ocr.Ocr.ocrcancellationToken = new CancellationTokenSource();
                    _ = await ViewModel.GetScannedTextAsync(TwainCtrl.ImgData);
                }

                if (e.PropertyName is "ApplyDataBaseOcr")
                {
                    try
                    {
                        if (TwainCtrl?.Scanner?.Resimler?.Count > 0 && !string.IsNullOrEmpty(Settings.Default.DefaultTtsLang))
                        {
                            Ocr.Ocr.ocrcancellationToken = new CancellationTokenSource();
                            foreach (ScannedImage scannedimage in TwainCtrl.Scanner.Resimler.ToList())
                            {
                                ObservableCollection<OcrData> ocrdata = await scannedimage.Resim.ToTiffJpegByteArray(Format.Jpg).OcrAsyc(Settings.Default.DefaultTtsLang);
                                ViewModel.ScannerData.Data.Add(new Data() { Id = DataSerialize.RandomNumber(), FileName = TwainCtrl.Scanner.PdfFilePath, FileContent = string.Join(" ", ocrdata.Select(z => z.Text)), QrData = GpScannerViewModel.GetImageBarcodeResult(scannedimage.Resim)?.Text });
                            }
                            ViewModel.DatabaseSave.Execute(null);
                        }
                    }
                    catch (Exception ex)
                    {
                        _ = MessageBox.Show(ex.Message);
                    }
                }

                if (e.PropertyName is "DragMoveStarted")
                {
                    ViewModel.ListBoxBorderAnimation = TwainCtrl.DragMoveStarted;
                }
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (TwainCtrl.Filesavetask?.IsCompleted == false || (DataContext as GpScannerViewModel)?.Filesavetask?.IsCompleted == false)
            {
                _ = MessageBox.Show("Bazı Görevler Çalışıyor Bitmesini Bekleyin.");
                e.Cancel = true;
            }
            StillImageHelper.KillServer();
        }
    }
}