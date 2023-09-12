using GpScanner.Properties;
using GpScanner.ViewModel;
using Ocr;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using TwainControl;
using static Extensions.ExtensionMethods;

namespace GpScanner;

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
        TwainCtrl.PropertyChanged += TwainCtrl_PropertyChangedAsync;
        TwainCtrl.Scanner.PropertyChanged += Scanner_PropertyChanged;
    }

    private async void ContentControl_DropAsync(object sender, DragEventArgs e)
    {
        if (e.OriginalSource is Image image && e.Data.GetData(typeof(ScannedImage)) is ScannedImage droppedData && image.TemplatedParent is PdfViewer.PdfViewer pdfviewer)
        {
            try
            {
                string temporarypdf = $"{Path.GetTempPath()}{Guid.NewGuid()}.pdf";
                string pdfFilePath = pdfviewer.PdfFilePath;
                int curpage = pdfviewer.Sayfa;
                droppedData.Resim.GeneratePdf(null, Format.Jpg, TwainCtrl.SelectedPaper).Save(temporarypdf);
                string[] processedfiles = { temporarypdf, pdfFilePath };
                if ((Keyboard.IsKeyDown(Key.LeftAlt) && Keyboard.IsKeyDown(Key.LeftShift)) || (Keyboard.IsKeyDown(Key.RightAlt) && Keyboard.IsKeyDown(Key.RightShift)))
                {
                    await TwainCtrl.RemovePdfPageAsync(pdfFilePath, curpage, curpage);
                    processedfiles.MergePdf().Save(pdfFilePath);
                    await TwainCtrl.ArrangeFileAsync(pdfFilePath, pdfFilePath, 0, curpage - 1);
                    TwainCtrl.NotifyPdfChange(pdfviewer, temporarypdf, pdfFilePath);
                    return;
                }

                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    processedfiles.MergePdf().Save(pdfFilePath);
                    await TwainCtrl.ArrangeFileAsync(pdfFilePath, pdfFilePath, 0, curpage - 1);
                    TwainCtrl.NotifyPdfChange(pdfviewer, temporarypdf, pdfFilePath);
                    return;
                }

                string[] pdffiles = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt) ? new[] { pdfFilePath, temporarypdf } : new[] { temporarypdf, pdfFilePath };
                pdffiles.MergePdf().Save(pdfFilePath);
                TwainCtrl.NotifyPdfChange(pdfviewer, temporarypdf, pdfFilePath);
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
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
            ViewModel.MainWindowDocumentGuiControlLength = new GridLength(1, GridUnitType.Star);
            ViewModel.MainWindowGuiControlLength = new GridLength(3, GridUnitType.Star);
        }
    }

    private void GridSplitter_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is GpScannerViewModel ViewModel)
        {
            ViewModel.MainWindowDocumentGuiControlLength = new GridLength(0, GridUnitType.Star);
            ViewModel.MainWindowGuiControlLength = new GridLength(1, GridUnitType.Star);
        }
    }

    private async void ListBox_DropAsync(object sender, DragEventArgs e) { await TwainCtrl.ListBoxDropFileAsync(e); }

    private void MW_ContentRendered(object sender, EventArgs e)
    {
        this.SystemMenu();
        if (DataContext is GpScannerViewModel ViewModel)
        {
            if (Settings.Default.RegisterBatchWatcher && Directory.Exists(Settings.Default.BatchFolder))
            {
                ViewModel.RegisterBatchImageFileWatcher(TwainCtrl.SelectedPaper, Settings.Default.BatchFolder);
            }

            if (ViewModel.NeedAppUpdate() && ViewModel.CheckUpdate.CanExecute(null))
            {
                ViewModel.CheckUpdate.Execute(null);
            }
        }

        if (Settings.Default.IsFirstRun)
        {
            Settings.Default.IsFirstRun = false;
            if (WindowExtensions.OpenSettings.CanExecute(null))
            {
                WindowExtensions.OpenSettings.Execute(null);
            }
        }

        string[] commandLineArgs = Environment.GetCommandLineArgs();
        if (commandLineArgs.Length > 1)
        {
            string filePath = commandLineArgs[1];
            string extension = Path.GetExtension(filePath)?.ToLower();

            if (Settings.Default.DirectOpenEypFile && extension == ".eyp" && File.Exists(filePath))
            {
                EypPdfViewer eypPdfViewer = TwainCtrl.PdfImportViewer.PdfViewer;
                eypPdfViewer.PdfFilePath = eypPdfViewer.ExtractEypFilesToPdf(filePath);
                eypPdfViewer.AddToHistoryList(eypPdfViewer.PdfFilePath);
                TwainCtrl.TbCtrl.SelectedIndex = 1;
                TwainCtrl.MaximizePdfControl.Execute(null);
                return;
            }

            if (Settings.Default.DirectOpenPdfFile && extension == ".pdf" && File.Exists(filePath))
            {
                EypPdfViewer eypPdfViewer = TwainCtrl.PdfImportViewer.PdfViewer;
                eypPdfViewer.PdfFilePath = filePath;
                eypPdfViewer.AddToHistoryList(eypPdfViewer.PdfFilePath);
                TwainCtrl.TbCtrl.SelectedIndex = 1;
                TwainCtrl.MaximizePdfControl.Execute(null);
                return;
            }

            if (Settings.Default.DirectOpenUdfFile && extension == ".udf" && File.Exists(filePath))
            {
                TwainCtrl.xpsViewer.XpsDataFilePath = TwainCtrl.LoadUdfFile(filePath);
                TwainCtrl.TbCtrl.SelectedIndex = 2;
                return;
            }

            _ = TwainCtrl.AddFiles(commandLineArgs, TwainCtrl.DecodeHeight);
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

        StillImageHelper.StartServer(
            msg =>
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
        if (e.Data.GetData(typeof(ScannedImage)) is ScannedImage scannedImage && DataContext is GpScannerViewModel ViewModel)
        {
            QrCode.QrCode qrcode = new();
            List<string> barcodes = qrcode.GetMultipleImageBarcodeResult(scannedImage.Resim);
            if (barcodes != null)
            {
                foreach (string barcode in barcodes)
                {
                    ViewModel.BarcodeList.Add(barcode);
                }
            }
        }
    }

    private void Run_PreviewMouseMove(object sender, MouseEventArgs e) { TwainCtrl.DropPreviewFile(sender, e); }

    private void Scanner_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if ((e.PropertyName is "ApplyPdfSaveOcr" && TwainCtrl?.Scanner?.ApplyPdfSaveOcr == true) || (e.PropertyName is "ApplyDataBaseOcr" && TwainCtrl?.Scanner?.ApplyDataBaseOcr == true))
        {
            if (DataContext is GpScannerViewModel ViewModel && ViewModel?.TesseractViewModel?.GetTesseractFiles(ViewModel.TesseractViewModel.Tessdatafolder)?.Count(item => item.Checked) == 0)
            {
                TwainCtrl.Scanner.ApplyPdfSaveOcr = false;
                TwainCtrl.Scanner.ApplyDataBaseOcr = false;
                _ = MessageBox.Show($"{Translation.GetResStringValue("SETTİNGS")}{Environment.NewLine}{Translation.GetResStringValue("TESSLANGSELECT")}");
            }
        }
    }

    private void StackPanel_Drop(object sender, DragEventArgs e) { TwainCtrl.DropFile(sender, e); }
    private void StackPanel_GiveFeedback(object sender, GiveFeedbackEventArgs e) { TwainCtrl.StackPanelDragFeedBack(sender, e); }

    private async void TwainCtrl_PropertyChangedAsync(object sender, PropertyChangedEventArgs e)
    {
        if (DataContext is GpScannerViewModel ViewModel)
        {
            if (e.PropertyName is "Resimler")
            {
                ViewModel.ReloadFileDatas();
            }

            if (e.PropertyName is "DetectPageSeperator" && ViewModel.DetectBarCode)
            {
                ViewModel.AddBarcodeToList(TwainCtrl?.Scanner?.BarcodeContent);

                if (ViewModel.DetectPageSeperator && TwainCtrl?.Scanner?.BarcodeContent is not null)
                {
                    TwainCtrl.Scanner.FileName = ViewModel.GetPatchCodeResult(TwainCtrl.Scanner.BarcodeContent);
                }
            }

            if (e.PropertyName is "DataBaseTextData" && TwainCtrl?.DataBaseTextData is not null)
            {
                ViewModel.ScannedText = TwainCtrl.DataBaseTextData;
                ViewModel.ScannerData?.Data?.Add(
                new Data { Id = DataSerialize.RandomNumber(), FileName = TwainCtrl?.Scanner?.PdfFilePath, FileContent = string.Join(" ", ViewModel.ScannedText?.Select(z => z.Text)), QrData = TwainCtrl?.Scanner?.BarcodeContent });
                ViewModel.DatabaseSave.Execute(null);
                ViewModel.ScannedText = null;
            }

            if (e.PropertyName is "ImgData" && TwainCtrl?.ImgData is not null)
            {
                if (ViewModel.DetectBarCode)
                {
                    QrCode.QrCode qrcode = new();
                    ViewModel.AddBarcodeToList(qrcode.GetImageBarcodeResult(TwainCtrl.ImgData));
                }

                ViewModel.OcrIsBusy = true;
                ViewModel.ScannedText = await TwainCtrl.ImgData.OcrAsync(Settings.Default.DefaultTtsLang);
                if (ViewModel.ScannedText != null)
                {
                    ViewModel.TranslateViewModel.Metin = string.Join(" ", ViewModel.ScannedText?.Select(z => z.Text));
                    ViewModel.TranslateViewModel.TaramaGeçmiş.Add(ViewModel.TranslateViewModel?.Metin);
                    ViewModel.OcrIsBusy = false;
                }

                TwainCtrl.ImgData = null;
            }

            if (e.PropertyName is "DragMoveStarted")
            {
                ViewModel.ListBoxBorderAnimation = TwainCtrl.DragMoveStarted;
            }

            if (e.PropertyName is "CameraQRCodeData" && TwainCtrl?.CameraQRCodeData is not null)
            {
                ViewModel.AddBarcodeToList(TwainCtrl?.Scanner?.BarcodeContent);
                TwainCtrl.CameraQRCodeData = null;
            }

            if (e.PropertyName is "UsePageSeperator" && TwainCtrl?.Scanner?.UsePageSeperator == true)
            {
                if (Settings.Default.PatchCodes.Count <= 0)
                {
                    TwainCtrl.Scanner.UsePageSeperator = false;
                    _ = MessageBox.Show($"{Translation.GetResStringValue("NOPATCHCODE")}\n{Translation.GetResStringValue("SETTİNGS")}=>{Translation.GetResStringValue("SEPERATOR")}");
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