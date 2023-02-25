using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
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
                    droppedData.Resim.GeneratePdf(null, Format.Jpg, TwainCtrl.SelectedPaper).Save(temporarypdf);
                    string[] pdffiles = (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) ? new string[] { pdfFilePath, temporarypdf } : new string[] { temporarypdf, pdfFilePath };
                    pdffiles.MergePdf().Save(pdfFilePath);
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

        private void ContentControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is GpScannerViewModel gpScannerViewModel && e.LeftButton is MouseButtonState.Pressed && e.MouseDevice.DirectlyOver is Image image)
            {
                string filepath = image.DataContext.ToString();
                if (gpScannerViewModel.OpenOriginalFile.CanExecute(filepath))
                {
                    gpScannerViewModel.OpenOriginalFile.Execute(filepath);
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
                    ViewModel.ReloadFileDatas();
                }

                if (e.PropertyName is "DetectPageSeperator" && ViewModel.DetectBarCode)
                {
                    ViewModel.BarcodeContent = GpScannerViewModel.GetImageBarcodeResult(TwainCtrl?.Scanner?.Resimler?.LastOrDefault()?.Resim)?.Text;
                    ViewModel.AddBarcodeToList();

                    if (ViewModel.DetectPageSeperator && ViewModel.BarcodeContent is not null)
                    {
                        TwainCtrl.Scanner.FileName = ViewModel.GetPatchCodeResult(ViewModel.BarcodeContent);
                    }
                }

                if (e.PropertyName is "DataBaseTextData" && TwainCtrl.DataBaseTextData is not null)
                {
                    ViewModel.ScannedText = TwainCtrl.DataBaseTextData;
                    if (ViewModel.ScannedText != null)
                    {
                        ViewModel.ScannerData.Data.Add(new Data() { Id = DataSerialize.RandomNumber(), FileName = TwainCtrl.Scanner.PdfFilePath, FileContent = string.Join(" ", ViewModel.ScannedText.Select(z => z.Text)), QrData = ViewModel.GetImageBarcodeResult(TwainCtrl?.DataBaseQrData)?.Text });
                    }
                    ViewModel.DatabaseSave.Execute(null);
                    TwainCtrl.DataBaseTextData = null;
                    ViewModel.ScannedText = null;
                }

                if (e.PropertyName is "ImgData" && TwainCtrl.ImgData is not null)
                {
                    ViewModel.BarcodeContent = ViewModel.GetImageBarcodeResult(TwainCtrl?.ImgData)?.Text;
                    ViewModel.AddBarcodeToList();
                    ViewModel.OcrIsBusy = true;
                    ViewModel.ScannedText = await TwainCtrl.ImgData.OcrAsyc(Settings.Default.DefaultTtsLang);
                    if (ViewModel.ScannedText != null)
                    {
                        ViewModel.TranslateViewModel.Metin = string.Join(" ", ViewModel.ScannedText.Select(z => z.Text));
                        ViewModel.TranslateViewModel.TaramaGeçmiş.Add(ViewModel.TranslateViewModel.Metin);
                        ViewModel.OcrIsBusy = false;
                    }
                    TwainCtrl.ImgData = null;
                }

                if (e.PropertyName is "DragMoveStarted")
                {
                    ViewModel.ListBoxBorderAnimation = TwainCtrl.DragMoveStarted;
                }

                if (e.PropertyName is "CameraQRCodeData" && TwainCtrl.CameraQRCodeData is not null)
                {
                    ViewModel.BarcodeContent = ViewModel.GetImageBarcodeResult(TwainCtrl.CameraQRCodeData)?.Text;
                    if (!string.IsNullOrWhiteSpace(ViewModel.BarcodeContent))
                    {
                        ViewModel.AddBarcodeToList();
                        TwainCtrl.CameraQRCodeData = null;
                    }
                }
                if (e.PropertyName is "UsePageSeperator" && TwainCtrl.Scanner.UsePageSeperator)
                {
                    if (Settings.Default.PatchCodes.Count <= 0)
                    {
                        TwainCtrl.Scanner.UsePageSeperator = false;
                        _ = MessageBox.Show(Translation.GetResStringValue("NOPATCHCODE"));
                        if (WindowExtensions.OpenSettings.CanExecute(null))
                        {
                            WindowExtensions.OpenSettings.Execute(null);
                        }
                        return;
                    }
                    ViewModel.DetectPageSeperator = TwainCtrl.Scanner.UsePageSeperator;
                }
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (TwainCtrl.Filesavetask?.IsCompleted == false || (DataContext as GpScannerViewModel)?.Filesavetask?.IsCompleted == false)
            {
                _ = MessageBox.Show(Translation.GetResStringValue("TASKSRUNNING"));
                e.Cancel = true;
            }
            GpScannerViewModel.BackupDataXmlFile();
            StillImageHelper.KillServer();
        }
    }
}