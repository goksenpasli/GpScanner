using Extensions;
using GpScanner.Properties;
using GpScanner.ViewModel;
using Ocr;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

public partial class MainWindow : Window
{
    public static CollectionViewSource cvs;
    private const int _AboutSysMenuID = 1001;
    private bool _dataBaseBackupTaskStarted;
    private bool _isClosingTaskRunning;

    public MainWindow()
    {
        InitializeComponent();
        cvs = TryFindResource("Veriler") as CollectionViewSource;
        IWindowService windowService = new WindowService();
        IScannerService scannerService = new ScannerService();
        ITwainService twainService = new TwainService(twainCtrl);
        TwainCtrl = twainService.TwainCtrl;
        DataContext = new GpScannerViewModel(windowService, scannerService, twainService);
        TwainCtrl.PropertyChanged += TwainCtrl_PropertyChangedAsync;
        TwainCtrl.Scanner.PropertyChanged += Scanner_PropertyChanged;
        Twainsettings.Settings.Default.PropertyChanged += TwainSettingsDefault_PropertyChanged;
    }

    public TwainCtrl TwainCtrl { get; set; }

    protected override void OnStateChanged(EventArgs e)
    {
        if (Settings.Default.MinimizeTray && Settings.Default.ShowTrayIcon && WindowState == WindowState.Minimized)
        {
            Hide();
        }
        else
        {
            ShowInTaskbar = true;
        }
        base.OnStateChanged(e);
    }

    private async void ContentControl_DropAsync(object sender, DragEventArgs e)
    {
        if (e.OriginalSource is not Image image || image.TemplatedParent is not PdfViewer.PdfViewer pdfviewer)
        {
            return;
        }
        string pdfFilePath = pdfviewer.DataContext as string ?? throw new ArgumentException("Invalid PDF file path.");
        string temporarypdf = $"{Path.GetTempPath()}{Guid.NewGuid()}.pdf";
        try
        {
            if (e?.Data?.GetData(typeof(ScannedImage)) is ScannedImage droppedData)
            {
                int currentPage = pdfviewer.Sayfa;
                droppedData.Resim.GeneratePdf(null, Format.Jpg, TwainCtrl.SelectedPaper).Save(temporarypdf);
                string[] mergedFiles = Keyboard.Modifiers switch
                {
                    ModifierKeys.Alt | ModifierKeys.Shift => [ temporarypdf, pdfFilePath ],
                    ModifierKeys.Shift => [ temporarypdf, pdfFilePath ],
                    ModifierKeys.Alt => [ pdfFilePath, temporarypdf ],
                    _ => [ temporarypdf, pdfFilePath ]
                };

                if ((Keyboard.Modifiers & (ModifierKeys.Alt | ModifierKeys.Shift)) == (ModifierKeys.Alt | ModifierKeys.Shift))
                {
                    await TwainCtrl.RemovePdfPageAsync(pdfFilePath, currentPage, currentPage);
                }
                mergedFiles.MergePdf()?.Save(pdfFilePath);
                await TwainCtrl.ArrangeFileAsync(pdfFilePath, pdfFilePath, 0, currentPage - 1);
            }
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                List<string> DroppedPdfFiles = [ .. files.Where(PdfViewer.PdfViewer.IsValidPdfFile) ];
                if (DroppedPdfFiles?.Any() == true)
                {
                    DroppedPdfFiles.Add(pdfFilePath);
                    await Task.Run(() => DroppedPdfFiles.ToArray().MergePdf()?.Save(pdfFilePath));
                    pdfviewer.Sayfa = 1;
                }
            }
            TwainCtrl.NotifyPdfChange(pdfviewer, temporarypdf, pdfFilePath);
            TwainCtrl.ClosedPdfFilePath = pdfFilePath;
            TwainCtrl.RefreshDocumentList = true;
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"An error occurred while processing the drop operation: {ex.Message}");
        }
    }

    private void DocumentGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Grid grid && e.OriginalSource is Run)
        {
            using System.Drawing.Bitmap img = grid.ToRenderTargetBitmap().Resize(230, 370).BitmapSourceToBitmap();
            using System.Drawing.Icon icon = System.Drawing.Icon.FromHandle(img.GetHicon());
            TwainCtrl.DragCursor = CursorInteropHelper.Create(new SafeIconHandle(icon.Handle));
            _ = DragDrop.DoDragDrop(grid, grid.DataContext, DragDropEffects.Move);
            e.Handled = true;
        }
    }

    private void Grid_GiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        if (e.Effects == DragDropEffects.Move)
        {
            if (TwainCtrl.DragCursor is not null)
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

    private void MW_ContentRendered(object sender, EventArgs e)
    {
        SystemMenu(this);
        string[] commandLineArgs = Environment.GetCommandLineArgs();
        if (DataContext is GpScannerViewModel ViewModel)
        {
            if (Keyboard.IsKeyDown(Key.F8))
            {
                Settings.Default.Reset();
                Twainsettings.Settings.Default.Reset();
                ShowExtendedMessageBox(Translation.GetResStringValue("RESTARTAPP"), false);
            }
            if (Settings.Default.RegisterBatchWatcher)
            {
                ViewModel.RegisterBatchImageFileWatcher(TwainCtrl.SelectedPaper, Settings.Default.BatchFolder, Settings.Default.BatchSaveFolder);
            }

            if (ViewModel.NeedAppUpdate() && ViewModel.CheckUpdate.CanExecute(null))
            {
                TrayIcon.ShowBalloonNearTray(Title, Translation.GetResStringValue("UPDATE"));
                ViewModel.CheckUpdate.Execute(null);
            }

            if (Settings.Default.IsFirstRun)
            {
                Settings.Default.IsFirstRun = false;
                if (ViewModel.OpenSettings.CanExecute(null))
                {
                    ViewModel.OpenSettings.Execute(null);
                }
                TwainCtrl.CreateBuiltInScanProfiles();
            }
        }

        if (commandLineArgs.Length > 1)
        {
            string filePath = commandLineArgs[1];
            string extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            if (File.Exists(filePath))
            {
                if (Settings.Default.DirectOpenEypFile && extension == ".eyp")
                {
                    TwainCtrl.SelectedTabIndex = 3;
                    EypPdfViewer eypPdfViewer = TwainCtrl.PdfImportViewer.PdfViewer;
                    eypPdfViewer.PdfFilePath = eypPdfViewer.ExtractEypFilesToPdf(filePath);
                    return;
                }

                if (Settings.Default.DirectOpenPdfFile && extension == ".pdf" && PdfViewer.PdfViewer.IsValidPdfFile(filePath))
                {
                    TwainCtrl.SelectedTabIndex = 3;
                    EypPdfViewer eypPdfViewer = TwainCtrl.PdfImportViewer.PdfViewer;
                    eypPdfViewer.PdfFilePath = filePath;
                    eypPdfViewer.AddToHistoryList(eypPdfViewer.PdfFilePath);
                    return;
                }
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

        if (!string.IsNullOrWhiteSpace(Settings.Default.StartupMessage))
        {
            ShowExtendedMessageBox(Settings.Default.StartupMessage, false);
        }
    }

    private void QrListBox_Drop(object sender, DragEventArgs e)
    {
        if (e?.Data?.GetData(typeof(ScannedImage)) is ScannedImage scannedImage && DataContext is GpScannerViewModel ViewModel)
        {
            QrCode.QrCode qrcode = new();
            List<string> barcodes = qrcode.GetMultipleImageBarcodeResult(scannedImage.Resim);
            if (barcodes is not null)
            {
                foreach (string barcode in barcodes)
                {
                    ViewModel.BarcodeList?.Add(barcode);
                }
            }
        }
    }

    private void Scanner_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if ((e.PropertyName is "ApplyPdfSaveOcr" && TwainCtrl?.Scanner?.ApplyPdfSaveOcr == true) || (e.PropertyName is "ApplyDataBaseOcr" && TwainCtrl?.Scanner?.ApplyDataBaseOcr == true))
        {
            if (DataContext is GpScannerViewModel ViewModel && ViewModel?.TesseractViewModel?.GetTesseractFiles(ViewModel.TesseractViewModel.Tessdatafolder)?.Count(item => item.Checked) == 0)
            {
                TwainCtrl.Scanner.ApplyPdfSaveOcr = false;
                TwainCtrl.Scanner.ApplyDataBaseOcr = false;
                ShowExtendedMessageBox($"{Translation.GetResStringValue("SETTİNGS")}{Environment.NewLine}{Translation.GetResStringValue("TESSLANGSELECT")}", false);
            }
        }
    }

    private void ShowExtendedMessageBox(string message, bool showProgress)
    {
        ExtendedMessageBox extendedMessageBox = new() { YesButton = showProgress ? Visibility.Collapsed : Visibility.Visible, ProgressBarVisibility = showProgress ? Visibility.Visible : Visibility.Collapsed, IsIndeterminate = showProgress };
        extendedMessageBox.ShowDialog(this, message, Title);
    }

    private void SystemMenu(MainWindow mainwindow)
    {
        IntPtr systemMenuHandle = new WindowInteropHelper(mainwindow).Handle.GetSystemMenu(false);
        _ = systemMenuHandle.InsertMenu(7, WindowExtensions.MF_BYPOSITION, _AboutSysMenuID, Translation.GetResStringValue("ABOUT"));
        HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(mainwindow).Handle);
        source?.AddHook(WndProc);
    }

    private async void TwainCtrl_PropertyChangedAsync(object sender, PropertyChangedEventArgs e)
    {
        if (DataContext is GpScannerViewModel ViewModel)
        {
            if (e.PropertyName is "Resimler")
            {
                string savefilename = TwainCtrl.Scanner.PdfFilePath;
                FileInfo fi = new(savefilename);
                Scanner scanner = new() { FileName = savefilename, FolderName = fi?.Directory?.Name, FileSize = fi.Length / 1048576F };
                ViewModel.Dosyalar?.Add(scanner);
            }

            if (e.PropertyName is "DetectPageSeperator" && ViewModel.DetectBarCode)
            {
                string detectedbarcode = TwainCtrl?.Scanner?.BarcodeContent;
                if (detectedbarcode is not null)
                {
                    ViewModel.AddBarcodeToList(detectedbarcode);
                    if (TwainCtrl?.Scanner?.UsePageSeperator == true)
                    {
                        TwainCtrl.Scanner.FileName = ViewModel.GetFileNameFromPatchCodeResult(detectedbarcode);
                    }
                }
            }

            if (e.PropertyName is "DataBaseTextDataCompleted" && TwainCtrl?.DataBaseTextDataCompleted == true && TwainCtrl?.DataBaseTextData is not null)
            {
                ViewModel.ScannedText = [ with(TwainCtrl.DataBaseTextData.SelectMany(z => z)) ];
                using (AppDbContext context = new())
                {
                    _ = context.Data.Add(new Data { FileName = TwainCtrl?.Scanner?.PdfFilePath, FileContent = string.Join(" ", ViewModel.ScannedText?.Select(z => z.Text)), QrData = TwainCtrl?.Scanner?.BarcodeContent });
                    _ = context.SaveChanges();
                }
                ViewModel.ScannedText = null;
            }

            if (e.PropertyName is "ImgData" && TwainCtrl?.ImgData is not null)
            {
                if (ViewModel.DetectBarCode)
                {
                    QrCode.QrCode qrcode = new();
                    ViewModel.AddBarcodeToList(qrcode.GetImageBarcodeResult(TwainCtrl.ImgData));
                }

                if (string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang))
                {
                    TwainCtrl.ImgData = null;
                    return;
                }

                ViewModel.OcrIsBusy = true;
                ViewModel.ScannedText = await TwainCtrl.ImgData.OcrAsync(Settings.Default.DefaultTtsLang);
                if (ViewModel.ScannedText is not null)
                {
                    ViewModel.TranslateViewModel.Metin = string.Join(" ", ViewModel.ScannedText?.Select(z => z.Text));
                    ViewModel.OcrIsBusy = false;
                }

                TwainCtrl.ImgData = null;
            }

            if (e.PropertyName is "DragMoveStarted")
            {
                ViewModel.ListBoxBorderAnimation = TwainCtrl.DragMoveStarted;
            }

            if (e.PropertyName is "CameraQRCodeData")
            {
                ViewModel.AddBarcodeToList(TwainCtrl?.Scanner?.BarcodeContent);
            }

            if (e.PropertyName is "UsePageSeperator" && TwainCtrl?.Scanner?.UsePageSeperator == true && Settings.Default.PatchCodes.Count == 0)
            {
                TwainCtrl.Scanner.UsePageSeperator = false;
                ShowExtendedMessageBox($"{Translation.GetResStringValue("NOPATCHCODE")}\n{Translation.GetResStringValue("SETTİNGS")}=>{Translation.GetResStringValue("QRDETECT")}", false);
            }

            if (e.PropertyName is "RefreshDocumentList" && TwainCtrl?.RefreshDocumentList == true)
            {
                string closedfile = TwainCtrl.ClosedPdfFilePath;
                ViewModel.RefreshItems<Scanner>(
                    ViewModel.Dosyalar,
                    item => item.FileName == closedfile,
                    item =>
                    {
                        item.FileSize = new FileInfo(closedfile).Length / 1048576F;
                        item.FileName = null;
                        item.FileName = closedfile;
                    });
                TwainCtrl.ClosedPdfFilePath = null;
                TwainCtrl.RefreshDocumentList = false;
                ViewModel.ReloadDocumentViewerFiles();
            }

            if (e.PropertyName is "SaveFileFullPath" && ViewModel?.HistorySaveList?.Contains(TwainCtrl?.Scanner?.SaveFileFullPath) == false)
            {
                ViewModel.HistorySaveList.Add(TwainCtrl.Scanner.SaveFileFullPath);
            }

            if (e.PropertyName is "SetShutdown")
            {
                ViewModel.Shutdown = TwainCtrl?.SetShutdown == true;
            }
        }
    }

    private void TwainSettingsDefault_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (DataContext is GpScannerViewModel ViewModel && e.PropertyName is "FolderDateFormat")
        {
            ViewModel.NotifyBaşlangıçTarihi();
        }
    }

    private async void Window_Closing(object sender, CancelEventArgs e)
    {
        TwainCtrl.SaveSettings();
        if (TwainCtrl.FileSaveTask?.IsCompleted == false || (DataContext as GpScannerViewModel)?.Filesavetask?.IsCompleted == false)
        {
            ShowExtendedMessageBox(Translation.GetResStringValue("TASKSRUNNING"), false);
            e.Cancel = true;
            return;
        }

        if (_isClosingTaskRunning)
        {
            e.Cancel = true;
            return;
        }

        if (!_dataBaseBackupTaskStarted && Settings.Default.BackUpDatabase)
        {
            ShowExtendedMessageBox(Translation.GetResStringValue("BACKUPDB"), true);
            _dataBaseBackupTaskStarted = true;
            _isClosingTaskRunning = true;
            e.Cancel = true;
            await Task.Run(BackupDatabaseFile);
            _isClosingTaskRunning = false;
            Close();
        }
        StillImageHelper.KillServer();
    }

    [DebuggerStepThrough]
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WindowExtensions.WM_SYSCOMMAND)
        {
            switch (wParam.ToInt32())
            {
                case _AboutSysMenuID:
                    _ = Process.Start("https://github.com/goksenpasli");
                    handled = true;
                    break;
            }
        }

        return IntPtr.Zero;
    }
}