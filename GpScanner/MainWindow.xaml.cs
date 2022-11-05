using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Extensions;
using GpScanner.Properties;
using GpScanner.ViewModel;
using Ocr;
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
            cvs = TryFindResource("Veriler") as CollectionViewSource;
            DataContext = new GpScannerViewModel();
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

        private void ContentControl_Drop(object sender, DragEventArgs e)
        {
            if (e.OriginalSource is Image image && e.Data.GetData(typeof(ScannedImage)) is ScannedImage droppedData && image.TemplatedParent is PdfViewer.PdfViewer pdfviewer)
            {
                string temporarypdf = Path.GetTempPath() + Guid.NewGuid() + ".pdf";
                string pdfFilePath = pdfviewer.PdfFilePath;
                PdfGeneration.GeneratePdf(droppedData.Resim, null, Format.Jpg, TwainCtrl.SelectedPaper).Save(temporarypdf);
                PdfGeneration.MergePdf(new string[] { temporarypdf, pdfFilePath }).Save(pdfFilePath);
                File.Delete(temporarypdf);
                pdfviewer.PdfFilePath = null;
                pdfviewer.PdfFilePath = pdfFilePath;
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
            if (Environment.GetCommandLineArgs().Length > 0)
            {
                int decodeheight = (int)(TwainCtrl.SelectedPaper.Height / TwainCtrl.Inch * TwainCtrl.ImgLoadResolution);
                TwainCtrl.AddFiles(Environment.GetCommandLineArgs(), decodeheight);
            }
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
                    Result result = GpScannerViewModel.GetImageBarcodeResult(TwainCtrl?.Scanner?.Resimler?.LastOrDefault()?.Resim);
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
                    ViewModel.BarcodeContent = ViewModel.GetImageBarcodeResult(TwainCtrl?.ImgData)?.Text;
                    AddBarcodeToList(ViewModel);
                    _ = ViewModel.GetScannedTextAsync(TwainCtrl.ImgData);
                }

                if (e.PropertyName is "ApplyDataBaseOcr" && TwainCtrl?.Scanner?.ApplyDataBaseOcr == true && TwainCtrl?.Scanner?.Resimler?.Count > 0)
                {
                    try
                    {
                        List<ObservableCollection<OcrData>> scannedtext = new();
                        if (!string.IsNullOrEmpty(Settings.Default.DefaultTtsLang))
                        {
                            foreach (ScannedImage scannedimage in TwainCtrl.Scanner.Resimler)
                            {
                                scannedtext.Add(await ViewModel.GetScannedTextAsync(scannedimage.Resim.ToTiffJpegByteArray(Format.Jpg), false));
                                ViewModel.ScannerData.Data.Add(new Data() { Id = DataSerialize.RandomNumber(), FileName = TwainCtrl.Scanner.PdfFilePath, FileContent = ViewModel.TranslateViewModel.Metin, QrData = GpScannerViewModel.GetImageBarcodeResult(scannedimage.Resim)?.Text });
                            }
                        }
                        if ((ColourSetting)TwainControl.Properties.Settings.Default.Mode == ColourSetting.BlackAndWhite)
                        {
                            PdfGeneration.GeneratePdf(TwainCtrl.Scanner.Resimler, Format.Tiff, TwainCtrl.SelectedPaper, TwainCtrl.Scanner.JpegQuality, scannedtext).Save(TwainCtrl.Scanner.PdfFilePath);
                        }
                        if ((ColourSetting)TwainControl.Properties.Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale)
                        {
                            PdfGeneration.GeneratePdf(TwainCtrl.Scanner.Resimler, Format.Jpg, TwainCtrl.SelectedPaper, TwainCtrl.Scanner.JpegQuality, scannedtext).Save(TwainCtrl.Scanner.PdfFilePath);
                        }
                        if (TwainControl.Properties.Settings.Default.ShowFile)
                        {
                            TwainCtrl.ExploreFile.Execute(TwainCtrl.Scanner.PdfFilePath);
                        }
                        ViewModel.DatabaseSave.Execute(null);
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