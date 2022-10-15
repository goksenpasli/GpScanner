using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Extensions;
using GpScanner.Properties;
using GpScanner.ViewModel;
using TwainControl;
using TwainWpf;
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
            DataContext = new GpScannerViewModel();
            cvs = TryFindResource("Veriler") as CollectionViewSource;
            TwainCtrl.PropertyChanged += TwainCtrl_PropertyChanged;
        }

        private static void AddBarcodeToList(GpScannerViewModel ViewModel)
        {
            if (ViewModel.BarcodeContent is not null)
            {
                ViewModel.BarcodeList.Add(ViewModel.BarcodeContent);
            }
        }

        private static void ReloadFileDatas(GpScannerViewModel ViewModel)
        {
            ViewModel.Dosyalar = ViewModel.GetScannerFileData();
            ViewModel.ChartData = ViewModel.GetChartsData();
        }

        private void Calendar_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.Captured is CalendarItem)
            {
                _ = Mouse.Capture(null);
            }
        }

        private void ContentControl_Drop(object sender, DragEventArgs e)
        {
            if (sender is ContentControl contentControl)
            {
                ScannedImage droppedData = e.Data.GetData(typeof(ScannedImage)) as ScannedImage;
                Scanner scanner = contentControl.DataContext as Scanner;
                string temporarypdf = Path.GetTempPath() + Guid.NewGuid() + ".pdf";
                PdfGeneration.GeneratePdf(droppedData.Resim, null, Format.Jpg).Save(temporarypdf);
                PdfGeneration.MergePdf(new string[] { temporarypdf, scanner.FileName }).Save(scanner.FileName);
                File.Delete(temporarypdf);
                if (DataContext is GpScannerViewModel gpScannerViewModel && gpScannerViewModel.ShowPdfPreview)
                {
                    gpScannerViewModel.ShowPdfPreview = false;
                    gpScannerViewModel.ShowPdfPreview = true;
                }
            }
        }

        private void GridSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is GpScannerViewModel ViewModel)
            {
                ViewModel.MainWindowDocumentGuiControlLength = new(1, GridUnitType.Star);
                ViewModel.MainWindowGuiControlLength = new(2, GridUnitType.Star);
            }
        }

        private void MW_ContentRendered(object sender, EventArgs e)
        {
            WindowExtensions.SystemMenu(this);
            if (StillImageHelper.ShouldScan)
            {
                TwainCtrl.ScanImage.Execute(null);
            }
        }

        private async void TwainCtrl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (DataContext is GpScannerViewModel ViewModel)
            {
                if (e.PropertyName is "Resimler")
                {
                    ReloadFileDatas(ViewModel);
                }

                if (e.PropertyName is "DetectPageSeperator" && ViewModel.DetectBarCode)
                {
                    Result result = ViewModel.GetImageBarcodeResult(TwainCtrl.Scanner.Resimler.LastOrDefault().Resim.BitmapSourceToBitmap());
                    ViewModel.BarcodeContent = result?.Text;
                    ViewModel.BarcodePosition = result?.ResultPoints;
                    AddBarcodeToList(ViewModel);

                    if (ViewModel.DetectPageSeperator && ViewModel.BarcodeContent is not null)
                    {
                        TwainCtrl.Scanner.FileName = ViewModel.GetPatchCodeResult(ViewModel.BarcodeContent);
                    }
                }

                if (e.PropertyName is "ImgData" && TwainCtrl.ImgData is not null)
                {
                    ViewModel.BarcodeContent = ViewModel.GetImageBarcodeResult(TwainCtrl.ImgData)?.Text;
                    AddBarcodeToList(ViewModel);
                    _ = ViewModel.GetScannedTextAsync(TwainCtrl.ImgData);
                    TwainCtrl.ImgData = null;
                }

                if (e.PropertyName is "ApplyOcr" && TwainCtrl.Scanner.ApplyOcr && TwainCtrl.Scanner.Resimler.Count > 0 && !string.IsNullOrEmpty(Settings.Default.DefaultTtsLang))
                {
                    try
                    {
                        ObservableCollection<OcrData> scannedtext = new();
                        foreach (ScannedImage scannedimage in TwainCtrl.Scanner.Resimler)
                        {
                            scannedtext = await ViewModel.GetScannedTextAsync(scannedimage.Resim.ToTiffJpegByteArray(Format.Jpg), false);
                            ViewModel.ScannerData.Data.Add(new Data() { Id = DataSerialize.RandomNumber(), FileName = TwainCtrl.Scanner.PdfFilePath, FileContent = ViewModel.TranslateViewModel.Metin });
                            ViewModel.DatabaseSave.Execute(null);
                        }
                        if ((ColourSetting)TwainControl.Properties.Settings.Default.Mode == ColourSetting.BlackAndWhite)
                        {
                            PdfGeneration.GeneratePdf(TwainCtrl.Scanner.Resimler, Format.Tiff, TwainCtrl.Scanner.JpegQuality, scannedtext).Save(TwainCtrl.Scanner.PdfFilePath);
                        }
                        if ((ColourSetting)TwainControl.Properties.Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale)
                        {
                            PdfGeneration.GeneratePdf(TwainCtrl.Scanner.Resimler, Format.Jpg, TwainCtrl.Scanner.JpegQuality, scannedtext).Save(TwainCtrl.Scanner.PdfFilePath);
                        }
                        if (TwainControl.Properties.Settings.Default.ShowFile)
                        {
                            TwainCtrl.ExploreFile.Execute(TwainCtrl.Scanner.PdfFilePath);
                        }
                        ReloadFileDatas(ViewModel);
                    }
                    catch (Exception ex)
                    {
                        _ = MessageBox.Show(ex.Message);
                    }
                }
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (TwainCtrl.filesavetask?.IsCompleted == false)
            {
                e.Cancel = true;
            }
        }
    }
}