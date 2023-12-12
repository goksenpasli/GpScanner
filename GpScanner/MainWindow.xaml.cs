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
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using TwainControl;
using static Extensions.ExtensionMethods;
using static GpScanner.ViewModel.GpScannerViewModel;
using static TwainControl.DrawControl;
using Twainsettings = TwainControl.Properties;

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
        twainCtrl.PropertyChanged += TwainCtrl_PropertyChangedAsync;
        twainCtrl.Scanner.PropertyChanged += Scanner_PropertyChanged;
        TwainCtrl = twainCtrl;
    }

    public TwainCtrl TwainCtrl { get; set; }

    protected override void OnStateChanged(EventArgs e)
    {
        if (Settings.Default.MinimizeTray && WindowState == WindowState.Minimized)
        {
            Hide();
        }
        base.OnStateChanged(e);
    }

    private async void ContentControl_DropAsync(object sender, DragEventArgs e)
    {
        if (e.OriginalSource is Image image && e.Data.GetData(typeof(ScannedImage)) is ScannedImage droppedData && image.TemplatedParent is PdfViewer.PdfViewer pdfviewer)
        {
            try
            {
                string temporarypdf = $"{Path.GetTempPath()}{Guid.NewGuid()}.pdf";
                string pdfFilePath = (string)pdfviewer.DataContext;
                int curpage = pdfviewer.Sayfa;
                droppedData.Resim.GeneratePdf(null, Format.Jpg, twainCtrl.SelectedPaper).Save(temporarypdf);
                string[] processedfiles = [temporarypdf, pdfFilePath];
                if (Keyboard.Modifiers == (ModifierKeys.Alt | ModifierKeys.Shift))
                {
                    await TwainCtrl.RemovePdfPageAsync(pdfFilePath, curpage, curpage);
                    processedfiles.MergePdf().Save(pdfFilePath);
                    await TwainCtrl.ArrangeFileAsync(pdfFilePath, pdfFilePath, 0, curpage - 1);
                    TwainCtrl.NotifyPdfChange(pdfviewer, temporarypdf, pdfFilePath);
                    return;
                }

                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    processedfiles.MergePdf().Save(pdfFilePath);
                    await TwainCtrl.ArrangeFileAsync(pdfFilePath, pdfFilePath, 0, curpage - 1);
                    TwainCtrl.NotifyPdfChange(pdfviewer, temporarypdf, pdfFilePath);
                    return;
                }

                string[] pdffiles = Keyboard.Modifiers == ModifierKeys.Alt ? [pdfFilePath, temporarypdf] : [temporarypdf, pdfFilePath];
                pdffiles.MergePdf().Save(pdfFilePath);
                TwainCtrl.NotifyPdfChange(pdfviewer, temporarypdf, pdfFilePath);
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void DocumentGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Grid grid)
        {
            using System.Drawing.Icon icon = System.Drawing.Icon.FromHandle(grid.ToRenderTargetBitmap().BitmapSourceToBitmap().GetHicon());
            TwainCtrl.DragCursor = CursorInteropHelper.Create(new SafeIconHandle(icon.Handle));
            _ = DragDrop.DoDragDrop(grid, grid.DataContext, DragDropEffects.Move);
            e.Handled = true;
        }
    }

    private void Grid_GiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        if (e.Effects == DragDropEffects.Move)
        {
            if (TwainCtrl.DragCursor != null)
            {
                e.UseDefaultCursors = false;
                _ = Mouse.SetCursor(TwainCtrl.DragCursor);
            }
        }
        else
        {
            e.UseDefaultCursors = true;
        }
        e.Handled = true;
    }

    private async void ListBox_DropAsync(object sender, DragEventArgs e) => await twainCtrl.ListBoxDropFileAsync(e);

    private void MiniDocumentRun_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Run run)
        {
            twainCtrl.DragMoveStarted = true;
            StackPanel stackPanel = (run.Parent as TextBlock)?.Parent as StackPanel;
            using System.Drawing.Icon icon = System.Drawing.Icon.FromHandle(stackPanel.ToRenderTargetBitmap().BitmapSourceToBitmap().GetHicon());
            TwainCtrl.DragCursor = CursorInteropHelper.Create(new SafeIconHandle(icon.Handle));
            _ = DragDrop.DoDragDrop(run, run.DataContext, DragDropEffects.Move);
            twainCtrl.DragMoveStarted = false;
            e.Handled = true;
        }
    }

    private void MW_ContentRendered(object sender, EventArgs e)
    {
        this.SystemMenu();
        string[] commandLineArgs = Environment.GetCommandLineArgs();
        if (DataContext is GpScannerViewModel ViewModel)
        {
            if (Keyboard.IsKeyDown(Key.F8))
            {
                Settings.Default.Reset();
                Twainsettings.Settings.Default.Reset();
                _ = MessageBox.Show(this, Translation.GetResStringValue("RESTARTAPP"));
            }
            if (Settings.Default.RegisterBatchWatcher)
            {
                ViewModel.RegisterBatchImageFileWatcher(twainCtrl.SelectedPaper, Settings.Default.BatchFolder, Settings.Default.BatchSaveFolder);
            }

            if (ViewModel.NeedAppUpdate() && ViewModel.CheckUpdate.CanExecute(null))
            {
                ViewModel.CheckUpdate.Execute(null);
            }

            if (Settings.Default.IsFirstRun)
            {
                Settings.Default.IsFirstRun = false;
                if (ViewModel.OpenSettings.CanExecute(null))
                {
                    ViewModel.OpenSettings.Execute(null);
                }
                twainCtrl.CreateBuiltInScanProfiles();
            }
        }

        if (commandLineArgs.Length > 1)
        {
            string filePath = commandLineArgs[1];
            string extension = Path.GetExtension(filePath)?.ToLower();
            if (File.Exists(filePath))
            {
                if (Settings.Default.DirectOpenEypFile && extension == ".eyp")
                {
                    EypPdfViewer eypPdfViewer = twainCtrl.PdfImportViewer.PdfViewer;
                    eypPdfViewer.PdfFilePath = eypPdfViewer.ExtractEypFilesToPdf(filePath);
                    eypPdfViewer.AddToHistoryList(eypPdfViewer.PdfFilePath);
                    twainCtrl.MaximizePdfControl.Execute(null);
                    return;
                }

                if (Settings.Default.DirectOpenPdfFile && extension == ".pdf" && PdfViewer.PdfViewer.IsValidPdfFile(filePath))
                {
                    EypPdfViewer eypPdfViewer = twainCtrl.PdfImportViewer.PdfViewer;
                    eypPdfViewer.PdfFilePath = filePath;
                    eypPdfViewer.AddToHistoryList(eypPdfViewer.PdfFilePath);
                    twainCtrl.MaximizePdfControl.Execute(null);
                    return;
                }

                if (Settings.Default.DirectOpenUdfFile && extension == ".udf")
                {
                    twainCtrl.xpsViewer.XpsDataFilePath = twainCtrl.LoadUdfFile(filePath);
                    return;
                }
            }
            _ = twainCtrl.AddFiles(commandLineArgs, twainCtrl.DecodeHeight);
        }

        if (StillImageHelper.FirstLanuchScan)
        {
            switch (Settings.Default.ButtonScanMode)
            {
                case 0 when twainCtrl.ScanImage.CanExecute(null):
                    twainCtrl.ScanImage.Execute(null);
                    break;

                case 1 when twainCtrl.FastScanImage.CanExecute(null):
                    twainCtrl.FastScanImage.Execute(null);
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
                        case 0 when twainCtrl.ScanImage.CanExecute(null):
                            Dispatcher.Invoke(() => twainCtrl.ScanImage.Execute(null));
                            break;

                        case 1 when twainCtrl.FastScanImage.CanExecute(null):
                            Dispatcher.Invoke(() => twainCtrl.FastScanImage.Execute(null));
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

    private void Scanner_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if ((e.PropertyName is "ApplyPdfSaveOcr" && twainCtrl?.Scanner?.ApplyPdfSaveOcr == true) || (e.PropertyName is "ApplyDataBaseOcr" && twainCtrl?.Scanner?.ApplyDataBaseOcr == true))
        {
            if (DataContext is GpScannerViewModel ViewModel && ViewModel?.TesseractViewModel?.GetTesseractFiles(ViewModel.TesseractViewModel.Tessdatafolder)?.Count(item => item.Checked) == 0)
            {
                twainCtrl.Scanner.ApplyPdfSaveOcr = false;
                twainCtrl.Scanner.ApplyDataBaseOcr = false;
                _ = MessageBox.Show($"{Translation.GetResStringValue("SETTİNGS")}{Environment.NewLine}{Translation.GetResStringValue("TESSLANGSELECT")}", Title);
            }
        }
    }

    private void StackPanel_Drop(object sender, DragEventArgs e) => twainCtrl.DropFile(sender, e);

    private void StackPanel_GiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        if (e.Effects == DragDropEffects.Move)
        {
            if (TwainCtrl.DragCursor != null)
            {
                e.UseDefaultCursors = false;
                _ = Mouse.SetCursor(TwainCtrl.DragCursor);
            }
        }
        else
        {
            e.UseDefaultCursors = true;
        }
        e.Handled = true;
    }

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
                ViewModel.AddBarcodeToList(twainCtrl?.Scanner?.BarcodeContent);

                if (ViewModel.DetectPageSeperator && twainCtrl?.Scanner?.BarcodeContent is not null)
                {
                    twainCtrl.Scanner.FileName = ViewModel.GetPatchCodeResult(twainCtrl.Scanner.BarcodeContent);
                }
            }

            if (e.PropertyName is "DataBaseTextData" && twainCtrl?.DataBaseTextData is not null)
            {
                ViewModel.ScannedText = twainCtrl.DataBaseTextData;
                using (AppDbContext context = new())
                {
                    _ = context.Data.Add(new Data { FileName = twainCtrl?.Scanner?.PdfFilePath, FileContent = string.Join(" ", ViewModel.ScannedText?.Select(z => z.Text)), QrData = twainCtrl?.Scanner?.BarcodeContent });
                    _ = context.SaveChanges();
                }
                ViewModel.ScannedText = null;
            }

            if (e.PropertyName is "ImgData" && twainCtrl?.ImgData is not null)
            {
                if (ViewModel.DetectBarCode)
                {
                    QrCode.QrCode qrcode = new();
                    ViewModel.AddBarcodeToList(qrcode.GetImageBarcodeResult(twainCtrl.ImgData));
                }

                if (string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang))
                {
                    twainCtrl.ImgData = null;
                    return;
                }

                ViewModel.OcrIsBusy = true;
                ViewModel.ScannedText = await twainCtrl.ImgData.OcrAsync(Settings.Default.DefaultTtsLang);
                if (ViewModel.ScannedText != null)
                {
                    ViewModel.TranslateViewModel.Metin = string.Join(" ", ViewModel.ScannedText?.Select(z => z.Text));
                    ViewModel.OcrIsBusy = false;
                }

                twainCtrl.ImgData = null;
            }

            if (e.PropertyName is "DragMoveStarted")
            {
                ViewModel.ListBoxBorderAnimation = twainCtrl.DragMoveStarted;
            }

            if (e.PropertyName is "CameraQRCodeData" && twainCtrl?.CameraQRCodeData is not null)
            {
                ViewModel.AddBarcodeToList(twainCtrl?.Scanner?.BarcodeContent);
            }

            if (e.PropertyName is "UsePageSeperator" && twainCtrl?.Scanner?.UsePageSeperator == true)
            {
                if (Settings.Default.PatchCodes.Count <= 0)
                {
                    twainCtrl.Scanner.UsePageSeperator = false;
                    _ = MessageBox.Show($"{Translation.GetResStringValue("NOPATCHCODE")}\n{Translation.GetResStringValue("SETTİNGS")}=>{Translation.GetResStringValue("SEPERATOR")}", Title);
                    return;
                }

                ViewModel.DetectPageSeperator = twainCtrl.Scanner.UsePageSeperator;
            }
            if (e.PropertyName is "RefreshDocumentList" && twainCtrl?.RefreshDocumentList == true)
            {
                DateTime tempdate = ViewModel.SeçiliGün;
                ViewModel.ReloadFileDatas(false);
                ViewModel.SeçiliGün = tempdate;
                twainCtrl.RefreshDocumentList = false;
            }
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (TwainCtrl.Filesavetask?.IsCompleted == false || (DataContext as GpScannerViewModel)?.Filesavetask?.IsCompleted == false)
        {
            _ = MessageBox.Show(Translation.GetResStringValue("TASKSRUNNING"), Title);
            e.Cancel = true;
        }

        BackupDatabaseFile();
        StillImageHelper.KillServer();
    }
}