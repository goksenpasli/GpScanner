using Extensions;
using GpScanner.Properties;
using Microsoft.SharePoint.Client;
using Microsoft.Win32;
using Ocr;
using PdfCompressor;
using PdfSharp.Pdf;
using SevenZipExtractor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using System.Xml.Linq;
using TwainControl;
using WebPWrapper;
using Xceed.Words.NET;
using static Extensions.ExtensionMethods;
using Application = System.Windows.Application;
using File = System.IO.File;
using FlowDirection = System.Windows.FlowDirection;
using InpcBase = Extensions.InpcBase;
using ListBox = System.Windows.Controls.ListBox;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Twainsettings = TwainControl.Properties;

namespace GpScanner.ViewModel;

public class GpScannerViewModel : InpcBase, IDataErrorInfo
{
    public static readonly string ErrorFile = "Error.log";
    public static readonly string ProfileFolder = $"{Path.GetDirectoryName(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath)}";
    public Task Filesavetask;
    public CancellationTokenSource ocrcancellationToken;
    public CancellationTokenSource unindexedfileocrcancellationToken;
    private const string MinimumVcVersion = "14.21.27702";
    private const int NetFxMinVersion = 461808;
    private static readonly SemaphoreSlim LogSemaphore = new(1, 1);
    private static DispatcherTimer flaganimationtimer;
    private static DispatcherTimer timer;
    private readonly string AppName;
    private readonly IdleTimeIndexer ıdleTimeIndexer;
    private readonly string[] sqlitedangerouscommands = ["truncate", "drop", "alter"];
    private readonly string[] supportedfilesextension = [".pdf", ".webp", ".eyp", ".tiff", ".tif", ".jpg", ".jpeg", ".jpe", ".png", ".bmp", ".zip", ".xps", ".mp4", ".3gp", ".wmv", ".mpg", ".mov", ".avi", ".mpeg", ".xml", ".xsl", ".xslt", ".xaml", ".xls", ".xlsx", ".xlsb", ".csv", ".docx", ".rar", ".7z", ".xz", ".gz", ".jb2"];
    private readonly List<string> unindexedfileextensions = [".pdf", ".webp", ".tiff", ".tif", ".jpg", ".jpe", ".gif", ".jpeg", ".jfif", ".png", ".bmp", ".docx", ".xlsx", ".xls", ".xlsb", ".csv"];
    private int cycleIndex;
    private GridLength mainWindowDocumentGuiControlLength = new(1, GridUnitType.Star);
    private GridLength mainWindowGuiControlLength = new(3, GridUnitType.Star);
    private Size selectedCompressorProfile;
    private Size selectedSize;

    public GpScannerViewModel(IWindowService windowService, TwainCtrl twainCtrl)
    {
        WindowService = windowService;
        TwainCtrl = twainCtrl;
        CreateEmptySqliteDatabase();
        RegisterSimplePdfFileWatcher();
        TesseractViewModel = new TesseractViewModel(windowService, twainCtrl);
        TranslateViewModel = new TranslateViewModel();
        Settings.Default.PropertyChanged += Default_PropertyChanged;
        PropertyChanged += GpScannerViewModel_PropertyChanged;
        AppName = windowService?.GetFirstWindow()?.Title;
        LoadFiles = new RelayCommand<object>(async parameter => Dosyalar = await GetScannerFileData(), parameter => true);
        LoadFiles.Execute(null);
        SeçiliDil = !string.IsNullOrWhiteSpace(Settings.Default.DefaultLang) ? Settings.Default.DefaultLang : "TÜRKÇE";
        BaşlangıçTarihi = BitişTarihi = DateTime.Today;
        SelectedSize = Settings.Default.PreviewIndex;
        GenerateAnimationTimer();
        GenerateJumpList();
        LoadRemainder = new RelayCommand<object>(async parameter => await LoadRemainderDatas(), parameter => true);
        LoadRemainder.Execute(null);
        ıdleTimeIndexer = new(this, Settings.Default.IdleMinuteIndex);
        RunIdleIndexOperation();

        RegisterSti = new RelayCommand<object>(parameter => StillImageHelper.Register(), parameter => IsAdministrator);

        UnRegisterSti = new RelayCommand<object>(parameter => StillImageHelper.Unregister(), parameter => IsAdministrator);

        PdfBirleştir = new RelayCommand<object>(
            async parameter =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    await Task.Run(
                        async () =>
                        {
                            List<string> pdffilelist = [.. Dosyalar.Where(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)).Select(z => z.FileName)];
                            string savefilename = PdfGeneration.GetPdfScanPath();
                            pdffilelist.ToArray().MergePdf().Save(savefilename);
                            FileInfo fi = new(savefilename);
                            Scanner scanner = new() { FileName = savefilename, FolderName = fi?.Directory?.Name, FileSize = fi.Length / 1048576F };
                            await Application.Current?.Dispatcher?.InvokeAsync(() => Dosyalar?.Add(scanner));
                        });
                    return;
                }

                SaveFileDialog saveFileDialog = new() { Filter = "Pdf Dosyası(*.pdf)|*.pdf", FileName = Translation.GetResStringValue("MERGE") };
                if (saveFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        await Task.Run(
                            () =>
                            {
                                List<string> pdffilelist = [.. Dosyalar.Where(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)).Select(z => z.FileName)];
                                pdffilelist.ToArray().MergePdf().Save(saveFileDialog.FileName);
                            });
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException(ex?.Message);
                    }
                }
            },
            parameter =>
            {
                CheckedPdfCount = Dosyalar?.Count(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)) ?? 0;
                return CheckedPdfCount > 1;
            });

        PdfZipBirleştir = new RelayCommand<object>(
            async parameter =>
            {
                SaveFileDialog saveFileDialog = new() { Filter = "Zip Dosyası(*.zip)|*.zip", FileName = Translation.GetResStringValue("MERGE") };
                if (saveFileDialog.ShowDialog() != true)
                {
                    return;
                }
                await Task.Run(
                    () =>
                    {
                        List<string> pdffilelist = [.. Dosyalar.Where(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)).Select(z => z.FileName)];
                        using ZipArchive archive = ZipFile.Open(saveFileDialog.FileName, ZipArchiveMode.Update);
                        for (int i = 0; i < pdffilelist.Count; i++)
                        {
                            string fPath = pdffilelist[i];
                            _ = archive.CreateEntryFromFile(fPath, Path.GetFileName(fPath));
                            ZipProgress = (i + 1) / (double)pdffilelist.Count;
                        }
                        ZipProgressIndeterminate = true;
                    });
                ZipProgress = 0;
                ZipProgressIndeterminate = false;
            },
            parameter =>
            {
                CheckedPdfCount = Dosyalar?.Count(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)) ?? 0;
                return CheckedPdfCount > 0;
            });

        OcrPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is not BitmapFrame bitmapframe)
                {
                    return;
                }
                byte[] imgdata = bitmapframe.ToTiffJpegByteArray(Format.Jpg);
                OcrIsBusy = true;
                ScannedText = await imgdata.OcrAsync(Settings.Default.DefaultTtsLang);
                OcrIsBusy = false;
                if (ScannedText is not null)
                {
                    TranslateViewModel.Metin = string.Join(" ", ScannedText.Select(z => z.Text));
                }

                if (DetectBarCode)
                {
                    QrCode.QrCode qrcode = new();
                    string result = await Task.Run(() => qrcode.GetImageBarcodeResult(bitmapframe));
                    if (result is not null)
                    {
                        BarcodeList.Add(result);
                    }
                }

                bitmapframe = null;
                imgdata = null;
            },
            parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang) && parameter is BitmapFrame bitmapFrame && bitmapFrame is not null);

        OcrPdfThumbnailPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is not PdfViewer.PdfViewer pdfviewer || !PdfViewer.PdfViewer.IsValidPdfFile(pdfviewer.PdfFilePath))
                {
                    return;
                }
                byte[] filedata = await PdfViewer.PdfViewer.ReadAllFileAsync(pdfviewer.PdfFilePath);
                if (filedata is null)
                {
                    return;
                }
                OcrIsBusy = true;
                ObservableCollection<OcrData> ocrdata;
                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    for (int i = 1; i <= pdfviewer.ToplamSayfa; i++)
                    {
                        using MemoryStream ms = await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, i, Twainsettings.Settings.Default.ImgLoadResolution);
                        ocrdata = await ms.ToArray().OcrAsync(Settings.Default.DefaultTtsLang);
                        using (AppDbContext context = new())
                        {
                            _ = context.Data.Add(new Data { FileName = pdfviewer.PdfFilePath, FileContent = string.Join(" ", ocrdata?.Select(z => z.Text)) });
                            _ = context.SaveChanges();
                        }
                        OcrPdfThumbnailPageNumber = i;
                    }
                    OcrPdfThumbnailPageNumber = null;
                }
                else
                {
                    using MemoryStream ms = await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, pdfviewer.Sayfa, Twainsettings.Settings.Default.ImgLoadResolution);
                    ocrdata = await ms.ToArray().OcrAsync(Settings.Default.DefaultTtsLang);
                    using AppDbContext context = new();
                    _ = context.Data.Add(new Data { FileName = pdfviewer.PdfFilePath, FileContent = string.Join(" ", ocrdata?.Select(z => z.Text)) });
                    _ = context.SaveChanges();
                }
                filedata = null;
                ocrdata = null;
                OcrIsBusy = false;
            },
            parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang) && (Settings.Default.ThumbMultipleOcrEnabled || !OcrIsBusy));

        UnindexedFileOcr = new RelayCommand<object>(
            async parameter =>
            {
                OcrIsBusy = true;
                try
                {
                    string unIndexedFile = parameter as string;
                    string ocrText = await ProcessFileAsync(unIndexedFile);
                    await SaveOcrTextToFileAsync(unIndexedFile, ocrText);
                    _ = (UnIndexedFiles?.Remove(unIndexedFile));
                }
                catch (Exception ex)
                {
                    await WriteToLogFile($@"{ProfileFolder}\{ErrorFile}", ex?.Message);
                }
                finally
                {
                    OcrIsBusy = false;
                    GC.Collect();
                }
                if (Shutdown)
                {
                    ViewModel.Shutdown.DoExitWin(ViewModel.Shutdown.EWX_SHUTDOWN);
                }
            },
            parameter => !OcrIsBusy && parameter is string pdffilepath && File.Exists(pdffilepath) && !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang));

        UnindexedAllFilesOcr = new RelayCommand<object>(
            async parameter =>
            {
                int i = 1;
                int slicecount = UnIndexedFiles.Count > Settings.Default.BatchOcrProcessorCount ? UnIndexedFiles.Count / Settings.Default.BatchOcrProcessorCount : 1;
                List<Task> Tasks = [];
                OcrIsBusy = true;
                unindexedfileocrcancellationToken = new CancellationTokenSource();
                foreach (List<string> unIndexedFile in TwainCtrl.ChunkBy(UnIndexedFiles.ToList(), slicecount))
                {
                    Task task = Task.Run(
                        async () =>
                        {
                            foreach (string item in unIndexedFile)
                            {
                                if (unindexedfileocrcancellationToken?.IsCancellationRequested != false)
                                {
                                    continue;
                                }
                                try
                                {
                                    string ocrText = await ProcessFileAsync(item);
                                    await SaveOcrTextToFileAsync(item, ocrText);
                                    _ = await Application.Current?.Dispatcher?.InvokeAsync(() => UnIndexedFiles?.Remove(item));
                                    IndexedFileCount = i++;
                                }
                                catch (Exception ex)
                                {
                                    await LogErrorAsync(ex);
                                }
                                finally
                                {
                                    GC.Collect();
                                }
                            }
                        },
                        unindexedfileocrcancellationToken.Token);
                    Tasks.Add(task);
                }
                await Task.WhenAll(Tasks);
                OcrIsBusy = false;
                if (Shutdown)
                {
                    ViewModel.Shutdown.DoExitWin(ViewModel.Shutdown.EWX_SHUTDOWN);
                }
            },
            parameter => !OcrIsBusy && UnIndexedFiles?.Count > 0 && !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang));

        WordOcrPdfThumbnailPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is not PdfViewer.PdfViewer pdfviewer || !PdfViewer.PdfViewer.IsValidPdfFile(pdfviewer.PdfFilePath))
                {
                    return;
                }
                byte[] filedata = await PdfViewer.PdfViewer.ReadAllFileAsync(pdfviewer.PdfFilePath);
                if (filedata is null)
                {
                    return;
                }
                OcrIsBusy = true;
                ObservableCollection<OcrData> ocrdata;
                using MemoryStream ms = await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, pdfviewer.Sayfa, Twainsettings.Settings.Default.ImgLoadResolution);
                ocrdata = await ms.ToArray().OcrAsync(Settings.Default.DefaultTtsLang, true);
                OcrIsBusy = false;
                filedata = null;
                SaveFileDialog saveFileDialog = new() { Filter = "Docx Dosyası(*.docx)|*.docx", FileName = Translation.GetResStringValue("FILE") };
                if (saveFileDialog.ShowDialog() == true)
                {
                    using DocX document = WriteDocxFile(ocrdata, saveFileDialog.FileName);
                    document.Save(saveFileDialog.FileName);
                }
                ocrdata = null;
            },
            parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang) && !OcrIsBusy);

        OpenOriginalFile = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is not string filepath)
                {
                    return;
                }

                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    ExploreFile.Execute(filepath);
                    return;
                }

                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    TwainCtrl.GotoPage(filepath);
                    return;
                }

                DocumentViewerWindow documentViewerWindow = new();
                if (documentViewerWindow.DataContext is DocumentViewerModel documentViewerModel)
                {
                    documentViewerModel.FilePath = filepath;
                    documentViewerWindow.Icon = ShellIcon.GetFileIconBySize(filepath, 0);
                    documentViewerWindow.Show();
                    documentViewerWindow.Lb?.ScrollIntoView(filepath);
                }
            },
            parameter => parameter is string filepath && File.Exists(filepath));

        ChangeDataFolder = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new() { Filter = "SqLite Database(*.db)|*.db", FileName = "Data.db" };
                if (openFileDialog.ShowDialog() == true)
                {
                    Settings.Default.DatabaseFile = openFileDialog.FileName;
                    Settings.Default.Save();
                }
            },
            parameter => true);

        LoadUnindexedFiles = new RelayCommand<object>(
            async parameter =>
            {
                UnIndexedFiles = await GetUnindexedFileData();
                ShowUnindexedFileWarn = UnIndexedFiles?.Count > Settings.Default.UnindexedFileCount;
                ShowUnindexedFileWarn = false;
            },
            parameter => true);

        Tümünüİşaretle = new RelayCommand<object>(
            parameter =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    foreach (Scanner item in Dosyalar)
                    {
                        item.Seçili = true;
                    }

                    return;
                }

                foreach (Scanner item in MainWindow.cvs.View.OfType<Scanner>().Where(z => Path.GetExtension(z.FileName.ToLowerInvariant()) == ".pdf"))
                {
                    item.Seçili = true;
                }
            },
            parameter => Dosyalar?.Count > 0);

        TümününİşaretiniKaldır = new RelayCommand<object>(
            parameter =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    foreach (Scanner item in Dosyalar)
                    {
                        item.Seçili = false;
                    }

                    return;
                }

                foreach (Scanner item in MainWindow.cvs.View)
                {
                    item.Seçili = false;
                }
            },
            parameter => Dosyalar?.Count > 0);

        Tersiniİşaretle = new RelayCommand<object>(
            parameter =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    foreach (Scanner item in Dosyalar)
                    {
                        item.Seçili = !item.Seçili;
                    }

                    return;
                }

                foreach (Scanner item in MainWindow.cvs.View.OfType<Scanner>().Where(z => Path.GetExtension(z.FileName.ToLowerInvariant()) == ".pdf"))
                {
                    item.Seçili = !item.Seçili;
                }
            },
            parameter => Dosyalar?.Count > 0);

        ExploreFile =
            new RelayCommand<object>(parameter => OpenFolderAndSelectItem(Path.GetDirectoryName(parameter as string), Path.GetFileName(parameter as string)), parameter => true);

        GridSplitterMouseDoubleClick =
            new RelayCommand<object>(
            parameter =>
            {
                MainWindowDocumentGuiControlLength = new GridLength(1, GridUnitType.Star);
                MainWindowGuiControlLength = new GridLength(3, GridUnitType.Star);
            },
            parameter => true);

        GridSplitterMouseRightButtonDown =
            new RelayCommand<object>(
            parameter =>
            {
                MainWindowDocumentGuiControlLength = new GridLength(0, GridUnitType.Star);
                MainWindowGuiControlLength = new GridLength(1, GridUnitType.Star);
            },
            parameter => true);

        CheckUpdate = new RelayCommand<object>(
            parameter =>
            {
                FileVersionInfo version = FileVersionInfo.GetVersionInfo(Process.GetCurrentProcess().MainModule.FileName);
                _ = Process.Start(
                    $@"{Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)}\twux32.exe",
                    $"/w:{new WindowInteropHelper(windowService.GetActiveWindow()).Handle} https://github.com/goksenpasli/GpScanner/releases/download/{version.FileMajorPart}.{version.FileMinorPart}/GpScanner-Setup.txt");
                Settings.Default.LastCheckDate = DateTime.Now;
                Settings.Default.Save();
            },
            parameter => Policy.CheckPolicy(nameof(CheckUpdate)));

        SavePatchProfile = new RelayCommand<object>(
            parameter =>
            {
                string profile = $"{PatchTag}|{PatchFileName}";
                _ = Settings.Default.PatchCodes.Add(profile);
                Settings.Default.Save();
                Settings.Default.Reload();
            },
            parameter => !string.IsNullOrWhiteSpace(PatchFileName) && !string.IsNullOrWhiteSpace(PatchTag) && !Settings.Default.PatchCodes.Cast<string>().Select(z => z.Split('|')[0]).Contains(PatchTag) && TwainCtrl.FileNameValid(PatchFileName));

        AddFtpSites = new RelayCommand<object>(
            parameter =>
            {
                string profile = $"{FtpSite}|{FtpUserName}|{FtpPassword.Encrypt()}";
                _ = Settings.Default.FtpSites.Add(profile);
                Settings.Default.Save();
                Settings.Default.Reload();
            },
            parameter => !string.IsNullOrWhiteSpace(FtpSite));

        RemoveSelectedFtp = new RelayCommand<object>(
            parameter =>
            {
                string ftpSiteToRemove = parameter as string;
                Settings.Default.FtpSites.Remove(ftpSiteToRemove);
                FtpSite = FtpUserName = FtpPassword = Settings.Default.SelectedFtp = string.Empty;
                Settings.Default.Save();
                Settings.Default.Reload();
            },
            parameter => true);

        AddAdditionalIndexFolder = new RelayCommand<object>(
            parameter =>
            {
                string folderpath = FolderDialog.SelectFolder($"{Translation.GetResStringValue("GRAPHFOLDER")}\n{Translation.GetResStringValue("UNINDEXED")}", new WindowInteropHelper(windowService.GetActiveWindow()).Handle);
                if (!string.IsNullOrEmpty(folderpath) && !Settings.Default.AdditionalIndexFolders.Contains(folderpath))
                {
                    _ = Settings.Default.AdditionalIndexFolders.Add(folderpath);
                    Settings.Default.Save();
                    Settings.Default.Reload();
                    ExtendedMessageBox extendedMessageBox = new();
                    extendedMessageBox.ShowDialog(windowService.GetActiveWindow(), Translation.GetResStringValue("RESTARTAPP"), AppName);
                }
            },
            parameter => true);

        RemoveAdditionalIndexFolder = new RelayCommand<object>(
            parameter =>
            {
                if (MessageBox.Show($"{Translation.GetResStringValue("DELETE")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    string folderpath = parameter as string;
                    Settings.Default.AdditionalIndexFolders?.Remove(folderpath);
                    Settings.Default.Save();
                    Settings.Default.Reload();
                }
            },
            parameter => Settings.Default.AdditionalIndexFolders?.Count > 0);

        UploadSharePoint = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is Scanner scanner && File.Exists(scanner.FileName))
                {
                    using ClientContext clientContext = new(Settings.Default.SharePointUrl);
                    clientContext.Credentials = new NetworkCredential(Settings.Default.SharePointUserName, Settings.Default.SharePointUserPassword);
                    FileCreationInformation fileCreationInformation = new() { Url = Path.GetFileName(scanner.FileName), Overwrite = true, Content = File.ReadAllBytes(scanner.FileName) };
                    Web web = clientContext.Web;
                    List list = web.Lists.GetByTitle(Settings.Default.SharePointLibraryName);
                    _ = list.RootFolder.Files.Add(fileCreationInformation);
                    clientContext.ExecuteQuery();
                }
            },
            parameter => !string.IsNullOrWhiteSpace(Settings.Default.SharePointLibraryName) && IsValidHttpAddress(Settings.Default.SharePointUrl));

        SaveQrImage = new RelayCommand<object>(
            parameter =>
            {
                SaveFileDialog saveFileDialog = new() { Filter = "Png Resmi (*.png)|*.png", FileName = "QR" };
                if (saveFileDialog.ShowDialog() == true)
                {
                    BitmapFrame bitmapFrame = BitmapFrame.Create(parameter as WriteableBitmap);
                    bitmapFrame.Freeze();
                    File.WriteAllBytes(saveFileDialog.FileName, bitmapFrame.ToTiffJpegByteArray(Format.Png));
                }
            },
            parameter => parameter is WriteableBitmap writeableBitmap && writeableBitmap is not null);

        RemovePatchProfile = new RelayCommand<object>(
            parameter =>
            {
                Settings.Default.PatchCodes.Remove(parameter as string);
                PatchProfileName = null;
                Settings.Default.Save();
                Settings.Default.Reload();
                if (Settings.Default.PatchCodes.Count == 0)
                {
                    PatchFileName = null;
                    PatchTag = null;
                }
            },
            parameter => true);

        ModifyGridWidth = new RelayCommand<object>(
            parameter =>
            {
                switch (parameter)
                {
                    case "0":
                        MainWindowDocumentGuiControlLength = new GridLength(1, GridUnitType.Star);
                        MainWindowGuiControlLength = new GridLength(3, GridUnitType.Star);
                        return;

                    case "1":
                        MainWindowDocumentGuiControlLength = new GridLength(1, GridUnitType.Star);
                        MainWindowGuiControlLength = new GridLength(0, GridUnitType.Star);
                        return;
                }
            },
            parameter => true);

        SetBatchFolder = new RelayCommand<object>(
            parameter =>
            {
                string path = FolderDialog.SelectFolder(
                    $"{Translation.GetResStringValue("GRAPHFOLDER")}\n{string.Join(" ", BatchImageFileExtensions.Where(z => z.Checked).Select(z => z.Name))}",
                    new WindowInteropHelper(windowService.GetFirstWindow()).Handle);
                if (!string.IsNullOrEmpty(path))
                {
                    BatchFolder = path;
                }
            },
            parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang));

        SetBatchSaveFolder = new RelayCommand<object>(
            parameter =>
            {
                string path = FolderDialog.SelectFolder($"{Translation.GetResStringValue("PDFFOLDER")}", new WindowInteropHelper(windowService.GetFirstWindow()).Handle);
                if (!string.IsNullOrEmpty(path))
                {
                    Settings.Default.BatchSaveFolder = path;
                }
            },
            parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang));

        BatchFolderTümünüİşaretle = new RelayCommand<object>(
            parameter =>
            {
                foreach (TessFiles item in BatchFolderProcessedFileList)
                {
                    item.Checked = true;
                }
            },
            parameter => BatchFolderProcessedFileList?.Count > 0);

        BatchFolderTümünüİşaretiniKaldır = new RelayCommand<object>(
            parameter =>
            {
                foreach (TessFiles item in BatchFolderProcessedFileList)
                {
                    item.Checked = false;
                }
            },
            parameter => BatchFolderProcessedFileList?.Count > 0);

        BatchFolderTümünüKaydet = new RelayCommand<object>(
            parameter =>
            {
                BatchFolderTümünüİşaretle.Execute(null);
                BatchMergeSelectedFiles.Execute(null);
            },
            parameter => BatchFolderProcessedFileList?.Count > 0);

        BatchFolderTümünüSil = new RelayCommand<object>(
            parameter =>
            {
                SelectedBatchFile = null;
                BatchFolderProcessedFileList?.Clear();
            },
            parameter => BatchFolderProcessedFileList?.Count > 0);

        SetBatchWatchFolder = new RelayCommand<object>(
            parameter =>
            {
                string path = FolderDialog.SelectFolder($"{Translation.GetResStringValue("BATCHDESC")}", new WindowInteropHelper(windowService.GetFirstWindow()).Handle);
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }
                if (path == Twainsettings.Settings.Default.AutoFolder)
                {
                    ExtendedMessageBox extendedMessageBox = new();
                    extendedMessageBox.ShowDialog(windowService.GetFirstWindow(), Translation.GetResStringValue("NO ACTION"), AppName);
                    return;
                }
                Settings.Default.BatchFolder = path;
                Settings.Default.Save();
            },
            parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang));

        StartPdfBatch = new RelayCommand<object>(
            async parameter =>
            {
                if (Filesavetask?.IsCompleted == false)
                {
                    ExtendedMessageBox extendedMessageBox = new();
                    extendedMessageBox.ShowDialog(windowService.GetFirstWindow(), Translation.GetResStringValue("TASKSRUNNING"), AppName);
                    return;
                }
                BatchFolderProcessedFileList = [];
                InitializeBatchFiles(out List<string> files, out int slicecount, out Scanner scanner, out List<Task> Tasks);
                GC.Collect();
                foreach (List<string> item in TwainCtrl.ChunkBy(files, slicecount))
                {
                    BatchTxtOcr batchTxtOcr = new();
                    Paper paper = ToolBox.Paper;
                    Task task = Task.Run(
                        async () =>
                        {
                            for (int i = 0; i < item.Count; i++)
                            {
                                try
                                {
                                    if (ocrcancellationToken?.IsCancellationRequested == false)
                                    {
                                        string pdffile = Path.ChangeExtension(item.ElementAtOrDefault(i), ".pdf");
                                        ObservableCollection<OcrData> scannedText = scanner?.ApplyPdfSaveOcr == true ? item.ElementAtOrDefault(i).GetOcrData(Settings.Default.DefaultTtsLang) : null;

                                        batchTxtOcr.ProgressValue = (i + 1) / (double)item.Count;
                                        batchTxtOcr.FilePath = Path.GetFileName(item.ElementAtOrDefault(i));
                                        if (Settings.Default.PdfBatchCompress)
                                        {
                                            BitmapFrame bitmapframe = BitmapFrame.Create(new Uri(item.ElementAtOrDefault(i)));
                                            bitmapframe?.Freeze();
                                            using PdfDocument pdfdocument = bitmapframe?.GeneratePdf(scannedText, Format.Jpg, paper, Twainsettings.Settings.Default.JpegQuality, Twainsettings.Settings.Default.ImgLoadResolution);
                                            pdfdocument.Save(pdffile);
                                        }
                                        else
                                        {
                                            using PdfDocument pdfdocument = item.ElementAtOrDefault(i).GeneratePdf(paper, scannedText);
                                            pdfdocument.Save(pdffile);
                                        }
                                        await Application.Current?.Dispatcher?.InvokeAsync(
                                        () =>
                                        {
                                            string file = Path.ChangeExtension(item.ElementAtOrDefault(i), ".pdf");
                                            BatchFolderProcessedFileList.Add(new TessFiles() { Name = file });
                                        });
                                        scanner.PdfSaveProgressValue = BatchTxtOcrs?.Sum(z => z.ProgressValue) / Tasks.Count ?? 0;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    await LogErrorAsync(ex);
                                }
                            }
                        },
                        ocrcancellationToken.Token);
                    BatchTxtOcrs.Add(batchTxtOcr);
                    Tasks.Add(task);
                }

                BatchDialogOpen = true;
                Filesavetask = Task.WhenAll(Tasks);
                await Filesavetask;
                BatchFolderProcessedFileList = OrderBatchFiles(BatchFolderProcessedFileList);
                scanner.PdfSaveProgressValue = 0;
                BatchTxtOcrs?.Clear();
                if (Filesavetask?.IsCompleted == true && Shutdown)
                {
                    ViewModel.Shutdown.DoExitWin(ViewModel.Shutdown.EWX_SHUTDOWN);
                }
            },
            parameter => !string.IsNullOrWhiteSpace(BatchFolder) && !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang));

        BatchMergeSelectedFiles = new RelayCommand<object>(
            async parameter =>
            {
                string[] files = [.. BatchFolderProcessedFileList.Where(z => z.Checked).Select(z => z.Name)];
                if (files.All(z => File.Exists(z)))
                {
                    await files.SavePdfFilesAsync();
                }
                GC.Collect();
            },
            parameter => BatchFolderProcessedFileList?.Any(z => z.Checked) == true);

        CancelBatchOcr = new RelayCommand<object>(
            parameter =>
            {
                if (MessageBox.Show($"{Translation.GetResStringValue("TRANSLATEPENDING")}\n{Translation.GetResStringValue("RESET")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    ocrcancellationToken?.Cancel();
                    BatchTxtOcrs = null;
                }
                GC.Collect();
            },
            parameter => BatchTxtOcrs?.Count > 0);

        StartTxtBatch = new RelayCommand<object>(
            async parameter =>
            {
                if (Filesavetask?.IsCompleted == false)
                {
                    ExtendedMessageBox extendedMessageBox = new();
                    extendedMessageBox.ShowDialog(windowService.GetFirstWindow(), Translation.GetResStringValue("TASKSRUNNING"), AppName);
                    return;
                }

                InitializeBatchFiles(out List<string> files, out int slicecount, out Scanner scanner, out List<Task> Tasks);
                GC.Collect();
                foreach (List<string> item in TwainCtrl.ChunkBy(files, slicecount))
                {
                    BatchTxtOcr batchTxtOcr = new();
                    Task task = Task.Run(
                        async () =>
                        {
                            for (int i = 0; i < item.Count; i++)
                            {
                                try
                                {
                                    if (ocrcancellationToken?.IsCancellationRequested == false)
                                    {
                                        string image = item[i];
                                        string txtfile = Path.ChangeExtension(image, ".txt");
                                        string content = string.Join(" ", image.GetOcrData(Settings.Default.DefaultTtsLang).Select(z => z.Text));
                                        File.WriteAllText(txtfile, content);
                                        batchTxtOcr.ProgressValue = (i + 1) / (double)item.Count;
                                        scanner.PdfSaveProgressValue = BatchTxtOcrs?.Sum(z => z.ProgressValue) / Tasks.Count ?? 0;
                                        batchTxtOcr.FilePath = Path.GetFileName(image);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    await LogErrorAsync(ex);
                                }
                            }
                        },
                        ocrcancellationToken.Token);
                    BatchTxtOcrs.Add(batchTxtOcr);
                    Tasks.Add(task);
                }

                BatchDialogOpen = true;
                Filesavetask = Task.WhenAll(Tasks);
                await Filesavetask;
                scanner.PdfSaveProgressValue = 0;
                BatchTxtOcrs?.Clear();
                if (Filesavetask?.IsCompleted == true && Shutdown)
                {
                    ViewModel.Shutdown.DoExitWin(ViewModel.Shutdown.EWX_SHUTDOWN);
                }
            },
            parameter => !string.IsNullOrWhiteSpace(BatchFolder) && !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang));

        ResetSettings = new RelayCommand<object>(
            parameter =>
            {
                if (MessageBox.Show($"{Translation.GetResStringValue("SETTİNGS")} {Translation.GetResStringValue("RESET")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    Twainsettings.Settings.Default.Reset();
                    Settings.Default.Reset();
                    ExtendedMessageBox extendedMessageBox = new();
                    extendedMessageBox.ShowDialog(windowService.GetActiveWindow(), Translation.GetResStringValue("RESTARTAPP"), AppName);
                }
            });

        CancelOcr = new RelayCommand<object>(parameter => Ocr.Ocr.ocrcancellationToken?.Cancel());

        CancelUnindexedBatchOcr = new RelayCommand<object>(parameter => unindexedfileocrcancellationToken?.Cancel());

        DateBack = new RelayCommand<object>(
            parameter =>
            {
                BaşlangıçTarihi = BaşlangıçTarihi.AddDays(-1);
                BitişTarihi = BitişTarihi.AddDays(-1);
            },
            parameter => BaşlangıçTarihi > DateTime.MinValue && BitişTarihi > DateTime.MinValue);

        DateForward = new RelayCommand<object>(
            parameter =>
            {
                BaşlangıçTarihi = BaşlangıçTarihi.AddDays(1);
                BitişTarihi = BitişTarihi.AddDays(1);
            },
            parameter => BitişTarihi < DateTime.Today && BaşlangıçTarihi < DateTime.Today);

        StartDateBack = new RelayCommand<object>(parameter => BaşlangıçTarihi = BaşlangıçTarihi.AddDays(-1), parameter => BaşlangıçTarihi > DateTime.MinValue);

        StartDateForward = new RelayCommand<object>(parameter => BaşlangıçTarihi = BaşlangıçTarihi.AddDays(1), parameter => BaşlangıçTarihi < BitişTarihi);

        EndDateBack = new RelayCommand<object>(parameter => BitişTarihi = BitişTarihi.AddDays(-1), parameter => BitişTarihi > BaşlangıçTarihi);

        EndDateForward = new RelayCommand<object>(parameter => BitişTarihi = BitişTarihi.AddDays(1), parameter => BitişTarihi < DateTime.Today);

        CycleSelectedDocuments = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is not ListBox listBox)
                {
                    return;
                }
                List<Scanner> listboxFiles = [.. MainWindow.cvs.View.OfType<Scanner>()];
                Scanner currentFile = listboxFiles.Where(z => z.Seçili).ElementAtOrDefault(cycleIndex);
                if (currentFile is null)
                {
                    return;
                }
                listBox.ScrollIntoView(currentFile);
                currentFile.BorderAnimation = true;
                cycleIndex = (cycleIndex + 1) % listboxFiles.Count(z => z.Seçili);
                await Task.Delay(1000);
                currentFile.BorderAnimation = false;
            },
            parameter => MainWindow.cvs?.View?.OfType<Scanner>().Count(z => z.Seçili) > 0);

        PrintImage = new RelayCommand<object>(parameter => PdfViewer.PdfViewer.PrintImageSource(parameter as ImageSource, 300, false), parameter => parameter is ImageSource);

        PlayAudio = new RelayCommand<object>(parameter => TwainCtrl.PlayNotificationSound(parameter as string), parameter => true);

        FocusControl = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is UIElement uIElement)
                {
                    _ = uIElement.Focus();
                }
            },
            parameter => true);

        PlayAnimation = new RelayCommand<object>(
            parameter =>
            {
                Fold = 0.3;
                Mirror = 0;
                GenerateAnimationTimer();
            },
            parameter => true);

        StopFlagAnimation = new RelayCommand<object>(
            parameter =>
            {
                flaganimationtimer?.Stop();
                FlagProgress = 0;
            },
            parameter => true);

        AddToCalendar = new RelayCommand<object>(
            parameter =>
            {
                using (AppDbContext context = new())
                {
                    ReminderData reminderData = new() { Açıklama = CalendarDesc, Tarih = NotifyDate, FileName = (parameter as Scanner)?.FileName };
                    _ = context.ReminderData.Add(reminderData);
                    _ = context.SaveChanges();
                    ScannerData.Reminder.Add(reminderData);
                }
                CalendarDesc = null;
            },
            parameter => parameter is Scanner scanner && File.Exists(scanner?.FileName) && !string.IsNullOrWhiteSpace(CalendarDesc));

        ApplyCalendarData = new RelayCommand<object>(
            async parameter =>
            {
                using (AppDbContext context = new())
                {
                    context.Entry(SelectedReminder).State = EntityState.Modified;
                    _ = context.SaveChanges();
                }
                ScannerData.Reminder = await ReminderYükle();
                ScannerData.GörülenReminder = await GörülenReminderYükle();
            },
            parameter => SelectedReminder is not null);

        UndoApplyCalendarData = new RelayCommand<object>(
            parameter =>
            {
                SelectedReminder.Seen = false;
                if (ApplyCalendarData.CanExecute(SelectedReminder))
                {
                    ApplyCalendarData.Execute(SelectedReminder);
                }
            },
            parameter => SelectedReminder is not null && SelectedReminder.Tarih > DateTime.Today);

        LoadContributionData = new RelayCommand<object>(parameter => OnPropertyChanged(nameof(SelectedContributionYear)), parameter => true);

        AssociateExtension = new RelayCommand<object>(
            parameter =>
            {
                if (MessageBox.Show(Translation.GetResStringValue("ASSOCIATE"), AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    CreateFileAssociationCurrentUser(".eyp", "Elektronik Yazışma Paketi", Process.GetCurrentProcess()?.MainModule?.FileName);
                }
            },
            parameter => true);

        OpenSettings = new RelayCommand<object>(
            parameter =>
            {
                if (windowService.GetFirstWindow().DataContext is GpScannerViewModel gpScannerViewModel)
                {
                    SettingsWindowView settingsWindowView = windowService.GetFirstWindow<SettingsWindowView>();
                    if (settingsWindowView is not null)
                    {
                        _ = settingsWindowView.Activate();
                        return;
                    }
                    gpScannerViewModel.GenerateFlagAnimation();
                    SettingsWindowView settingswindow = new() { Owner = windowService.GetFirstWindow(), DataContext = gpScannerViewModel };
                    settingswindow.Closed += (s, e) => gpScannerViewModel.StopFlagAnimation.Execute(null);
                    _ = settingswindow.ShowDialog();
                }
            },
            parameter => Policy.CheckPolicy(nameof(OpenSettings)));

        ClearPdfCompressorBatchList = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is ObservableCollection<BatchPdfData> list)
                {
                    list.Clear();
                }
            },
            parameter => parameter is ObservableCollection<BatchPdfData> list && list.Count > 0);

        LoadErrorEvents = new RelayCommand<object>(
            parameter =>
            {
                ErrorLogPath = $@"{ProfileFolder}\{ErrorFile}";
                if (File.Exists(ErrorLogPath))
                {
                    return;
                }

                OpenFileDialog openFileDialog = new() { Filter = "Log Dosyası (*.log)|*.log", Multiselect = false };
                if (openFileDialog.ShowDialog() != true)
                {
                    return;
                }

                ErrorLogPath = openFileDialog.FileName;
            },
            parameter => true);

        SaveZipErrorEvents = new RelayCommand<object>(
            parameter =>
            {
                SaveFileDialog saveFileDialog = new() { Filter = "Zip Dosyası(*.zip)|*.zip", FileName = "Error.zip" };
                if (saveFileDialog.ShowDialog() == true)
                {
                    CreateSingleZipFile(ErrorLogPath, saveFileDialog.FileName);
                }
            },
            parameter => File.Exists(ErrorLogPath));

        LoadGroupFilesMonth = new RelayCommand<object>(
            async parameter =>
            {
                List<ScannerFileDatas> files = await GetContributionFilesAsync();
                ObservableCollection<ContributionData> contributiondata = await GetContributionData(files, new DateTime(DateTime.Now.Year, 1, 1), new DateTime(DateTime.Now.Year, 12, 31));
                YearlyGroupData = contributiondata?.GroupBy(z => z.ContrubutionDate.Value.Month);
            },
            parameter => true);

        RunSqliteCommand = new RelayCommand<object>(
            async parameter =>
            {
                try
                {
                    using AppDbContext context = new();
                    if (context.Database.Exists())
                    {
                        if (IsSqlQuery)
                        {
                            SqlQueryData = await context.Database.SqlQuery<Data>(SqlText).ToListAsync();
                            return;
                        }
                        _ = await context.Database.ExecuteSqlCommandAsync(SqlText);
                        _ = await context.SaveChangesAsync();
                        SqlText = "Select * From Data;";
                        SqlQueryData = await context.Database.SqlQuery<Data>(SqlText).ToListAsync();
                    }
                }
                catch (Exception ex)
                {
                    throw new ArgumentException(ex?.Message);
                }
            },
            parameter => File.Exists(Settings.Default.DatabaseFile) && !string.IsNullOrWhiteSpace(SqlText) && !sqlitedangerouscommands.Any(SqlText.ToLower().Contains));

        RunSqliteVacuumCommand = new RelayCommand<object>(
            async parameter =>
            {
                try
                {
                    using AppDbContext context = new();
                    if (context.Database.Exists())
                    {
                        _ = await context.Database.ExecuteSqlCommandAsync(TransactionalBehavior.DoNotEnsureTransaction, "VACUUM;");
                        _ = await context.SaveChangesAsync();
                        ExtendedMessageBox extendedMessageBox = new();
                        extendedMessageBox.ShowDialog(windowService.GetActiveWindow(), Translation.GetResStringValue("SUCCESS"), AppName);
                    }
                }
                catch (Exception ex)
                {
                    throw new ArgumentException(ex?.Message);
                }
            },
            parameter => File.Exists(Settings.Default.DatabaseFile));

        AddPdfGroupFilesMonthToControlPanel = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is object[] obj && obj[0] is string filepath && File.Exists(filepath) && obj[1] is TwainCtrl twainCtrl)
                {
                    await twainCtrl.AddFiles([filepath], twainCtrl.DecodeHeight);
                }
            },
            parameter => true);

        MonthZipFile = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is not IGrouping<int, ContributionData> data)
                {
                    return;
                }
                string monthname = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(data.Key);
                SaveFileDialog saveFileDialog = new() { Filter = "Zip Dosyası(*.zip)|*.zip", FileName = $"{monthname} {Translation.GetResStringValue("MERGE")}" };
                if (saveFileDialog.ShowDialog() == true)
                {
                    await Task.Run(
                        () =>
                        {
                            List<string> zippedfiles = [.. data.Where(z => z.Count > 0).SelectMany(z => ((ExtendedContributionData)z).Name)];
                            using ZipArchive archive = ZipFile.Open(saveFileDialog.FileName, ZipArchiveMode.Update);
                            for (int i = 0; i < zippedfiles.Count; i++)
                            {
                                string fPath = zippedfiles[i];
                                _ = archive.CreateEntryFromFile(fPath, Path.GetFileName(fPath));
                            }
                            ZipProgressIndeterminate = true;
                        });
                    ZipProgressIndeterminate = false;
                }
                YearlyGroupData = null;
            },
            parameter => parameter is IGrouping<int, ContributionData> data && data.Count(z => z.Count > 0) > 0);

        SetDbBackUpFolder = new RelayCommand<object>(
            parameter =>
            {
                string path = FolderDialog.SelectFolder($"{Translation.GetResStringValue("AUTOFOLDER")}\n{Translation.GetResStringValue("BACKUPDB")}", new WindowInteropHelper(windowService.GetActiveWindow()).Handle, Settings.Default.DataBaseBackUpFolder);
                if (!string.IsNullOrEmpty(path))
                {
                    DriveInfo driveInfo = new(path);
                    if (driveInfo.DriveType == DriveType.CDRom)
                    {
                        ExtendedMessageBox extendedMessageBox = new();
                        extendedMessageBox.ShowDialog(windowService.GetActiveWindow(), $"{Translation.GetResStringValue("ERROR")}\n{Translation.GetResStringValue("INVALIDFILENAME")}", AppName);
                        return;
                    }
                    Settings.Default.DataBaseBackUpFolder = path;
                }
            },
            parameter => true);

        SetDateToday = new RelayCommand<object>(parameter => BaşlangıçTarihi = BitişTarihi = DateTime.Today, parameter => BaşlangıçTarihi != DateTime.Today || BitişTarihi != DateTime.Today);

        ApplyCustomSize = new RelayCommand<object>(parameter => SelectedSize = new Size(Settings.Default.CustomWidth, Settings.Default.CustomHeight), parameter => true);

        CloseApp = new RelayCommand<object>(parameter =>
        {
            Window window = windowService.GetLastWindow();
            if (window?.GetFirstVisualChild<Grid>()?.Children?.OfType<ExtendedMessageBox>()?.Any() == true)
            {
                return;
            }
            window?.Close();
        }, parameter => true);

        AddEsclData = new RelayCommand<object>(
            async parameter =>
            {
                XDocument xDocument = await ESCLScanner.GetScannerCapabilitiesAsync($"{EsclUrl}:{EsclPort}");
                if (xDocument is not null)
                {
                    string scannerName = xDocument?.Root?.Element("ScannerCapabilities")?.Element("ScannerConfiguration")?.Element("Manufacturer")?.Value;
                    scannerName ??= xDocument?.Root?.Element("ScannerCapabilities")?.Element("ScannerConfiguration")?.Element("Model")?.Value;
                    string esclscanner = $"{scannerName}|{EsclUrl}:{EsclPort}";
                    if (!Twainsettings.Settings.Default.EsclScanners.Contains(esclscanner))
                    {
                        _ = Twainsettings.Settings.Default.EsclScanners.Add(esclscanner);
                        Twainsettings.Settings.Default.Save();
                        Twainsettings.Settings.Default.Reload();
                    }
                }
            },
            parameter => true);

        InstallPortablePackage = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new() { FileName = "GpScannerPortable.zip", Filter = "Zip Package (*.zip)|*.zip", Multiselect = false };
                if (openFileDialog.ShowDialog() == true)
                {
                    string extractpath = $"{Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)}";
                    TwainCtrl.ExtractAndHandleFiles(openFileDialog.FileName, extractpath);
                    Shutdown = true;
                }
            },
            parameter => IsAdministrator);

        RemoveEsclScanner = new RelayCommand<object>(
            parameter =>
            {
                Twainsettings.Settings.Default.EsclScanners.Remove(parameter as string);
                Twainsettings.Settings.Default.Save();
                Twainsettings.Settings.Default.Reload();
            },
            parameter => true);

        CheckStatusData = new RelayCommand<object>(
            async parameter =>
            {
                XDocument xDocument = await ESCLScanner.GetScannerStatusAsync($"{EsclUrl}:{EsclPort}");
                if (xDocument is not null)
                {
                    ExtendedMessageBox extendedMessageBox = new();
                    extendedMessageBox.ShowDialog(windowService.GetActiveWindow(), xDocument.ToString(), AppName);
                }
            },
            parameter => true);

        CheckCapabilitiesData = new RelayCommand<object>(
            async parameter =>
            {
                XDocument xDocument = await ESCLScanner.GetScannerCapabilitiesAsync($"{EsclUrl}:{EsclPort}");
                if (xDocument is not null)
                {
                    ExtendedMessageBox extendedMessageBox = new();
                    extendedMessageBox.ShowDialog(windowService.GetActiveWindow(), xDocument.ToString(), AppName);
                }
            },
            parameter => true);

        AppBringToFront = new RelayCommand<object>(
            parameter =>
            {
                windowService.GetFirstWindow()?.Show();
                windowService.GetFirstWindow().WindowState = WindowState.Maximized;
            },
            parameter => true);

        EditWithControlPanel = new RelayCommand<object>(
            parameter =>
            {
                if (windowService.GetActiveWindow() is MainWindow mainWindow && parameter is string filepath)
                {
                    if (string.Equals(Path.GetExtension(filepath), ".xps", StringComparison.InvariantCultureIgnoreCase))
                    {
                        mainWindow.twainCtrl.xpsViewer.XpsDataFilePath = filepath;
                        mainWindow.twainCtrl.SelectedTabIndex = 4;
                        return;
                    }
                    if (string.Equals(Path.GetExtension(filepath), ".docx", StringComparison.InvariantCultureIgnoreCase))
                    {
                        mainWindow.twainCtrl.docxViewer.DocxDataFilePath = filepath;
                        mainWindow.twainCtrl.SelectedTabIndex = 8;
                        return;
                    }
                    if (string.Equals(Path.GetExtension(filepath), ".eyp", StringComparison.InvariantCultureIgnoreCase))
                    {
                        mainWindow.twainCtrl.PdfImportViewer.PdfViewer.EypFilePath = filepath;
                        mainWindow.twainCtrl.SelectedTabIndex = 3;
                        return;
                    }
                    if (new string[] { ".zip", ".rar", ".7z" }.Any(z => string.Equals(z, Path.GetExtension(filepath), StringComparison.InvariantCultureIgnoreCase)))
                    {
                        mainWindow.twainCtrl.ArchiveVwr.ArchivePath = filepath;
                        mainWindow.twainCtrl.SelectedTabIndex = 2;
                        return;
                    }
                    if (BatchImageFileExtensions?.Select(z => z.Name)?.Contains(Path.GetExtension(filepath).ToLowerInvariant()) == true)
                    {
                        BitmapImage bitmapImage = new(new Uri(filepath));
                        bitmapImage?.Freeze();
                        mainWindow.twainCtrl.drawControl.TemporaryImage = bitmapImage;
                        mainWindow.twainCtrl.SelectedTabIndex = 1;
                        return;
                    }
                    if (PdfViewer.PdfViewer.IsValidPdfFile(filepath))
                    {
                        mainWindow.twainCtrl.PdfImportViewer.PdfViewer.PdfFilePath = filepath;
                        mainWindow.twainCtrl.SelectedTabIndex = 3;
                    }
                }
            },
            parameter => true);
    }

    public RelayCommand<object> AddAdditionalIndexFolder { get; }

    public RelayCommand<object> AddEsclData { get; }

    public ICommand AddFtpSites { get; }

    public RelayCommand<object> AddPdfGroupFilesMonthToControlPanel { get; }

    public ICommand AddToCalendar { get; }

    public int AllPdfPage
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(AllPdfPage));
            }
        }
    } = 1;

    public RelayCommand<object> AppBringToFront { get; }

    public RelayCommand<object> ApplyCalendarData { get; }

    public RelayCommand<object> ApplyCustomSize { get; }

    public string AramaMetni
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(AramaMetni));
            }
        }
    }

    public ICommand AssociateExtension { get; }

    public IEnumerable<string> AudioFiles
    {
        get
        {
            string folder = $"{Environment.GetFolderPath(Environment.SpecialFolder.Windows)}\\Media";
            return Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.wav", SearchOption.TopDirectoryOnly) : null;
        }
    }

    public ObservableCollection<string> BarcodeList
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(BarcodeList));
            }
        }
    } = [];

    public DateTime BaşlangıçTarihi
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(BaşlangıçTarihi));
            }
        }
    }

    public bool BatchDialogOpen
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(BatchDialogOpen));
            }
        }
    }

    public string BatchFolder
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(BatchFolder));
            }
        }
    }

    public ObservableCollection<TessFiles> BatchFolderProcessedFileList
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(BatchFolderProcessedFileList));
            }
        }
    }

    public RelayCommand<object> BatchFolderTümünüİşaretiniKaldır { get; }

    public RelayCommand<object> BatchFolderTümünüİşaretle { get; }

    public RelayCommand<object> BatchFolderTümünüKaydet { get; }

    public RelayCommand<object> BatchFolderTümünüSil { get; }

    public ObservableCollection<TessFiles> BatchImageFileExtensions
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(BatchImageFileExtensions));
            }
        }
    } = [
        new TessFiles() { Name = ".tiff", Checked = true },
        new TessFiles() { Name = ".tif", Checked = true },
        new TessFiles() { Name = ".jpg", Checked = true },
        new TessFiles() { Name = ".jpe", Checked = true },
        new TessFiles() { Name = ".gif", Checked = true },
        new TessFiles() { Name = ".jpeg", Checked = true },
        new TessFiles() { Name = ".jfif", Checked = true },
        new TessFiles() { Name = ".png", Checked = true },
        new TessFiles() { Name = ".bmp", Checked = true },
        new TessFiles() { Name = ".jb2", Checked = true },
        new TessFiles() { Name = ".webp", Checked = false, Enabled = WebP.WebpDllExists },
        ];

    public RelayCommand<object> BatchMergeSelectedFiles { get; }

    public ObservableCollection<BatchTxtOcr> BatchTxtOcrs
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(BatchTxtOcrs));
            }
        }
    }

    public DateTime BitişTarihi
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(BitişTarihi));
            }
        }
    }

    public ObservableCollection<string> BurnFiles
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(BurnFiles));
            }
        }
    } = [];

    public string CalendarDesc
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CalendarDesc));
            }
        }
    }

    public XmlLanguage CalendarLang
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CalendarLang));
            }
        }
    }

    public bool CalendarPanelIsExpanded
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CalendarPanelIsExpanded));
            }
        }
    }

    public ICommand CancelBatchOcr { get; }

    public ICommand CancelOcr { get; }

    public RelayCommand<object> CancelUnindexedBatchOcr { get; }

    public ICommand ChangeDataFolder { get; }

    public RelayCommand<object> CheckCapabilitiesData { get; }

    public int CheckedPdfCount
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CheckedPdfCount));
            }
        }
    }

    public RelayCommand<object> CheckStatusData { get; }

    public ICommand CheckUpdate { get; }

    public RelayCommand<object> ClearPdfCompressorBatchList { get; }

    public RelayCommand<object> CloseApp { get; }

    public ObservableCollection<BatchPdfData> CompressedFiles
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CompressedFiles));
            }
        }
    } = [];

    public List<Size> CompressorList { get; } = new List<Size>() { { new Size(72, 80) }, { new Size(96, 75) }, { new Size(120, 70) }, { new Size(150, 65) }, { new Size(200, 60) }, };

    public ObservableCollection<ContributionData> ContributionData
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ContributionData));
            }
        }
    }

    public int ContributionDocumentCount
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ContributionDocumentCount));
            }
        }
    }

    public double ContributionPreviewSize
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ContributionPreviewSize));
            }
        }
    } = 120;

    public ICommand CycleSelectedDocuments { get; }

    public ICommand DateBack { get; }

    public ICommand DateForward { get; }

    public bool DetectBarCode
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DetectBarCode));
            }
        }
    } = true;

    public bool DocumentPanelIsExpanded
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DocumentPanelIsExpanded));
            }
        }
    }

    public ObservableCollection<Scanner> Dosyalar
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Dosyalar));
            }
        }
    }

    public RelayCommand<object> EditWithControlPanel { get; }

    public RelayCommand<object> EndDateBack { get; }

    public RelayCommand<object> EndDateForward { get; }

    public string Error => string.Empty;

    public string ErrorLogPath
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ErrorLogPath));
            }
        }
    }

    public string EsclPort
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(EsclPort));
            }
        }
    }

    public string EsclUrl
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(EsclUrl));
            }
        }
    }

    public ICommand ExploreFile { get; }

    public double FileLoadProgress
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FileLoadProgress));
            }
        }
    }

    public ObservableCollection<Chart> FilesChartList
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FilesChartList));
            }
        }
    }

    public ObservableCollection<string> FileSystemWatcherProcessedFileList
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FileSystemWatcherProcessedFileList));
            }
        }
    }

    public int FlagProgress
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FlagProgress));
            }
        }
    }

    public RelayCommand<object> FocusControl { get; }

    public double Fold
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Fold));
            }
        }
    } = 0.3;

    public string FtpPassword
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FtpPassword));
            }
        }
    } = string.Empty;

    public string FtpSite
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FtpSite));
            }
        }
    } = string.Empty;

    public string FtpUserName
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FtpUserName));
            }
        }
    } = string.Empty;

    public RelayCommand<object> GridSplitterMouseDoubleClick { get; }

    public RelayCommand<object> GridSplitterMouseRightButtonDown { get; }

    public int IndexedFileCount
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(IndexedFileCount));
            }
        }
    }

    public RelayCommand<object> InstallPortablePackage { get; }

    public bool IsAdministrator
    {
        get
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            field = principal.IsInRole(WindowsBuiltInRole.Administrator);
            return field;
        }
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(IsAdministrator));
            }
        }
    }

    public bool IsSqlQuery
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(IsSqlQuery));
            }
        }
    } = true;

    public FlowDirection LangFlowDirection
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(LangFlowDirection));
            }
        }
    } = FlowDirection.LeftToRight;

    public bool ListBoxBorderAnimation
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ListBoxBorderAnimation));
            }
        }
    }

    public RelayCommand<object> LoadContributionData { get; }

    public RelayCommand<object> LoadErrorEvents { get; }

    public RelayCommand<object> LoadFiles { get; }

    public RelayCommand<object> LoadGroupFilesMonth { get; }

    public RelayCommand<object> LoadRemainder { get; }

    public RelayCommand<object> LoadUnindexedFiles { get; }

    public GridLength MainWindowDocumentGuiControlLength
    {
        get => mainWindowDocumentGuiControlLength;

        set
        {
            if (mainWindowDocumentGuiControlLength != value)
            {
                mainWindowDocumentGuiControlLength = value;
                OnPropertyChanged(nameof(MainWindowDocumentGuiControlLength));
            }
        }
    }

    public GridLength MainWindowGuiControlLength
    {
        get => mainWindowGuiControlLength;

        set
        {
            if (mainWindowGuiControlLength != value)
            {
                mainWindowGuiControlLength = value;
                OnPropertyChanged(nameof(MainWindowGuiControlLength));
            }
        }
    }

    public double Mirror
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Mirror));
            }
        }
    }

    public ICommand ModifyGridWidth { get; }

    public RelayCommand<object> MonthZipFile { get; }

    public bool NetFxVersionSupported
    {
        get
        {
            using RegistryKey ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
            return (int?)(ndpKey?.GetValue("Release")) > NetFxMinVersion;
        }
    }

    public DateTime NotifyDate
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(NotifyDate));
            }
        }
    } = DateTime.Today;

    public bool OcrAllPdfPages
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(OcrAllPdfPages));
            }
        }
    }

    public double OcrAllPdfPagesProgress
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(OcrAllPdfPagesProgress));
            }
        }
    }

    public bool OcrIsBusy
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(OcrIsBusy));
            }
        }
    }

    public ICommand OcrPage { get; }

    public ICommand OcrPdfThumbnailPage { get; }

    public int? OcrPdfThumbnailPageNumber
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(OcrPdfThumbnailPageNumber));
            }
        }
    }

    public ICommand OpenOriginalFile { get; }

    public RelayCommand<object> OpenSettings { get; }

    public string PatchFileName
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PatchFileName));
            }
        }
    }

    public string PatchProfileName
    {
        get => field?.Split('|')[0];

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PatchProfileName));
            }
        }
    }

    public string PatchTag
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PatchTag));
            }
        }
    }

    public PdfViewer.FitImageOrientation PdfAllPageOrientation
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfAllPageOrientation));
            }
        }
    } = PdfViewer.FitImageOrientation.Width;

    public bool PdfBatchRunning
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfBatchRunning));
            }
        }
    }

    public ICommand PdfBirleştir { get; }

    public bool PdfCompressorItemToggle
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfCompressorItemToggle));
            }
        }
    }

    public double PdfMergeProgressValue
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfMergeProgressValue));
            }
        }
    }

    public RelayCommand<object> PdfZipBirleştir { get; }

    public RelayCommand<object> PlayAnimation { get; }

    public RelayCommand<object> PlayAudio { get; }

    public ICommand PrintImage { get; }

    public Brush ProgressBarForegroundBrush
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ProgressBarForegroundBrush));
            }
        }
    } = Brushes.Green;

    public ICommand RegisterSti { get; }

    public RelayCommand<object> RemoveAdditionalIndexFolder { get; }

    public RelayCommand<object> RemoveEsclScanner { get; }

    public ICommand RemovePatchProfile { get; }

    public ICommand RemoveSelectedFtp { get; }

    public ICommand ResetSettings { get; }

    public double Ripple
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Ripple));
            }
        }
    }

    public RelayCommand<object> RunSqliteCommand { get; }

    public RelayCommand<object> RunSqliteVacuumCommand { get; }

    public List<string[]> SampleSqlQueryLists { get; } = [["1", "Select * From Data;"], ["2", "Select * From Data Where FileContent='';"], ["3", "Select * From Data Where QrData='';"],];

    public ICommand SavePatchProfile { get; }

    public ICommand SaveQrImage { get; }

    public RelayCommand<object> SaveZipErrorEvents { get; }

    public ObservableCollection<OcrData> ScannedText
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ScannedText));
            }
        }
    } = [];

    public ScannerData ScannerData { get; set; }

    public ObservableCollection<CheckBoxItem> SearchArchiveTypes
    {
        get;
        set;
    } = [new() { Name = ".zip", IsChecked = true }, new() { Name = ".rar", IsChecked = true }, new() { Name = ".7z", IsChecked = true }, new() { Name = ".tar", IsChecked = true }, new() { Name = ".gzip", IsChecked = true }, new()
    {
        Name = ".arj",
        IsChecked = true
    }];

    public string SeçiliDil
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SeçiliDil));
            }
        }
    }

    public TessFiles SelectedBatchFile
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedBatchFile));
            }
        }
    }

    public Size SelectedCompressorProfile
    {
        get => selectedCompressorProfile;
        set
        {
            if (selectedCompressorProfile != value)
            {
                selectedCompressorProfile = value;
                OnPropertyChanged(nameof(SelectedCompressorProfile));
            }
        }
    }

    public ContributionData SelectedContribution
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedContribution));
            }
        }
    }

    public int SelectedContributionYear
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedContributionYear));
            }
        }
    } = DateTime.Now.Year;

    public string SelectedFtp
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedFtp));
            }
        }
    }

    public ReminderData SelectedReminder
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedReminder));
            }
        }
    }

    public Size SelectedSize
    {
        get => selectedSize;

        set
        {
            if (selectedSize != value)
            {
                selectedSize = value;
                OnPropertyChanged(nameof(SelectedSize));
            }
        }
    }

    public ICommand SetBatchFolder { get; }

    public RelayCommand<object> SetBatchSaveFolder { get; }

    public ICommand SetBatchWatchFolder { get; }

    public RelayCommand<object> SetDateToday { get; }

    public RelayCommand<object> SetDbBackUpFolder { get; }

    public int[] SettingsPagePdfDpiList { get; } = PdfViewer.PdfViewer.DpiList;

    public int[] SettingsPagePictureResizeList { get; } = [.. Enumerable.Range(5, 100).Where(z => z % 5 == 0)];

    public bool ShowUnindexedFileWarn
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ShowUnindexedFileWarn));
            }
        }
    }

    public bool Shutdown
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Shutdown));
            }
        }
    }

    public List<Data> SqlQueryData
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SqlQueryData));
            }
        }
    }

    public string SqlText
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SqlText));
            }
        }
    } = string.Empty;

    public RelayCommand<object> StartDateBack { get; }

    public RelayCommand<object> StartDateForward { get; }

    public ICommand StartPdfBatch { get; }

    public ICommand StartTxtBatch { get; }

    public RelayCommand<object> StopFlagAnimation { get; }

    public ICommand Tersiniİşaretle { get; }

    public bool TesseractAnyLanguageSelected => TesseractViewModel?.TesseractFiles?.Count(z => z.Checked) > 0;

    public bool TesseractOrientationLanguageAvailable => TesseractViewModel?.TesseractFiles?.Any(z => z.Name == "osd") == true;

    public TesseractViewModel TesseractViewModel
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(TesseractViewModel));
            }
        }
    }

    public bool TesseractVisualCRuntimeInstalled => CheckFileVersion($@"{Environment.SystemDirectory}\msvcp140.dll") > new Version(MinimumVcVersion);

    public long TotalFileSize
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(TotalFileSize));
            }
        }
    }

    public TranslateViewModel TranslateViewModel
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(TranslateViewModel));
            }
        }
    }

    public ICommand Tümünüİşaretle { get; }

    public ICommand TümününİşaretiniKaldır { get; }

    public TwainCtrl TwainCtrl { get; }

    public RelayCommand<object> UndoApplyCalendarData { get; }

    public ObservableCollection<string> UnIndexedFiles
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(UnIndexedFiles));
            }
        }
    }

    public RelayCommand<object> UnindexedAllFilesOcr { get; }

    public RelayCommand<object> UnindexedFileOcr { get; }

    public ICommand UnRegisterSti { get; }

    public RelayCommand<object> UploadSharePoint { get; }

    public IWindowService WindowService { get; }

    public RelayCommand<object> WordOcrPdfThumbnailPage { get; }

    public IEnumerable<IGrouping<int, ContributionData>> YearlyGroupData
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(YearlyGroupData));
            }
        }
    }

    public IEnumerable<int> Years { get; } = Enumerable.Range(DateTime.Now.Year - 10, 11);

    public double ZipProgress
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ZipProgress));
            }
        }
    }

    public bool ZipProgressIndeterminate
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ZipProgressIndeterminate));
            }
        }
    }

    public string this[string columnName] => columnName switch
    {
        "IsAdministrator" when !IsAdministrator => "FOLDERACCESS",
        "SqlText" when sqlitedangerouscommands.Any(SqlText.ToLower().Contains) => "ERROR",
        _ => null
    };

    public static void BackupDatabaseFile()
    {
        if (!File.Exists(Settings.Default.DatabaseFile) || !Settings.Default.BackUpDatabase)
        {
            return;
        }
        string databaseFilePath = Settings.Default.DatabaseFile;
        FileInfo fi = new(databaseFilePath);
        string backupFileName = $"{fi.DirectoryName}\\{DateTime.Today.DayOfWeek}.db";
        string backupFolderPath = Settings.Default.DataBaseBackUpFolder;
        if (Directory.Exists(backupFolderPath))
        {
            backupFileName = $"{backupFolderPath}\\{DateTime.Today.DayOfWeek}.db";
        }
        else
        {
            Settings.Default.DataBaseBackUpFolder = string.Empty;
        }
        File.Copy(databaseFilePath, backupFileName, true);
    }

    public static async Task WriteToLogFile(string filePath, string content)
    {
        using StreamWriter writer = new(filePath, true);
        await writer?.WriteLineAsync($"{DateTime.Now} {content}");
    }

    public void AddBarcodeToList(string barcodecontent)
    {
        if (!string.IsNullOrWhiteSpace(barcodecontent))
        {
            BarcodeList?.Add(barcodecontent);
        }
    }

    public string GetFileNameFromPatchCodeResult(string qrcodetag)
    {
        if (string.IsNullOrWhiteSpace(qrcodetag))
        {
            return Translation.GetResStringValue("DEFAULTSCANNAME");
        }

        List<string> patchcodes = [.. Settings.Default.PatchCodes.Cast<string>()];
        string matchingPatchCode = patchcodes?.Find(z => z.Split('|')[0] == qrcodetag);
        return matchingPatchCode?.Split('|')[1] ?? Translation.GetResStringValue("DEFAULTSCANNAME");
    }

    public async Task<ObservableCollection<Scanner>> GetScannerFileData()
    {
        if (!Directory.Exists(Twainsettings.Settings.Default.AutoFolder))
        {
            return [];
        }

        ObservableCollection<Scanner> list = [];
        ConcurrentBag<Scanner> templist = [];
        object _progressLock = new();
        try
        {
            List<string> allfilepaths = [.. Settings.Default.AdditionalIndexFolders.OfType<string>(), Twainsettings.Settings.Default.AutoFolder,];
            return await Task.Run(
                () =>
                {
                    List<string> files = GetAllFilesFromPaths(allfilepaths, file => supportedfilesextension.Contains(Path.GetExtension(file).ToLowerInvariant()));
                    int totalFiles = files.Count;
                    _ = Parallel.ForEach(
                        files,
                        dosya =>
                        {
                            FileInfo fi = new(dosya);
                            if ((fi.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                            {
                                templist.Add(new Scanner { FileName = dosya, FolderName = fi?.Directory?.Name, FileSize = fi.Length / 1048576F });
                            }
                            lock (_progressLock)
                            {
                                FileLoadProgress = templist.Count / (double)totalFiles;
                            }
                        });
                    List<Scanner> list = [.. templist];
                    list.Sort(new ScannerStrCmpLogicalComparer());
                    templist = null;
                    return new ObservableCollection<Scanner>(list);
                });
        }
        catch (UnauthorizedAccessException)
        {
            return list;
        }
    }

    public bool NeedAppUpdate() => Settings.Default.CheckAppUpdate && DateTime.Now > Settings.Default.LastCheckDate.AddDays(Settings.Default.UpdateInterval);

    public void RefreshItems<T>(ObservableCollection<T> collection, Func<T, bool> predicate, Action<T> refreshAction)
    {
        foreach (T item in collection.Where(predicate).ToList())
        {
            refreshAction(item);
        }
    }

    public void RegisterBatchImageFileWatcher(Paper paper, string batchfolder, string batchsavefolder)
    {
        if (!Directory.Exists(batchfolder) || !Directory.Exists(batchsavefolder) || paper is null)
        {
            return;
        }
        FileSystemWatcherProcessedFileList = [];
        FileSystemWatcher watcher = new(batchfolder) { NotifyFilter = NotifyFilters.FileName, Filter = "*.*", IncludeSubdirectories = true, EnableRaisingEvents = true };
        watcher.Created += async (s, e) =>
                           {
                               string currentfilepath = e.FullPath;
                               string currentfilename = e.Name;
                               bool isFileLocked = IsFileLocked(currentfilepath);
                               while (isFileLocked)
                               {
                                   await Task.Delay(100);
                                   isFileLocked = IsFileLocked(currentfilepath);
                               }

                               try
                               {
                                   if (File.Exists(currentfilepath) && BatchImageFileExtensions.Any(z => z.Checked && z.Name == Path.GetExtension(currentfilename).ToLowerInvariant()))
                                   {
                                       await FileSystemWatcherOcrFile(paper, batchsavefolder, currentfilepath, currentfilename);
                                       await Application.Current?.Dispatcher?.InvokeAsync(
                                       () =>
                                       {
                                           string item = $"{batchsavefolder}\\{Path.ChangeExtension(currentfilename, ".pdf")}";
                                           FileSystemWatcherProcessedFileList?.Add(item);
                                       });
                                   }
                               }
                               catch (Exception ex)
                               {
                                   throw new ArgumentException(ex?.Message);
                               }
                           };
    }

    public void ReloadDocumentViewerFiles()
    {
        if (WindowService?.GetActiveWindow()?.DataContext is DocumentViewerModel documentViewerModel)
        {
            string currentfile = documentViewerModel.FilePath;
            CollectionViewSource.GetDefaultView(documentViewerModel.DirectoryAllPdfFiles)?.Refresh();
            documentViewerModel.FilePath = null;
            documentViewerModel.FilePath = currentfile;
        }
    }

    private void AnimationOnTick(object sender, EventArgs e)
    {
        if (StillImageHelper.FirstLanuchScan)
        {
            StopAnimation();
            return;
        }

        switch (Settings.Default.AnimationType)
        {
            case 0:
                Fold -= 0.01;
                if (Fold <= 0)
                {
                    StopAnimation();
                }
                break;
            case 1:
                Ripple++;
                if (Ripple > 100)
                {
                    StopAnimation();
                }
                break;
            case 2:
                Mirror += 0.01;
                if (Mirror > 1)
                {
                    StopAnimation();
                }
                break;
        }

        void StopAnimation()
        {
            Fold = 0;
            Ripple = 0;
            Mirror = 1;
            timer.Stop();
            timer.Tick -= AnimationOnTick;
        }
    }

    private FlowDirection ChangeApplicationFlowDirection(string lang)
    {
        LangFlowDirection = FlowDirection.LeftToRight;
        LangFlowDirection = lang switch
        {
            "عربي" or "فلسطين" or "لبنان" or "ایرانی" => FlowDirection.RightToLeft,
            _ => FlowDirection.LeftToRight
        };
        return LangFlowDirection;
    }

    private Version CheckFileVersion(string filepath) => File.Exists(filepath) ? new Version(FileVersionInfo.GetVersionInfo(filepath).FileVersion) : null;

    private void CreateEmptySqliteDatabase()
    {
        string databaseFilePath = Settings.Default.DatabaseFile;
        if (!File.Exists(databaseFilePath))
        {
            Settings.Default.DatabaseFile = $@"{ProfileFolder}\Data.db";
        }
        if (File.Exists(databaseFilePath))
        {
            return;
        }
        using AppDbContext context = new();
        _ = context?.Database?
        .ExecuteSqlCommand(
            """
                CREATE TABLE IF NOT EXISTS "Data" (
                	"Id"	INTEGER UNIQUE,
                	"FileName"	TEXT,
                	"QrData"	TEXT,
                	"FileContent"	TEXT,
                	PRIMARY KEY("Id")
                )
                """);
        _ = context?.Database?
        .ExecuteSqlCommand(
            """
                CREATE INDEX IF NOT EXISTS "index" ON "Data" (
                	"FileContent",
                	"FileName"	ASC
                );
                """);
        _ = context?.Database?
        .ExecuteSqlCommand(
            """
                CREATE TABLE IF NOT EXISTS "ReminderDatas" (
                	"Id"	INTEGER UNIQUE,
                	"Açıklama"	TEXT,
                	"Seen"	INTEGER,
                	"Tarih"	INTEGER,
                	"FileName"	TEXT,
                	PRIMARY KEY("Id")
                )
                """);
        Settings.Default.Save();
    }

    private void CreateFileAssociationCurrentUser(string extension, string fileTypeDescription, string applicationPath, int iconindex = 0)
    {
        using RegistryKey extensionKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}");
        extensionKey?.SetValue(string.Empty, fileTypeDescription);
        using RegistryKey fileTypeKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{fileTypeDescription}");
        using RegistryKey iconKey = fileTypeKey?.CreateSubKey("DefaultIcon");
        iconKey?.SetValue(string.Empty, $"{applicationPath},{iconindex}");
        using RegistryKey commandKey = fileTypeKey?.CreateSubKey(@"shell\open\command");
        commandKey?.SetValue(string.Empty, $@"""{applicationPath}"" ""%1""");
    }

    private void CreateSingleZipFile(string filepath, string zipsavepath)
    {
        using ZipArchive archive = ZipFile.Open(zipsavepath, ZipArchiveMode.Update);
        _ = archive.CreateEntryFromFile(filepath, Path.GetFileName(filepath));
    }

    private void Default_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "RegisterBatchWatcher" && Settings.Default.RegisterBatchWatcher && WindowService.GetActiveWindow() is MainWindow mainWindow)
        {
            RegisterBatchImageFileWatcher(mainWindow.twainCtrl.SelectedPaper, Settings.Default.BatchFolder, Settings.Default.BatchSaveFolder);
        }

        if (e.PropertyName is "BatchFolder" or "BatchSaveFolder")
        {
            if (!Directory.Exists(Settings.Default.BatchFolder) || !Directory.Exists(Settings.Default.BatchSaveFolder))
            {
                Settings.Default.RegisterBatchWatcher = false;
            }
        }

        if (e.PropertyName is "StartWithWindows")
        {
            RunAtStartup(Settings.Default.StartWithWindows);
            if (Settings.Default.StartWithWindows)
            {
                Settings.Default.MinimizeTray = true;
                Settings.Default.ShowTrayIcon = true;
            }
        }

        if (e.PropertyName is "ApplyIdleIndexOcr")
        {
            RunIdleIndexOperation();
        }

        Settings.Default.Save();
    }

    private void DrawFileSizeGraph(bool showfilegraph = false)
    {
        if (!showfilegraph || MainWindow.cvs?.View is not ICollectionView collection)
        {
            return;
        }
        foreach (Scanner file in collection)
        {
            FilesChartList.Add(new Chart() { ChartBrush = Brushes.Blue, ChartValue = Math.Round(file.FileSize, 2), Description = Path.GetFileName(file.FileName) });
        }
    }

    private bool ExistsInArchive(string filename, bool apply = false, long filesize = 5_242_880)
    {
        if (apply)
        {
            try
            {
                if (SearchArchiveTypes.Where(z => z.IsChecked).Select(z => z.Name).Contains(Path.GetExtension(filename).ToLowerInvariant()) && new FileInfo(filename).Length <= filesize)
                {
                    using ArchiveFile archive = new(filename);
                    return archive?.Entries?.Any(z => z.FileName.IndexOf(AramaMetni, StringComparison.CurrentCultureIgnoreCase) >= 0) == true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
        return false;
    }

    private async Task FileSystemWatcherOcrFile(Paper paper, string batchsavefolder, string currentfilepath, string currentfilename)
    {
        try
        {
            ObservableCollection<OcrData> scannedText;
            if (string.Equals(Path.GetExtension(currentfilepath), ".webp", StringComparison.OrdinalIgnoreCase))
            {
                byte[] webpfile = currentfilepath.WebpDecode(true, Twainsettings.Settings.Default.ImgLoadResolution).ToTiffJpegByteArray(Format.Jpg);
                scannedText = await webpfile.OcrAsync(Settings.Default.DefaultTtsLang);
                webpfile = null;
            }
            else
            {
                scannedText = await currentfilepath?.OcrAsync(Settings.Default.DefaultTtsLang);
            }
            await Task.Run(
                () =>
                {
                    PdfBatchRunning = true;
                    using (PdfDocument pdfdocument = Settings.Default.PdfBatchCompress
                                                     ? BitmapFrame.Create(new Uri(currentfilepath)).GeneratePdf(scannedText, Format.Jpg, paper, Twainsettings.Settings.Default.JpegQuality, Twainsettings.Settings.Default.ImgLoadResolution)
                                                     : currentfilepath.GeneratePdf(paper, scannedText))
                    {
                        string pdfFileName = Path.ChangeExtension(currentfilename, ".pdf");
                        string pdfFilePath = Path.Combine(batchsavefolder, pdfFileName);
                        pdfdocument.Save(pdfFilePath);
                    }
                    PdfBatchRunning = false;
                });
        }
        catch (Exception ex)
        {
            throw new ArgumentException(ex?.Message);
        }
    }

    private void GenerateAnimationTimer()
    {
        timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(15) };
        timer.Tick += AnimationOnTick;
        timer.Start();
    }

    private void GenerateFlagAnimation()
    {
        if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
        {
            return;
        }
        int direction = 1;
        flaganimationtimer = new(DispatcherPriority.SystemIdle) { Interval = TimeSpan.FromMilliseconds(25) };
        flaganimationtimer.Tick += (sender, e) =>
                                   {
                                       if (FlagProgress >= 85)
                                       {
                                           direction = -1;
                                       }
                                       if (FlagProgress <= 15)
                                       {
                                           direction = 1;
                                       }
                                       FlagProgress += direction;
                                   };
        flaganimationtimer.Start();
    }

    private void GenerateJumpList()
    {
        if (!IsWin7OrAbove())
        {
            return;
        }
        string fileName = Process.GetCurrentProcess().MainModule.FileName;
        FileVersionInfo version = FileVersionInfo.GetVersionInfo(fileName);
        JumpTask update = new()
        {
            IconResourcePath = $@"{Path.GetDirectoryName(fileName)}\twux32.exe",
            Description = $"GPSCANNER {Translation.GetResStringValue("UPDATE")}",
            ApplicationPath = $@"{Path.GetDirectoryName(fileName)}\twux32.exe",
            Arguments = $"https://github.com/goksenpasli/GpScanner/releases/download/{version.FileMajorPart}.{version.FileMinorPart}/GpScanner-Setup.txt",
            Title = Translation.GetResStringValue("UPDATE")
        };
        JumpTask scan = new() { Arguments = "/StiDevice:", Description = Translation.GetResStringValue("SCAN"), ApplicationPath = fileName, Title = Translation.GetResStringValue("SCAN") };
        JumpList list = JumpList.GetJumpList(Application.Current);
        list ??= new JumpList();
        list.ShowRecentCategory = true;
        list.ShowFrequentCategory = true;
        JumpList.SetJumpList(Application.Current, list);
        list?.JumpItems?.Add(update);
        list?.JumpItems?.Add(scan);
        list.Apply();
    }

    private List<string> GetAllFilesFromPaths(List<string> paths, Func<string, bool> filter = null)
    {
        List<string> allFiles = [];
        try
        {
            foreach (string path in paths)
            {
                if (Directory.Exists(path))
                {
                    IEnumerable<string> files = FastFileSearch.EnumerateFilepaths(path);
                    if (filter is not null)
                    {
                        files = files.Where(filter);
                    }
                    allFiles.AddRange(files);
                    files = null;
                }
            }
            return allFiles;
        }
        catch (UnauthorizedAccessException)
        {
            return allFiles;
        }
    }

    private async Task<ObservableCollection<ContributionData>> GetContributionData(List<ScannerFileDatas> files, DateTime first, DateTime last)
    {
        try
        {
            ObservableCollection<ContributionData> contributiondata = [];
            for (DateTime? date = first; date <= last; date = date.Value.AddDays(1))
            {
                if (!files.Select(z => z.ParentDate).Contains(date.Value))
                {
                    contributiondata.Add(new ExtendedContributionData { ContrubutionDate = date, Count = 0 });
                }
            }

            foreach (IGrouping<DateTime, Scanner> file in files.GroupBy(item => item.ParentDate, item => item.Scanner))
            {
                contributiondata.Add(new ExtendedContributionData { Name = file.Select(z => z.FileName), ContrubutionDate = file?.Key, Count = file.Count() });
            }

            IEnumerable<ContributionData> collection = contributiondata.Where(z => z.ContrubutionDate >= first && z.ContrubutionDate <= last).OrderBy(z => z.ContrubutionDate);
            return [.. collection];
        }
        catch (Exception ex)
        {
            await WriteToLogFile($@"{ProfileFolder}\{ErrorFile}", ex?.Message);
        }
        return null;
    }

    private async Task<List<ScannerFileDatas>> GetContributionFilesAsync()
    {
        return await Task.Run(
            () => Dosyalar?.Where(scanner => Directory.GetParent(scanner.FileName) is not null)
            .Select(
                scanner =>
                {
                    string parentDirectoryName = Directory.GetParent(scanner.FileName).Name;
                    _ = DateTime.TryParse(parentDirectoryName, out DateTime parsedDateTime);
                    return new ScannerFileDatas() { Scanner = scanner, ParentDate = parsedDateTime };
                })
            .ToList());
    }

    private Tuple<XmlLanguage, string> GetLanguageSettings(string lang)
    {
        return lang switch
        {
            "" or "TÜRKÇE" => Tuple.Create(XmlLanguage.GetLanguage("tr-TR"), "Turkish"),
            "ENGLISH" => Tuple.Create(XmlLanguage.GetLanguage("en-US"), "English"),
            "FRANÇAIS" => Tuple.Create(XmlLanguage.GetLanguage("fr-FR"), "French"),
            "ITALIANO" => Tuple.Create(XmlLanguage.GetLanguage("it-IT"), "Italian"),
            "عربي" or "فلسطين" or "لبنان" => Tuple.Create(XmlLanguage.GetLanguage("ar-AR"), "Arabic"),
            "РУССКИЙ" => Tuple.Create(XmlLanguage.GetLanguage("ru-RU"), "Russian"),
            "DEUTSCH" => Tuple.Create(XmlLanguage.GetLanguage("de-DE"), "German"),
            "日本" => Tuple.Create(XmlLanguage.GetLanguage("ja-JP"), "Japanese"),
            "DUTCH" or "BELGIË" => Tuple.Create(XmlLanguage.GetLanguage("nl-NL"), "Dutch"),
            "CZECH" => Tuple.Create(XmlLanguage.GetLanguage("cs-CZ"), "Czech"),
            "ESPAÑOL" => Tuple.Create(XmlLanguage.GetLanguage("es-ES"), "Spanish"),
            "中國人" => Tuple.Create(XmlLanguage.GetLanguage("zh-CN"), "Chinese"),
            "УКРАЇНСЬКА" => Tuple.Create(XmlLanguage.GetLanguage("uk-UA"), "Ukrainian"),
            "ΕΛΛΗΝΙΚΑ" => Tuple.Create(XmlLanguage.GetLanguage("el"), "Greek"),
            "AZƏRBAYCAN" => Tuple.Create(XmlLanguage.GetLanguage("az"), "Azerbaijani"),
            "БЕЛАРУСКАЯ" => Tuple.Create(XmlLanguage.GetLanguage("be"), "Belarusian"),
            "БЪЛГАРСКИ" => Tuple.Create(XmlLanguage.GetLanguage("bg"), "Bulgarian"),
            "DANSK" => Tuple.Create(XmlLanguage.GetLanguage("da"), "Danish"),
            "HRVATSKI" => Tuple.Create(XmlLanguage.GetLanguage("hr"), "Croatian"),
            "भारतीय" => Tuple.Create(XmlLanguage.GetLanguage("gu"), "Hindi"),
            "PORTUGUÊS" => Tuple.Create(XmlLanguage.GetLanguage("pt"), "Portuguese"),
            "INDONESIA" => Tuple.Create(XmlLanguage.GetLanguage("id"), "Indonesian"),
            "ՀԱՅԵՐԵՆ" => Tuple.Create(XmlLanguage.GetLanguage("hy"), "Armenian"),
            "ROMÂNĂ" => Tuple.Create(XmlLanguage.GetLanguage("ro"), "Romanian"),
            "MAGYAR" => Tuple.Create(XmlLanguage.GetLanguage("hu"), "Hungarian"),
            "SVENSKA" => Tuple.Create(XmlLanguage.GetLanguage("sv"), "Swedish"),
            "SUOMI" => Tuple.Create(XmlLanguage.GetLanguage("fi"), "Finnish"),
            "MALAYSIAN" => Tuple.Create(XmlLanguage.GetLanguage("ms"), "Malay"),
            "ایرانی" => Tuple.Create(XmlLanguage.GetLanguage("fa"), "Persian"),
            "МАКЕДОНСКИ" => Tuple.Create(XmlLanguage.GetLanguage("mk"), "Macedonian"),
            "ქართველი" => Tuple.Create(XmlLanguage.GetLanguage("ka"), "Georgian"),
            "한국인" => Tuple.Create(XmlLanguage.GetLanguage("ko"), "Korean"),
            "UZBEK" => Tuple.Create(XmlLanguage.GetLanguage("uz"), "Uzbek"),
            "TÜRKMEN" => Tuple.Create(XmlLanguage.GetLanguage("tk"), "Turkmen"),
            _ => Tuple.Create(XmlLanguage.GetLanguage("en-US"), "English")
        };
    }
    private long GetTotalFileSizeMB(string[] files) => files?.Aggregate(0L, (accumulator, item) => accumulator += new FileInfo(item).Length) / 1024 / 1024 ?? 0;

    private async Task<ObservableCollection<string>> GetUnindexedFileData()
    {
        try
        {
            HashSet<string> scannerunindexedfiles = Dosyalar?.Where(z => unindexedfileextensions.Contains(Path.GetExtension(z?.FileName?.ToLowerInvariant()))).Select(z => z.FileName).ToHashSet();
            using AppDbContext context = new();
            List<string> scannedDatabaseFiles = await context?.Data?.AsNoTracking()?.Select(x => x.FileName).ToListAsync();
            if (scannerunindexedfiles is not null && scannedDatabaseFiles is not null)
            {
                return [.. scannerunindexedfiles.Except(scannedDatabaseFiles)];
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    private async Task<ObservableCollection<ReminderData>> GörülenReminderYükle()
    {
        try
        {
            using AppDbContext context = new();
            List<ReminderData> reminders = await context.ReminderData.AsNoTracking().Where(z => z.Seen).OrderBy(z => z.Tarih).ToListAsync();
            return [.. reminders];
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async void GpScannerViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "BaşlangıçTarihi" or "BitişTarihi")
        {
            if (!string.IsNullOrWhiteSpace(AramaMetni))
            {
                AramaMetni = string.Empty;
            }
            if (MainWindow.cvs is not null)
            {
                FilesChartList = [];
                MainWindow.cvs.Filter += (s, x) =>
                                         {
                                             Scanner scanner = (Scanner)x.Item;
                                             if (DateTime.TryParse(Directory.GetParent(scanner?.FileName).Name, out DateTime result))
                                             {
                                                 if (BaşlangıçTarihi > BitişTarihi)
                                                 {
                                                     x.Accepted = false;
                                                     return;
                                                 }
                                                 x.Accepted = result >= BaşlangıçTarihi && result <= BitişTarihi;
                                             }
                                             else
                                             {
                                                 x.Accepted = false;
                                             }
                                         };
                DrawFileSizeGraph(Settings.Default.ShowFileSizeGraph);
            }
        }

        if (e.PropertyName is "SelectedContribution" && SelectedContribution is not null)
        {
            BaşlangıçTarihi = BitişTarihi = (DateTime)SelectedContribution.ContrubutionDate;
        }

        if (e.PropertyName is "AramaMetni")
        {
            if (string.IsNullOrWhiteSpace(AramaMetni))
            {
                OnPropertyChanged(nameof(BaşlangıçTarihi));
                OnPropertyChanged(nameof(BitişTarihi));
                return;
            }

            var datas = await Task.Run(
                () =>
                {
                    ZipProgressIndeterminate = true;
                    using AppDbContext context = new();
                    return context.Data.AsNoTracking().ToList().Where(z => z.FileContent?.IndexOf(AramaMetni, StringComparison.CurrentCultureIgnoreCase) >= 0).Select(z => new { z.FileName }).ToList();
                });
            FilesChartList = [];
            MainWindow.cvs.Filter += (s, x) =>
                                     {
                                         Scanner scanner = (Scanner)x.Item;
                                         bool filearchivefilter = ExistsInArchive(scanner.FileName, Settings.Default.SearchInArchiveFiles, Settings.Default.SearchInArchiveFileLimit * 1_048_576);
                                         bool filenamefilter = Path.GetFileNameWithoutExtension(scanner.FileName).IndexOf(AramaMetni, StringComparison.CurrentCultureIgnoreCase) >= 0;
                                         bool filecontentfilter = datas?.Any(z => z.FileName == scanner.FileName) == true;
                                         x.Accepted = filenamefilter || filecontentfilter || filearchivefilter;
                                     };
            DrawFileSizeGraph(Settings.Default.ShowFileSizeGraph);
            ZipProgressIndeterminate = false;
            datas = null;
        }

        if (e.PropertyName is "SeçiliDil")
        {
            TranslationSource.Instance.CurrentCulture = SplashViewModel.ChangeApplicationLanguage(SeçiliDil);
            CalendarLang = GetLanguageSettings(SeçiliDil).Item1;
            TesseractViewModel.SeçiliDil = GetLanguageSettings(SeçiliDil).Item2;
            LangFlowDirection = ChangeApplicationFlowDirection(SeçiliDil);
            Settings.Default.DefaultLang = SeçiliDil;
        }

        if (e.PropertyName is "LangFlowDirection")
        {
            Application.Current?.Windows?.Cast<Window>()?.ToList()?.ForEach(z => z.FlowDirection = LangFlowDirection);
        }

        if (e.PropertyName is "CheckedPdfCount")
        {
            ObservableCollection<string> burnfiles = [];
            ObservableCollection<BatchPdfData> compressedfiles = [];
            foreach (Scanner item in Dosyalar?.Where(z => z.Seçili))
            {
                burnfiles.Add(item.FileName);
                if (Path.GetExtension(item.FileName.ToLowerInvariant()) == ".pdf")
                {
                    compressedfiles.Add(new BatchPdfData() { Filename = item.FileName });
                }
            }
            BurnFiles = burnfiles;
            CompressedFiles = compressedfiles;
            TotalFileSize = GetTotalFileSizeMB([.. BurnFiles]);
        }

        if (e.PropertyName is "SelectedContributionYear")
        {
            List<ScannerFileDatas> files = await GetContributionFilesAsync();
            if (files?.Any() == true)
            {
                DateTime firstdate = new(SelectedContributionYear, 1, 1);
                DateTime lastdate = new(SelectedContributionYear, 12, 31);
                ContributionData = await GetContributionData(files, firstdate, lastdate);
                ContributionData todaycontribution = ContributionData?.FirstOrDefault(item => item.ContrubutionDate == DateTime.Today);
                if (todaycontribution is not null)
                {
                    todaycontribution.Stroke = new SolidColorBrush(Colors.Blue);
                }
                ContributionDocumentCount = ContributionData?.Sum(z => z.Count) ?? 0;
            }
        }

        if (e.PropertyName is "SelectedSize")
        {
            Settings.Default.PreviewIndex = SelectedSize;
        }
    }

    private void InitializeBatchFiles(out List<string> files, out int slicecount, out Scanner scanner, out List<Task> Tasks)
    {
        files = [.. FastFileSearch.EnumerateFilepaths(BatchFolder).Where(file => BatchImageFileExtensions.Any(z => z.Checked && z.Name == Path.GetExtension(file).ToLowerInvariant()))];
        slicecount = files.Count > Settings.Default.ProcessorCount ? files.Count / Settings.Default.ProcessorCount : 1;
        scanner = ToolBox.Scanner;
        scanner.ProgressState = TaskbarItemProgressState.Normal;
        BatchTxtOcrs = [];
        Tasks = [];
        ocrcancellationToken = new CancellationTokenSource();
    }

    private bool IsFileLocked(string filePath)
    {
        try
        {
            new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None).Dispose();
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    private bool IsValidHttpAddress(string address)
    {
        const string pattern = @"^(https?|ftp):\/\/[^\s/$.?#].[^\s]*$";
        return Regex.IsMatch(address, pattern);
    }

    private bool IsWin7OrAbove()
    {
        Version os = Environment.OSVersion.Version;
        return os?.Major > 6 || (os?.Major == 6 && os?.Minor >= 1);
    }

    private async Task LoadRemainderDatas()
    {
        ScannerData = new ScannerData { Reminder = await Task.Run(ReminderYükle), GörülenReminder = await Task.Run(GörülenReminderYükle) };

        if (Settings.Default.NotifyCalendar && ScannerData?.Reminder?.Any(z => z.Tarih < DateTime.Today.AddDays(Settings.Default.NotifyCalendarDateValue)) == true)
        {
            CalendarPanelIsExpanded = true;
        }
    }

    private async Task LogErrorAsync(Exception ex)
    {
        await LogSemaphore.WaitAsync();
        try
        {
            await WriteToLogFile($@"{ProfileFolder}\{ErrorFile}", ex?.Message);
        }
        finally
        {
            _ = LogSemaphore.Release();
        }
    }

    private ObservableCollection<TessFiles> OrderBatchFiles(ObservableCollection<TessFiles> batchFolderProcessedFileList) => [.. batchFolderProcessedFileList.OrderBy(z => z.Name, new StrCmpLogicalComparer())];

    private async Task<string> ProcessFileAsync(string unIndexedFile)
    {
        string extension = Path.GetExtension(unIndexedFile.ToLowerInvariant());
        StringBuilder ocrTextBuilder = new();
        ObservableCollection<OcrData> ocrData;
        switch (extension)
        {
            case ".pdf" when PdfViewer.PdfViewer.IsValidPdfFile(unIndexedFile):
                await ProcessPdfFileAsync(unIndexedFile, ocrTextBuilder);
                break;

            case ".docx":
                using (FileStream fileStream = new(unIndexedFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using DocX document = DocX.Load(fileStream);
                    _ = ocrTextBuilder.Append(document.Text);
                }
                break;

            case ".xlsx" or ".xls" or ".xlsb" or ".csv":
                await Application.Current?.Dispatcher?
                .Invoke(
                async () =>
                {
                    using FileStream fileStream = File.Open(unIndexedFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    XlsxViewer xlsxViewer = new();
                    DataTableCollection datatablecollection = await xlsxViewer.GetDataTableCollection(fileStream, unIndexedFile);
                    foreach (DataTable dataTable in datatablecollection)
                    {
                        foreach (DataRow row in dataTable.Rows.OfType<DataRow>())
                        {
                            string rowString = string.Join("\t", dataTable.Columns.OfType<DataColumn>().Select(col => row[col]?.ToString() ?? string.Empty));
                            _ = ocrTextBuilder.Append(rowString).Append(" ");
                        }
                    }
                });
                break;

            case ".webp":
                WebP webP = new();
                ocrData = await webP.Load(unIndexedFile).ToBitmapImage(ImageFormat.Jpeg).ToTiffJpegByteArray(Format.Jpg).OcrAsync(Settings.Default.DefaultTtsLang);
                _ = ocrTextBuilder.Append(string.Join(" ", ocrData?.Select(z => z.Text)));
                break;

            default:
                ocrData = await unIndexedFile.OcrAsync(Settings.Default.DefaultTtsLang);
                _ = ocrTextBuilder.Append(string.Join(" ", ocrData?.Select(z => z.Text)));
                break;
        }

        return ocrTextBuilder.ToString();
    }

    private async Task ProcessPdfFileAsync(string unIndexedFile, StringBuilder ocrTextBuilder)
    {
        int pagecount = PdfViewer.PdfViewer.PdfPageCount(unIndexedFile);
        BitmapImage bitmapImage;
        ObservableCollection<OcrData> ocrData;
        if (OcrAllPdfPages)
        {
            for (int i = 1; i <= pagecount; i++)
            {
                if (Settings.Default.OcrContentUseInternalPdfContent)
                {
                    using PdfiumViewer.PdfDocument pdfDocument = PdfiumViewer.PdfDocument.Load(unIndexedFile);
                    _ = ocrTextBuilder.Append(pdfDocument.GetPdfText(i - 1));
                }
                else
                {
                    bitmapImage = await PdfViewer.PdfViewer.ConvertToImgAsync(unIndexedFile, i, Twainsettings.Settings.Default.ImgLoadResolution);
                    ocrData = await bitmapImage.ToTiffJpegByteArray(Format.Jpg).OcrAsync(Settings.Default.DefaultTtsLang);
                    _ = ocrTextBuilder.Append(string.Join(" ", ocrData?.Select(z => z.Text)));
                }
                OcrAllPdfPagesProgress = i / (double)pagecount;
            }
            OcrAllPdfPagesProgress = 0;
            return;
        }
        if (Settings.Default.OcrContentUseInternalPdfContent)
        {
            using PdfiumViewer.PdfDocument pdfDocument = PdfiumViewer.PdfDocument.Load(unIndexedFile);
            _ = ocrTextBuilder.Append(pdfDocument.GetPdfText(0));
            return;
        }
        bitmapImage = await PdfViewer.PdfViewer.ConvertToImgAsync(unIndexedFile, 1, Twainsettings.Settings.Default.ImgLoadResolution);
        ocrData = await bitmapImage.ToTiffJpegByteArray(Format.Jpg).OcrAsync(Settings.Default.DefaultTtsLang);
        _ = ocrTextBuilder.Append(string.Join(" ", ocrData?.Select(z => z.Text)));
    }

    private void RegisterSimplePdfFileWatcher()
    {
        List<string> folders = [.. Settings.Default.AdditionalIndexFolders.OfType<string>(), Twainsettings.Settings.Default.AutoFolder,];
        MultiFolderWatcher.Watch(this, folders);
    }

    private async Task<ObservableCollection<ReminderData>> ReminderYükle()
    {
        try
        {
            using AppDbContext context = new();
            List<ReminderData> reminders = await context.ReminderData.AsNoTracking().Where(z => z.Tarih >= DateTime.Today && !z.Seen).OrderBy(z => z.Tarih).ToListAsync();
            return [.. reminders];
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void RunAtStartup(bool isChecked, string appname = "GPSCANNER")
    {
        try
        {
            using RegistryKey registryKey = Registry.CurrentUser?.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (isChecked)
            {
                registryKey?.SetValue(appname, $@"""{Process.GetCurrentProcess().MainModule.FileName}"" /silent");
            }
            else
            {
                if (registryKey?.GetValue(appname) is not null)
                {
                    registryKey?.DeleteValue(appname);
                }
            }
        }
        catch (Exception ex)
        {
            ExtendedMessageBox extendedMessageBox = new();
            extendedMessageBox.ShowDialog(WindowService.GetFirstWindow(), ex.Message, AppName);
        }
    }

    private void RunIdleIndexOperation()
    {
        if (Settings.Default.ApplyIdleIndexOcr)
        {
            ıdleTimeIndexer.StartIdleOcrTimer();
        }
        else
        {
            ıdleTimeIndexer.StopIdleOcrTimer();
        }
    }

    private async Task SaveOcrTextToFileAsync(string fileName, string ocrText)
    {
        using AppDbContext context = new();
        _ = context.Data.Add(new Data { FileName = fileName, FileContent = ocrText });
        ocrText = null;
        _ = await context.SaveChangesAsync();
    }

    private DocX WriteDocxFile(ObservableCollection<OcrData> ocrdata, string filename)
    {
        DocX document = DocX.Create(filename);
        document.SetDefaultFont(new Xceed.Document.NET.Font("Times New Roman"), 12d);
        foreach (OcrData item in ocrdata)
        {
            Xceed.Document.NET.Paragraph paragraph = document.InsertParagraph();
            paragraph.Append(item.Text).FontSize(12).Alignment = Xceed.Document.NET.Alignment.both;
            paragraph.IndentationFirstLine = (float)(1.25 / TwainCtrl.Inch * 72);
        }
        return document;
    }

    internal class ScannerFileDatas()
    {
        public DateTime ParentDate { get; set; }

        public Scanner Scanner { get; set; }
    }
}