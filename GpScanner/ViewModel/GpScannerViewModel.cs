using Extensions;
using GpScanner.Properties;
using Microsoft.SharePoint.Client;
using Microsoft.Win32;
using Ocr;
using PdfCompressor;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Data.Entity;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using TwainControl;
using Xceed.Words.NET;
using static Extensions.ExtensionMethods;
using Application = System.Windows.Application;
using ContextMenu = System.Windows.Forms.ContextMenu;
using File = System.IO.File;
using FlowDirection = System.Windows.FlowDirection;
using InpcBase = Extensions.InpcBase;
using ListBox = System.Windows.Controls.ListBox;
using MenuItem = System.Windows.Forms.MenuItem;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Twainsettings = TwainControl.Properties;

namespace GpScanner.ViewModel;

public partial class GpScannerViewModel : InpcBase, IDataErrorInfo
{
    public static readonly string ErrorFile = "Error.log";
    public static readonly string ProfileFolder = $"{Path.GetDirectoryName(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath)}";
    public static NotifyIcon AppNotifyIcon;
    public Task Filesavetask;
    public CancellationTokenSource ocrcancellationToken;
    private const string MinimumVcVersion = "14.21.27702";
    private const int NetFxMinVersion = 461808;
    private static readonly string AppName = Application.Current?.Windows?.Cast<Window>()?.FirstOrDefault()?.Title;
    private static DispatcherTimer flaganimationtimer;
    private static DispatcherTimer timer;
    private readonly List<string> batchimagefileextensions = [".tiff", ".tıf", ".tıff", ".tif", ".jpg", ".jpe", ".gif", ".jpeg", ".jfif", ".jfıf", ".png", ".bmp"];
    private readonly string[] sqlitedangerouscommands = ["truncate", "drop", "alter"];
    private readonly string[] supportedfilesextension = [".pdf", ".eyp", ".tıff", ".tıf", ".tiff", ".tif", ".jpg", ".jpeg", ".jpe", ".png", ".bmp", ".zip", ".xps", ".mp4", ".3gp", ".wmv", ".mpg", ".mov", ".avi", ".mpeg", ".xml", ".xsl", ".xslt", ".xaml", ".xls", ".xlsx", ".xlsb", ".csv", ".docx", ".rar", ".7z", ".xz", ".gz"];
    private readonly List<string> unindexedfileextensions = [".pdf", ".tiff", ".tıf", ".tıff", ".tif", ".jpg", ".jpe", ".gif", ".jpeg", ".jfif", ".jfıf", ".png", ".bmp"];
    private int allPdfPage = 1;
    private string aramaMetni;
    private ObservableCollection<string> barcodeList = [];
    private bool batchDialogOpen;
    private string batchFolder;
    private ObservableCollection<BatchFiles> batchFolderProcessedFileList;
    private ObservableCollection<BatchTxtOcr> batchTxtOcrs;
    private ObservableCollection<string> burnFiles = [];
    private string calendarDesc;
    private XmlLanguage calendarLang;
    private bool calendarPanelIsExpanded;
    private int checkedPdfCount;
    private ObservableCollection<BatchPdfData> compressedFiles = [];
    private ObservableCollection<ContributionData> contributionData;
    private int contributionDocumentCount;
    private double contributionPreviewSize = 120;
    private int cycleIndex;
    private bool detectBarCode = true;
    private bool documentPanelIsExpanded;
    private ObservableCollection<Scanner> dosyalar;
    private string errorLogPath;
    private ObservableCollection<string> fileSystemWatcherProcessedFileList;
    private int flagProgress;
    private double fold = 0.3;
    private string ftpPassword = string.Empty;
    private string ftpSite = string.Empty;
    private string ftpUserName = string.Empty;
    private ObservableCollection<Size> getPreviewSize = [new Size(190, 305), new Size(230, 370), new Size(330, 530), new Size(380, 610), new Size(425, 645), new Size(Settings.Default.CustomWidth, Settings.Default.CustomHeight)];
    private int ındexedFileCount;
    private bool ısAdministrator;
    private bool ısSqlQuery = true;
    private FlowDirection langFlowDirection = FlowDirection.LeftToRight;
    private bool listBoxBorderAnimation;
    private GridLength mainWindowDocumentGuiControlLength = new(1, GridUnitType.Star);
    private GridLength mainWindowGuiControlLength = new(3, GridUnitType.Star);
    private double mirror;
    private DateTime notifyDate = DateTime.Today;
    private bool ocrAllPdfPages;
    private double ocrAllPdfPagesProgress;
    private bool ocrısBusy;
    private int? ocrPdfThumbnailPageNumber;
    private string patchFileName;
    private string patchProfileName;
    private string patchTag;
    private bool pdfBatchRunning;
    private double pdfMergeProgressValue;
    private Brush progressBarForegroundBrush = Brushes.Green;
    private double ripple;
    private ObservableCollection<OcrData> scannedText = [];
    private string seçiliDil;
    private DateTime seçiliGün;
    private BatchFiles selectedBatchFile;
    private ContributionData selectedContribution;
    private int selectedContributionYear = DateTime.Now.Year;
    private string selectedFtp;
    private ReminderData selectedReminder;
    private Size selectedSize;
    private bool shutdown;
    private List<Data> sqlQueryData;
    private string sqlText = string.Empty;
    private TesseractViewModel tesseractViewModel;
    private TranslateViewModel translateViewModel;
    private ObservableCollection<string> unIndexedFiles;
    private IEnumerable<IGrouping<int, ContributionData>> yearlyGroupData;
    private double zipProgress;
    private bool zipProgressIndeterminate;

    public GpScannerViewModel()
    {
        CreateEmptySqliteDatabase();
        RegisterSimplePdfFileWatcher();
        TesseractViewModel = new TesseractViewModel();
        TranslateViewModel = new TranslateViewModel();
        Settings.Default.PropertyChanged += Default_PropertyChanged;
        PropertyChanged += GpScannerViewModel_PropertyChanged;

        Dosyalar = GetScannerFileData();
        SeçiliDil = Settings.Default.DefaultLang;
        SeçiliGün = DateTime.Today;
        SelectedSize = GetPreviewSize[Settings.Default.PreviewIndex];
        GenerateAnimationTimer();
        GenerateSystemTrayMenu();
        GenerateJumpList();
        _ = LoadRemainderDatas();

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
                            List<string> pdffilelist = Dosyalar.Where(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)).Select(z => z.FileName).ToList();
                            pdffilelist.ToArray().MergePdf().Save(PdfGeneration.GetPdfScanPath());
                            await Application.Current?.Dispatcher?.InvokeAsync(() => ReloadFileDatas());
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
                                List<string> pdffilelist = Dosyalar.Where(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)).Select(z => z.FileName).ToList();
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
                if (saveFileDialog.ShowDialog() == true)
                {
                    await Task.Run(
                        () =>
                        {
                            List<string> pdffilelist = Dosyalar.Where(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)).Select(z => z.FileName).ToList();
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
                }
            },
            parameter =>
            {
                CheckedPdfCount = Dosyalar?.Count(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)) ?? 0;
                return CheckedPdfCount > 0;
            });

        OcrPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is BitmapFrame bitmapframe)
                {
                    byte[] imgdata = bitmapframe.ToTiffJpegByteArray(Format.Jpg);
                    OcrIsBusy = true;
                    ScannedText = await imgdata.OcrAsync(Settings.Default.DefaultTtsLang);
                    OcrIsBusy = false;
                    if (ScannedText != null)
                    {
                        TranslateViewModel.Metin = string.Join(" ", ScannedText.Select(z => z.Text));
                    }

                    if (DetectBarCode)
                    {
                        QrCode.QrCode qrcode = new();
                        string result = await Task.Run(() => qrcode.GetImageBarcodeResult(bitmapframe));
                        if (result != null)
                        {
                            BarcodeList.Add(result);
                        }
                    }

                    bitmapframe = null;
                    imgdata = null;
                }
            },
            parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang) && parameter is BitmapFrame bitmapFrame && bitmapFrame is not null);

        OcrPdfThumbnailPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer && PdfViewer.PdfViewer.IsValidPdfFile(pdfviewer.PdfFilePath))
                {
                    byte[] filedata = await PdfViewer.PdfViewer.ReadAllFileAsync(pdfviewer.PdfFilePath);
                    if (filedata == null)
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
                }
            },
            parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang) && (Settings.Default.ThumbMultipleOcrEnabled || !OcrIsBusy));

        UnindexedFileOcr = new RelayCommand<object>(
            async parameter =>
            {
                try
                {
                    if (parameter is string unIndexedFile)
                    {
                        OcrIsBusy = true;
                        BitmapImage bitmapImage = null;
                        ObservableCollection<OcrData> ocrdata = null;
                        string ocrtext = string.Empty;
                        if (Path.GetExtension(unIndexedFile.ToLower()) == ".pdf" && PdfViewer.PdfViewer.IsValidPdfFile(unIndexedFile))
                        {
                            double pagecount = await PdfViewer.PdfViewer.PdfPageCountAsync(File.ReadAllBytes(unIndexedFile));
                            if (OcrAllPdfPages)
                            {
                                for (int i = 1; i <= pagecount; i++)
                                {
                                    bitmapImage = await PdfViewer.PdfViewer.ConvertToImgAsync(unIndexedFile, i, Twainsettings.Settings.Default.ImgLoadResolution);
                                    ocrdata = await bitmapImage.ToTiffJpegByteArray(Format.Jpg).OcrAsync(Settings.Default.DefaultTtsLang);
                                    ocrtext += string.Join(" ", ocrdata?.Select(z => z.Text));
                                    OcrAllPdfPagesProgress = i / pagecount;
                                }
                            }
                            else
                            {
                                bitmapImage = await PdfViewer.PdfViewer.ConvertToImgAsync(unIndexedFile, 1, Twainsettings.Settings.Default.ImgLoadResolution);
                                ocrdata = await bitmapImage.ToTiffJpegByteArray(Format.Jpg).OcrAsync(Settings.Default.DefaultTtsLang);
                                ocrtext = string.Join(" ", ocrdata?.Select(z => z.Text));
                            }
                        }
                        else
                        {
                            ocrdata = await unIndexedFile.OcrAsync(Settings.Default.DefaultTtsLang);
                            ocrtext = string.Join(" ", ocrdata?.Select(z => z.Text));
                        }

                        using (AppDbContext context = new())
                        {
                            _ = context.Data.Add(new Data { FileName = unIndexedFile, FileContent = ocrtext });
                            _ = context.SaveChanges();
                        }

                        ocrdata = null;
                        ocrtext = null;
                        OcrIsBusy = false;
                        _ = UnIndexedFiles?.Remove(unIndexedFile);
                    }
                }
                catch (Exception ex)
                {
                    await WriteToLogFile($@"{ProfileFolder}\{ErrorFile}", ex?.Message);
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
                foreach (string unIndexedFile in UnIndexedFiles.ToList())
                {
                    try
                    {
                        OcrIsBusy = true;
                        BitmapImage bitmapImage = null;
                        ObservableCollection<OcrData> ocrdata;
                        string ocrtext = string.Empty;
                        if (Path.GetExtension(unIndexedFile.ToLower()) == ".pdf" && PdfViewer.PdfViewer.IsValidPdfFile(unIndexedFile))
                        {
                            double pagecount = await PdfViewer.PdfViewer.PdfPageCountAsync(File.ReadAllBytes(unIndexedFile));
                            if (OcrAllPdfPages)
                            {
                                for (int j = 1; j <= pagecount; j++)
                                {
                                    bitmapImage = await PdfViewer.PdfViewer.ConvertToImgAsync(unIndexedFile, j, Twainsettings.Settings.Default.ImgLoadResolution);
                                    ocrdata = await bitmapImage.ToTiffJpegByteArray(Format.Jpg).OcrAsync(Settings.Default.DefaultTtsLang);
                                    ocrtext += string.Join(" ", ocrdata?.Select(z => z.Text));
                                    OcrAllPdfPagesProgress = j / pagecount;
                                }
                            }
                            else
                            {
                                bitmapImage = await PdfViewer.PdfViewer.ConvertToImgAsync(unIndexedFile, 1, Twainsettings.Settings.Default.ImgLoadResolution);
                                ocrdata = await bitmapImage.ToTiffJpegByteArray(Format.Jpg).OcrAsync(Settings.Default.DefaultTtsLang);
                                ocrtext = string.Join(" ", ocrdata?.Select(z => z.Text));
                            }
                        }
                        else
                        {
                            ocrdata = await unIndexedFile.OcrAsync(Settings.Default.DefaultTtsLang);
                            ocrtext = string.Join(" ", ocrdata?.Select(z => z.Text));
                        }

                        using (AppDbContext context = new())
                        {
                            _ = context.Data.Add(new Data { FileName = unIndexedFile, FileContent = ocrtext });
                            _ = context.SaveChanges();
                        }

                        ocrdata = null;
                        ocrtext = null;
                        _ = UnIndexedFiles?.Remove(unIndexedFile);
                        IndexedFileCount = i++;
                        OcrIsBusy = false;
                        GC.Collect();
                    }
                    catch (Exception ex)
                    {
                        await WriteToLogFile($@"{ProfileFolder}\{ErrorFile}", ex?.Message);
                    }
                }
                if (Shutdown)
                {
                    ViewModel.Shutdown.DoExitWin(ViewModel.Shutdown.EWX_SHUTDOWN);
                }
            },
            parameter => !OcrIsBusy && UnIndexedFiles?.Count > 0 && !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang));

        WordOcrPdfThumbnailPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer && PdfViewer.PdfViewer.IsValidPdfFile(pdfviewer.PdfFilePath))
                {
                    byte[] filedata = await PdfViewer.PdfViewer.ReadAllFileAsync(pdfviewer.PdfFilePath);
                    if (filedata == null)
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
                }
            },
            parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang) && !OcrIsBusy);

        OpenOriginalFile = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is string filepath)
                {
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
                        documentViewerWindow.Owner = Application.Current?.Windows?.Cast<Window>()?.FirstOrDefault();
                        documentViewerModel.Scanner = ToolBox.Scanner;
                        documentViewerModel.FilePath = filepath;
                        List<string> files = MainWindow.cvs?.View?.OfType<Scanner>()?.Select(z => z.FileName)?.ToList();
                        files?.Sort(new StrCmpLogicalComparer());
                        documentViewerModel.DirectoryAllPdfFiles = files;
                        documentViewerModel.Index = Array.IndexOf(documentViewerModel.DirectoryAllPdfFiles.ToArray(), documentViewerModel.FilePath);
                        documentViewerWindow.Show();
                        documentViewerWindow.Lb?.ScrollIntoView(filepath);
                    }
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

        LoadUnindexedFiles = new RelayCommand<object>(async parameter => UnIndexedFiles = await GetUnindexedFileData(), parameter => true);

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

                foreach (Scanner item in MainWindow.cvs.View.OfType<Scanner>().Where(z => Path.GetExtension(z.FileName.ToLower()) == ".pdf"))
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

                foreach (Scanner item in MainWindow.cvs.View.OfType<Scanner>().Where(z => Path.GetExtension(z.FileName.ToLower()) == ".pdf"))
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
                    $"/w:{new WindowInteropHelper(Application.Current?.Windows?.Cast<Window>()?.FirstOrDefault()).Handle} https://github.com/goksenpasli/GpScanner/releases/download/{version.FileMajorPart}.{version.FileMinorPart}/GpScanner-Setup.txt");
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
                FolderBrowserDialog dialog = new() { Description = $"{Translation.GetResStringValue("GRAPHFOLDER")}\n{string.Join(" ", batchimagefileextensions)}" };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    BatchFolder = dialog.SelectedPath;
                }
            },
            parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang));

        SetBatchSaveFolder = new RelayCommand<object>(
            parameter =>
            {
                FolderBrowserDialog dialog = new() { Description = $"{Translation.GetResStringValue("PDFFOLDER")}" };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    Settings.Default.BatchSaveFolder = dialog.SelectedPath;
                }
            },
            parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang));

        BatchFolderTümünüİşaretle = new RelayCommand<object>(
            parameter =>
            {
                foreach (BatchFiles item in BatchFolderProcessedFileList)
                {
                    item.Checked = true;
                }
            },
            parameter => BatchFolderProcessedFileList?.Count > 0);

        BatchFolderTümünüİşaretiniKaldır = new RelayCommand<object>(
            parameter =>
            {
                foreach (BatchFiles item in BatchFolderProcessedFileList)
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
                FolderBrowserDialog dialog = new() { Description = Translation.GetResStringValue("BATCHDESC") };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (dialog.SelectedPath == Twainsettings.Settings.Default.AutoFolder)
                    {
                        _ = MessageBox.Show(Translation.GetResStringValue("NO ACTION"), AppName, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        return;
                    }
                    Settings.Default.BatchFolder = dialog.SelectedPath;
                    Settings.Default.Save();
                }
            },
            parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang));

        StartPdfBatch = new RelayCommand<object>(
            async parameter =>
            {
                if (Filesavetask?.IsCompleted == false)
                {
                    _ = MessageBox.Show(Translation.GetResStringValue("TASKSRUNNING"), AppName);
                    return;
                }
                BatchFolderProcessedFileList = [];
                InitializeBatchFiles(out List<string> files, out int slicecount, out Scanner scanner, out List<Task> Tasks);
                GC.Collect();
                foreach (List<string> item in TwainCtrl.ChunkBy(files, slicecount))
                {
                    try
                    {
                        BatchTxtOcr batchTxtOcr = new();
                        Paper paper = ToolBox.Paper;
                        Task task = Task.Run(
                            async () =>
                            {
                                for (int i = 0; i < item.Count; i++)
                                {
                                    if (ocrcancellationToken?.IsCancellationRequested == false)
                                    {
                                        string pdffile = Path.ChangeExtension(item.ElementAtOrDefault(i), ".pdf");
                                        ObservableCollection<OcrData> scannedText = scanner?.ApplyPdfSaveOcr == true ? item.ElementAtOrDefault(i).GetOcrData(Settings.Default.DefaultTtsLang) : null;

                                        batchTxtOcr.ProgressValue = (i + 1) / (double)item.Count;
                                        batchTxtOcr.FilePath = Path.GetFileName(item.ElementAtOrDefault(i));
                                        if (Settings.Default.PdfBatchCompress)
                                        {
                                            BitmapFrame.Create(new Uri(item.ElementAtOrDefault(i))).GeneratePdf(scannedText, Format.Jpg, paper, Twainsettings.Settings.Default.JpegQuality, Twainsettings.Settings.Default.ImgLoadResolution).Save(pdffile);
                                        }
                                        else
                                        {
                                            item.ElementAtOrDefault(i).GeneratePdf(paper, scannedText).Save(pdffile);
                                        }
                                        await Application.Current?.Dispatcher?.InvokeAsync(
                                        () =>
                                        {
                                            string file = Path.ChangeExtension(item.ElementAtOrDefault(i), ".pdf");
                                            BatchFolderProcessedFileList.Add(new BatchFiles() { Name = file });
                                        });
                                        scanner.PdfSaveProgressValue = BatchTxtOcrs?.Sum(z => z.ProgressValue) / Tasks.Count ?? 0;
                                    }
                                }
                            },
                            ocrcancellationToken.Token);
                        BatchTxtOcrs.Add(batchTxtOcr);
                        Tasks.Add(task);
                    }
                    catch (Exception ex)
                    {
                        await WriteToLogFile($@"{ProfileFolder}\{ErrorFile}", ex?.Message);
                    }
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
                string[] files = BatchFolderProcessedFileList.Where(z => z.Checked).Select(z => z.Name).ToArray();
                if (files.Length > 0)
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
                    _ = MessageBox.Show(Translation.GetResStringValue("TASKSRUNNING"), AppName);
                    return;
                }

                InitializeBatchFiles(out List<string> files, out int slicecount, out Scanner scanner, out List<Task> Tasks);
                GC.Collect();
                foreach (List<string> item in TwainCtrl.ChunkBy(files, slicecount))
                {
                    try
                    {
                        BatchTxtOcr batchTxtOcr = new();
                        Task task = Task.Run(
                            () =>
                            {
                                for (int i = 0; i < item.Count; i++)
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
                            },
                            ocrcancellationToken.Token);
                        BatchTxtOcrs.Add(batchTxtOcr);
                        Tasks.Add(task);
                    }
                    catch (Exception ex)
                    {
                        await WriteToLogFile($@"{ProfileFolder}\{ErrorFile}", ex?.Message);
                    }
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
                    _ = MessageBox.Show(Translation.GetResStringValue("RESTARTAPP"), AppName);
                }
            });

        CancelOcr = new RelayCommand<object>(parameter => Ocr.Ocr.ocrcancellationToken?.Cancel());

        DateBack = new RelayCommand<object>(parameter => SeçiliGün = SeçiliGün.AddDays(-1), parameter => SeçiliGün > DateTime.MinValue);

        DateForward = new RelayCommand<object>(parameter => SeçiliGün = SeçiliGün.AddDays(1), parameter => SeçiliGün < DateTime.Today);

        CycleSelectedDocuments = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is ListBox listBox)
                {
                    List<Scanner> listboxFiles = MainWindow.cvs.View.OfType<Scanner>().ToList();
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
                }
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
                Window mainWindow = Application.Current?.Windows?.OfType<Window>()?.FirstOrDefault();
                if (mainWindow.DataContext is GpScannerViewModel gpScannerViewModel)
                {
                    gpScannerViewModel.GenerateFlagAnimation();
                    SettingsWindowView settingswindow = new() { Owner = mainWindow, DataContext = gpScannerViewModel };
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
                if (!File.Exists(ErrorLogPath))
                {
                    OpenFileDialog openFileDialog = new() { Filter = "Log Dosyası (*.log)|*.log", Multiselect = false };
                    if (openFileDialog.ShowDialog() == true)
                    {
                        ErrorLogPath = openFileDialog.FileName;
                    }
                }
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
                ObservableCollection<ContributionData> contributiondata = await GetContributionData(GetContributionFiles(), new DateTime(DateTime.Now.Year, 1, 1), new DateTime(DateTime.Now.Year, 12, 31));
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
                        _ = MessageBox.Show(Translation.GetResStringValue("SUCCESS"), AppName, MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (parameter is IGrouping<int, ContributionData> data)
                {
                    string monthname = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(data.Key);
                    SaveFileDialog saveFileDialog = new() { Filter = "Zip Dosyası(*.zip)|*.zip", FileName = $"{monthname} {Translation.GetResStringValue("MERGE")}" };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        await Task.Run(
                            () =>
                            {
                                List<string> zippedfiles = data.Where(z => z.Count > 0).SelectMany(z => ((ExtendedContributionData)z).Name).ToList();
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
                }
            },
            parameter => parameter is IGrouping<int, ContributionData> data && data.Count(z => z.Count > 0) > 0);

        SetDbBackUpFolder = new RelayCommand<object>(
            parameter =>
            {
                FolderBrowserDialog dialog = new() { Description = $"{Translation.GetResStringValue("AUTOFOLDER")}\n{Translation.GetResStringValue("BACKUPDB")}", SelectedPath = Settings.Default.DataBaseBackUpFolder };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    DriveInfo driveInfo = new(dialog.SelectedPath);
                    if (driveInfo.DriveType == DriveType.CDRom)
                    {
                        _ = MessageBox.Show($"{Translation.GetResStringValue("ERROR")}\n{Translation.GetResStringValue("INVALIDFILENAME")}", AppName, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        return;
                    }
                    Settings.Default.DataBaseBackUpFolder = dialog.SelectedPath;
                }
            },
            parameter => true);
    }

    public ICommand AddFtpSites { get; }

    public RelayCommand<object> AddPdfGroupFilesMonthToControlPanel { get; }

    public ICommand AddToCalendar { get; }

    public int AllPdfPage
    {
        get => allPdfPage;

        set
        {
            if (allPdfPage != value)
            {
                allPdfPage = value;
                OnPropertyChanged(nameof(AllPdfPage));
            }
        }
    }

    public RelayCommand<object> ApplyCalendarData { get; }

    public string AramaMetni
    {
        get => aramaMetni;

        set
        {
            if (aramaMetni != value)
            {
                aramaMetni = value;
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
        get => barcodeList;

        set
        {
            if (barcodeList != value)
            {
                barcodeList = value;
                OnPropertyChanged(nameof(BarcodeList));
            }
        }
    }

    public bool BatchDialogOpen
    {
        get => batchDialogOpen;

        set
        {
            if (batchDialogOpen != value)
            {
                batchDialogOpen = value;
                OnPropertyChanged(nameof(BatchDialogOpen));
            }
        }
    }

    public string BatchFolder
    {
        get => batchFolder;

        set
        {
            if (batchFolder != value)
            {
                batchFolder = value;
                OnPropertyChanged(nameof(BatchFolder));
            }
        }
    }

    public ObservableCollection<BatchFiles> BatchFolderProcessedFileList
    {
        get => batchFolderProcessedFileList;
        set
        {
            if (batchFolderProcessedFileList != value)
            {
                batchFolderProcessedFileList = value;
                OnPropertyChanged(nameof(BatchFolderProcessedFileList));
            }
        }
    }

    public RelayCommand<object> BatchFolderTümünüİşaretiniKaldır { get; }

    public RelayCommand<object> BatchFolderTümünüİşaretle { get; }

    public RelayCommand<object> BatchFolderTümünüKaydet { get; }

    public RelayCommand<object> BatchFolderTümünüSil { get; }

    public RelayCommand<object> BatchMergeSelectedFiles { get; }

    public ObservableCollection<BatchTxtOcr> BatchTxtOcrs
    {
        get => batchTxtOcrs;

        set
        {
            if (batchTxtOcrs != value)
            {
                batchTxtOcrs = value;
                OnPropertyChanged(nameof(BatchTxtOcrs));
            }
        }
    }

    public ObservableCollection<string> BurnFiles
    {
        get => burnFiles;
        set
        {
            if (burnFiles != value)
            {
                burnFiles = value;
                OnPropertyChanged(nameof(BurnFiles));
            }
        }
    }

    public string CalendarDesc
    {
        get => calendarDesc;
        set
        {
            if (calendarDesc != value)
            {
                calendarDesc = value;
                OnPropertyChanged(nameof(CalendarDesc));
            }
        }
    }

    public XmlLanguage CalendarLang
    {
        get => calendarLang;

        set
        {
            if (calendarLang != value)
            {
                calendarLang = value;
                OnPropertyChanged(nameof(CalendarLang));
            }
        }
    }

    public bool CalendarPanelIsExpanded
    {
        get => calendarPanelIsExpanded;
        set
        {
            if (calendarPanelIsExpanded != value)
            {
                calendarPanelIsExpanded = value;
                OnPropertyChanged(nameof(CalendarPanelIsExpanded));
            }
        }
    }

    public ICommand CancelBatchOcr { get; }

    public ICommand CancelOcr { get; }

    public ICommand ChangeDataFolder { get; }

    public int CheckedPdfCount
    {
        get => checkedPdfCount;

        set
        {
            if (checkedPdfCount != value)
            {
                checkedPdfCount = value;
                OnPropertyChanged(nameof(CheckedPdfCount));
            }
        }
    }

    public ICommand CheckUpdate { get; }

    public RelayCommand<object> ClearPdfCompressorBatchList { get; }

    public ObservableCollection<BatchPdfData> CompressedFiles
    {
        get => compressedFiles;

        set
        {
            if (compressedFiles != value)
            {
                compressedFiles = value;
                OnPropertyChanged(nameof(CompressedFiles));
            }
        }
    }

    public ObservableCollection<ContributionData> ContributionData
    {
        get => contributionData;
        set
        {
            if (contributionData != value)
            {
                contributionData = value;
                OnPropertyChanged(nameof(ContributionData));
            }
        }
    }

    public int ContributionDocumentCount
    {
        get => contributionDocumentCount;

        set
        {
            if (contributionDocumentCount != value)
            {
                contributionDocumentCount = value;
                OnPropertyChanged(nameof(ContributionDocumentCount));
            }
        }
    }

    public double ContributionPreviewSize
    {
        get => contributionPreviewSize;
        set
        {
            if (contributionPreviewSize != value)
            {
                contributionPreviewSize = value;
                OnPropertyChanged(nameof(ContributionPreviewSize));
            }
        }
    }

    public ICommand CycleSelectedDocuments { get; }

    public ICommand DateBack { get; }

    public ICommand DateForward { get; }

    public bool DetectBarCode
    {
        get => detectBarCode;

        set
        {
            if (detectBarCode != value)
            {
                detectBarCode = value;
                OnPropertyChanged(nameof(DetectBarCode));
            }
        }
    }

    public bool DocumentPanelIsExpanded
    {
        get => documentPanelIsExpanded;

        set
        {
            if (documentPanelIsExpanded != value)
            {
                documentPanelIsExpanded = value;
                OnPropertyChanged(nameof(DocumentPanelIsExpanded));
            }
        }
    }

    public ObservableCollection<Scanner> Dosyalar
    {
        get => dosyalar;

        set
        {
            if (dosyalar != value)
            {
                dosyalar = value;
                OnPropertyChanged(nameof(Dosyalar));
            }
        }
    }

    public string Error => string.Empty;

    public string ErrorLogPath
    {
        get => errorLogPath;
        set
        {
            if (errorLogPath != value)
            {
                errorLogPath = value;
                OnPropertyChanged(nameof(ErrorLogPath));
            }
        }
    }

    public ICommand ExploreFile { get; }

    public ObservableCollection<string> FileSystemWatcherProcessedFileList
    {
        get => fileSystemWatcherProcessedFileList;
        set
        {
            if (fileSystemWatcherProcessedFileList != value)
            {
                fileSystemWatcherProcessedFileList = value;
                OnPropertyChanged(nameof(FileSystemWatcherProcessedFileList));
            }
        }
    }

    public int FlagProgress
    {
        get => flagProgress;
        set
        {
            if (flagProgress != value)
            {
                flagProgress = value;
                OnPropertyChanged(nameof(FlagProgress));
            }
        }
    }

    public RelayCommand<object> FocusControl { get; }

    public double Fold
    {
        get => fold;

        set
        {
            if (fold != value)
            {
                fold = value;
                OnPropertyChanged(nameof(Fold));
            }
        }
    }

    public string FtpPassword
    {
        get => ftpPassword;

        set
        {
            if (ftpPassword != value)
            {
                ftpPassword = value;
                OnPropertyChanged(nameof(FtpPassword));
            }
        }
    }

    public string FtpSite
    {
        get => ftpSite;

        set
        {
            if (ftpSite != value)
            {
                ftpSite = value;
                OnPropertyChanged(nameof(FtpSite));
            }
        }
    }

    public string FtpUserName
    {
        get => ftpUserName;

        set
        {
            if (ftpUserName != value)
            {
                ftpUserName = value;
                OnPropertyChanged(nameof(FtpUserName));
            }
        }
    }

    public ObservableCollection<Size> GetPreviewSize
    {
        get => getPreviewSize;
        set
        {
            if (getPreviewSize != value)
            {
                getPreviewSize = value;
                OnPropertyChanged(nameof(GetPreviewSize));
            }
        }
    }

    public RelayCommand<object> GridSplitterMouseDoubleClick { get; }

    public RelayCommand<object> GridSplitterMouseRightButtonDown { get; }

    public int IndexedFileCount
    {
        get => ındexedFileCount;

        set
        {
            if (ındexedFileCount != value)
            {
                ındexedFileCount = value;
                OnPropertyChanged(nameof(IndexedFileCount));
            }
        }
    }

    public bool IsAdministrator
    {
        get
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            ısAdministrator = principal.IsInRole(WindowsBuiltInRole.Administrator);
            return ısAdministrator;
        }
        set
        {
            if (ısAdministrator != value)
            {
                ısAdministrator = value;
                OnPropertyChanged(nameof(IsAdministrator));
            }
        }
    }

    public bool IsSqlQuery
    {
        get => ısSqlQuery;
        set
        {
            if (ısSqlQuery != value)
            {
                ısSqlQuery = value;
                OnPropertyChanged(nameof(IsSqlQuery));
            }
        }
    }

    public FlowDirection LangFlowDirection
    {
        get => langFlowDirection;
        set
        {
            if (langFlowDirection != value)
            {
                langFlowDirection = value;
                OnPropertyChanged(nameof(LangFlowDirection));
            }
        }
    }

    public bool ListBoxBorderAnimation
    {
        get => listBoxBorderAnimation;

        set
        {
            if (listBoxBorderAnimation != value)
            {
                listBoxBorderAnimation = value;
                OnPropertyChanged(nameof(ListBoxBorderAnimation));
            }
        }
    }

    public RelayCommand<object> LoadContributionData { get; }

    public RelayCommand<object> LoadErrorEvents { get; }

    public RelayCommand<object> LoadGroupFilesMonth { get; }

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
        get => mirror;
        set
        {
            if (mirror != value)
            {
                mirror = value;
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
        get => notifyDate;
        set
        {
            if (notifyDate != value)
            {
                notifyDate = value;
                OnPropertyChanged(nameof(NotifyDate));
            }
        }
    }

    public bool OcrAllPdfPages
    {
        get => ocrAllPdfPages;
        set
        {
            if (ocrAllPdfPages != value)
            {
                ocrAllPdfPages = value;
                OnPropertyChanged(nameof(OcrAllPdfPages));
            }
        }
    }

    public double OcrAllPdfPagesProgress
    {
        get => ocrAllPdfPagesProgress;
        set
        {
            if (ocrAllPdfPagesProgress != value)
            {
                ocrAllPdfPagesProgress = value;
                OnPropertyChanged(nameof(OcrAllPdfPagesProgress));
            }
        }
    }

    public bool OcrIsBusy
    {
        get => ocrısBusy;

        set
        {
            if (ocrısBusy != value)
            {
                ocrısBusy = value;
                OnPropertyChanged(nameof(OcrIsBusy));
            }
        }
    }

    public ICommand OcrPage { get; }

    public ICommand OcrPdfThumbnailPage { get; }

    public int? OcrPdfThumbnailPageNumber
    {
        get => ocrPdfThumbnailPageNumber;
        set
        {
            if (ocrPdfThumbnailPageNumber != value)
            {
                ocrPdfThumbnailPageNumber = value;
                OnPropertyChanged(nameof(OcrPdfThumbnailPageNumber));
            }
        }
    }

    public ICommand OpenOriginalFile { get; }

    public RelayCommand<object> OpenSettings { get; }

    public string PatchFileName
    {
        get => patchFileName;

        set
        {
            if (patchFileName != value)
            {
                patchFileName = value;
                OnPropertyChanged(nameof(PatchFileName));
            }
        }
    }

    public string PatchProfileName
    {
        get => patchProfileName?.Split('|')[0];

        set
        {
            if (patchProfileName != value)
            {
                patchProfileName = value;
                OnPropertyChanged(nameof(PatchProfileName));
            }
        }
    }

    public string PatchTag
    {
        get => patchTag;

        set
        {
            if (patchTag != value)
            {
                patchTag = value;
                OnPropertyChanged(nameof(PatchTag));
            }
        }
    }

    public bool PdfBatchRunning
    {
        get => pdfBatchRunning;

        set
        {
            if (pdfBatchRunning != value)
            {
                pdfBatchRunning = value;
                OnPropertyChanged(nameof(PdfBatchRunning));
            }
        }
    }

    public ICommand PdfBirleştir { get; }

    public double PdfMergeProgressValue
    {
        get => pdfMergeProgressValue;

        set
        {
            if (pdfMergeProgressValue != value)
            {
                pdfMergeProgressValue = value;
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
        get => progressBarForegroundBrush;

        set
        {
            if (progressBarForegroundBrush != value)
            {
                progressBarForegroundBrush = value;
                OnPropertyChanged(nameof(ProgressBarForegroundBrush));
            }
        }
    }

    public ICommand RegisterSti { get; }

    public ICommand RemovePatchProfile { get; }

    public ICommand RemoveSelectedFtp { get; }

    public ICommand ResetSettings { get; }

    public double Ripple
    {
        get => ripple;
        set
        {
            if (ripple != value)
            {
                ripple = value;
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
        get => scannedText;

        set
        {
            if (scannedText != value)
            {
                scannedText = value;
                OnPropertyChanged(nameof(ScannedText));
            }
        }
    }

    public ScannerData ScannerData { get; set; }

    public string SeçiliDil
    {
        get => seçiliDil;

        set
        {
            if (seçiliDil != value)
            {
                seçiliDil = value;
                OnPropertyChanged(nameof(SeçiliDil));
            }
        }
    }

    public DateTime SeçiliGün
    {
        get => seçiliGün;

        set
        {
            if (seçiliGün != value)
            {
                seçiliGün = value;
                OnPropertyChanged(nameof(SeçiliGün));
            }
        }
    }

    public BatchFiles SelectedBatchFile
    {
        get => selectedBatchFile;
        set
        {
            if (selectedBatchFile != value)
            {
                selectedBatchFile = value;
                OnPropertyChanged(nameof(SelectedBatchFile));
            }
        }
    }

    public ContributionData SelectedContribution
    {
        get => selectedContribution;
        set
        {
            if (selectedContribution != value)
            {
                selectedContribution = value;
                OnPropertyChanged(nameof(SelectedContribution));
            }
        }
    }

    public int SelectedContributionYear
    {
        get => selectedContributionYear;
        set
        {
            if (selectedContributionYear != value)
            {
                selectedContributionYear = value;
                OnPropertyChanged(nameof(SelectedContributionYear));
            }
        }
    }

    public string SelectedFtp
    {
        get => selectedFtp;

        set
        {
            if (selectedFtp != value)
            {
                selectedFtp = value;
                OnPropertyChanged(nameof(SelectedFtp));
            }
        }
    }

    public ReminderData SelectedReminder
    {
        get => selectedReminder;
        set
        {
            if (selectedReminder != value)
            {
                selectedReminder = value;
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

    public RelayCommand<object> SetDbBackUpFolder { get; }

    public int[] SettingsPagePdfDpiList { get; } = PdfViewer.PdfViewer.DpiList;

    public int[] SettingsPagePictureResizeList { get; } = Enumerable.Range(5, 100).Where(z => z % 5 == 0).ToArray();

    public bool Shutdown
    {
        get => shutdown;

        set
        {
            if (shutdown != value)
            {
                shutdown = value;
                OnPropertyChanged(nameof(Shutdown));
            }
        }
    }

    public List<Data> SqlQueryData
    {
        get => sqlQueryData;

        set
        {
            if (sqlQueryData != value)
            {
                sqlQueryData = value;
                OnPropertyChanged(nameof(SqlQueryData));
            }
        }
    }

    public string SqlText
    {
        get => sqlText;

        set
        {
            if (sqlText != value)
            {
                sqlText = value;
                OnPropertyChanged(nameof(SqlText));
            }
        }
    }

    public ICommand StartPdfBatch { get; }

    public ICommand StartTxtBatch { get; }

    public RelayCommand<object> StopFlagAnimation { get; }

    public ICommand Tersiniİşaretle { get; }

    public TesseractViewModel TesseractViewModel
    {
        get => tesseractViewModel;

        set
        {
            if (tesseractViewModel != value)
            {
                tesseractViewModel = value;
                OnPropertyChanged(nameof(TesseractViewModel));
            }
        }
    }

    public bool TesseractVisualCRuntimeInstalled => CheckFileVersion($@"{Environment.SystemDirectory}\msvcp140.dll") > new Version(MinimumVcVersion);

    public TranslateViewModel TranslateViewModel
    {
        get => translateViewModel;

        set
        {
            if (translateViewModel != value)
            {
                translateViewModel = value;
                OnPropertyChanged(nameof(TranslateViewModel));
            }
        }
    }

    public ICommand Tümünüİşaretle { get; }

    public ICommand TümününİşaretiniKaldır { get; }

    public RelayCommand<object> UndoApplyCalendarData { get; }

    public ObservableCollection<string> UnIndexedFiles
    {
        get => unIndexedFiles;
        set
        {
            if (unIndexedFiles != value)
            {
                unIndexedFiles = value;
                OnPropertyChanged(nameof(UnIndexedFiles));
            }
        }
    }

    public RelayCommand<object> UnindexedAllFilesOcr { get; }

    public RelayCommand<object> UnindexedFileOcr { get; }

    public ICommand UnRegisterSti { get; }

    public RelayCommand<object> UploadSharePoint { get; }

    public RelayCommand<object> WordOcrPdfThumbnailPage { get; }

    public IEnumerable<IGrouping<int, ContributionData>> YearlyGroupData
    {
        get => yearlyGroupData;
        set
        {
            if (yearlyGroupData != value)
            {
                yearlyGroupData = value;
                OnPropertyChanged(nameof(YearlyGroupData));
            }
        }
    }

    public IEnumerable<int> Years { get; } = Enumerable.Range(DateTime.Now.Year - 10, 11);

    public double ZipProgress
    {
        get => zipProgress;
        set
        {
            if (zipProgress != value)
            {
                zipProgress = value;
                OnPropertyChanged(nameof(ZipProgress));
            }
        }
    }

    public bool ZipProgressIndeterminate
    {
        get => zipProgressIndeterminate;
        set
        {
            if (zipProgressIndeterminate != value)
            {
                zipProgressIndeterminate = value;
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
        await writer.WriteLineAsync($"{DateTime.Now} {content}");
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

        List<string> patchcodes = Settings.Default.PatchCodes.Cast<string>().ToList();
        string matchingPatchCode = patchcodes?.Find(z => z.Split('|')[0] == qrcodetag);
        return matchingPatchCode?.Split('|')[1] ?? Translation.GetResStringValue("DEFAULTSCANNAME");
    }

    public bool NeedAppUpdate() => Settings.Default.CheckAppUpdate && DateTime.Now > Settings.Default.LastCheckDate.AddDays(Settings.Default.UpdateInterval);

    public void RegisterBatchImageFileWatcher(Paper paper, string batchfolder, string batchsavefolder)
    {
        if (Directory.Exists(batchfolder) && Directory.Exists(batchsavefolder))
        {
            FileSystemWatcherProcessedFileList = [];
            FileSystemWatcher watcher = new(batchfolder) { NotifyFilter = NotifyFilters.FileName, Filter = "*.*", IncludeSubdirectories = true, EnableRaisingEvents = true };
            batchimagefileextensions.Add(".webp");
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

                                   if (File.Exists(currentfilepath) && batchimagefileextensions.Contains(Path.GetExtension(currentfilename.ToLower())))
                                   {
                                       await FileSystemWatcherOcrFile(paper, batchsavefolder, currentfilepath, currentfilename);
                                       await Application.Current?.Dispatcher?.InvokeAsync(
                                       () =>
                                       {
                                           string item = $"{batchsavefolder}\\{Path.ChangeExtension(currentfilename, ".pdf")}";
                                           FileSystemWatcherProcessedFileList.Add(item);
                                       });
                                   }
                               };
        }
    }

    public void ReloadFileDatas(bool dateapplytoday = true)
    {
        Dosyalar = GetScannerFileData();
        if (dateapplytoday)
        {
            SeçiliGün = DateTime.Today;
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

    private void ChangeApplicationLanguage(string lang)
    {
        LangFlowDirection = FlowDirection.LeftToRight;
        switch (lang)
        {
            case "TÜRKÇE":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                CalendarLang = XmlLanguage.GetLanguage("tr-TR");
                TesseractViewModel.SeçiliDil = "Turkish";
                break;

            case "ENGLISH":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                CalendarLang = XmlLanguage.GetLanguage("en-US");
                TesseractViewModel.SeçiliDil = "English";

                break;

            case "FRANÇAIS":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                CalendarLang = XmlLanguage.GetLanguage("fr-FR");
                TesseractViewModel.SeçiliDil = "French";

                break;

            case "ITALIANO":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
                CalendarLang = XmlLanguage.GetLanguage("it-IT");
                TesseractViewModel.SeçiliDil = "Italian";

                break;

            case "عربي":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("ar-AR");
                CalendarLang = XmlLanguage.GetLanguage("ar-AR");
                TesseractViewModel.SeçiliDil = "Arabic";

                LangFlowDirection = FlowDirection.RightToLeft;
                break;

            case "РУССКИЙ":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                CalendarLang = XmlLanguage.GetLanguage("ru-RU");
                TesseractViewModel.SeçiliDil = "Russian";

                break;

            case "DEUTSCH":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                CalendarLang = XmlLanguage.GetLanguage("de-DE");
                TesseractViewModel.SeçiliDil = "German";

                break;

            case "日本":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("ja-JP");
                CalendarLang = XmlLanguage.GetLanguage("ja-JP");
                TesseractViewModel.SeçiliDil = "Japanese";

                break;

            case "DUTCH":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("nl-NL");
                CalendarLang = XmlLanguage.GetLanguage("nl-NL");
                TesseractViewModel.SeçiliDil = "Dutch";

                break;

            case "BELGIË":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("nl-NL");
                CalendarLang = XmlLanguage.GetLanguage("nl-NL");
                TesseractViewModel.SeçiliDil = "Dutch";

                break;

            case "CZECH":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("cs-CZ");
                CalendarLang = XmlLanguage.GetLanguage("cs-CZ");
                TesseractViewModel.SeçiliDil = "Czech";

                break;

            case "ESPAÑOL":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("es-ES");
                CalendarLang = XmlLanguage.GetLanguage("es-ES");
                TesseractViewModel.SeçiliDil = "Spanish";

                break;

            case "中國人":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("zh-CN");
                CalendarLang = XmlLanguage.GetLanguage("zh-CN");
                TesseractViewModel.SeçiliDil = "Chinese";

                break;

            case "УКРАЇНСЬКА":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("uk-UA");
                CalendarLang = XmlLanguage.GetLanguage("uk-UA");
                TesseractViewModel.SeçiliDil = "Ukrainian";

                break;

            case "ΕΛΛΗΝΙΚΑ":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("el");
                CalendarLang = XmlLanguage.GetLanguage("el");
                TesseractViewModel.SeçiliDil = "Greek";

                break;

            case "فلسطين":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("ar-AR");
                CalendarLang = XmlLanguage.GetLanguage("ar-AR");
                TesseractViewModel.SeçiliDil = "Arabic";

                LangFlowDirection = FlowDirection.RightToLeft;
                break;

            case "لبنان":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("ar-AR");
                CalendarLang = XmlLanguage.GetLanguage("ar-AR");
                TesseractViewModel.SeçiliDil = "Arabic";

                LangFlowDirection = FlowDirection.RightToLeft;
                break;

            case "AZƏRBAYCAN":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("az");
                CalendarLang = XmlLanguage.GetLanguage("az");
                TesseractViewModel.SeçiliDil = "Azerbaijani";

                break;
            case "БЕЛАРУСКАЯ":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("be");
                CalendarLang = XmlLanguage.GetLanguage("be");
                TesseractViewModel.SeçiliDil = "Belarusian";

                break;

            case "БЪЛГАРСКИ":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("bg");
                CalendarLang = XmlLanguage.GetLanguage("bg");
                TesseractViewModel.SeçiliDil = "Bulgarian";

                break;

            case "DANSK":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("da");
                CalendarLang = XmlLanguage.GetLanguage("da");
                TesseractViewModel.SeçiliDil = "Danish";

                break;

            case "HRVATSKI":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("hr");
                CalendarLang = XmlLanguage.GetLanguage("hr");
                TesseractViewModel.SeçiliDil = "Croatian";

                break;

            case "भारतीय":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("gu");
                CalendarLang = XmlLanguage.GetLanguage("gu");
                TesseractViewModel.SeçiliDil = "Hindi";

                break;
            case "PORTUGUÊS":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("pt");
                CalendarLang = XmlLanguage.GetLanguage("pt");
                TesseractViewModel.SeçiliDil = "Portuguese";

                break;

            case "INDONESIA":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("id");
                CalendarLang = XmlLanguage.GetLanguage("id");
                TesseractViewModel.SeçiliDil = "Indonesian";

                break;

            case "ՀԱՅԵՐԵՆ":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("hy");
                CalendarLang = XmlLanguage.GetLanguage("hy");
                TesseractViewModel.SeçiliDil = "Armenian";

                break;

            case "ROMÂNĂ":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("ro");
                CalendarLang = XmlLanguage.GetLanguage("ro");
                TesseractViewModel.SeçiliDil = "Romanian";

                break;

            case "MAGYAR":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("hu");
                CalendarLang = XmlLanguage.GetLanguage("hu");
                TesseractViewModel.SeçiliDil = "Hungarian";

                break;

            case "SVENSKA":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("sv");
                CalendarLang = XmlLanguage.GetLanguage("sv");
                TesseractViewModel.SeçiliDil = "Swedish";

                break;

            case "SUOMI":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("sv");
                CalendarLang = XmlLanguage.GetLanguage("sv");
                TesseractViewModel.SeçiliDil = "Finnish";

                break;

            case "MALAYSIAN":
                TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("ms");
                CalendarLang = XmlLanguage.GetLanguage("ms");
                TesseractViewModel.SeçiliDil = "Malay";

                break;
        }
    }

    private Version CheckFileVersion(string filepath) => File.Exists(filepath) ? new Version(FileVersionInfo.GetVersionInfo(filepath).FileVersion) : null;

    private void CreateEmptySqliteDatabase()
    {
        if (!File.Exists(Settings.Default.DatabaseFile))
        {
            Settings.Default.DatabaseFile = $@"{ProfileFolder}\Data.db";
            using AppDbContext context = new();
            _ = context.Database
            .ExecuteSqlCommand(
                """
                CREATE TABLE "Data" (
                	"Id"	INTEGER UNIQUE,
                	"FileName"	TEXT,
                	"QrData"	TEXT,
                	"FileContent"	TEXT,
                	PRIMARY KEY("Id")
                )
                """);
            _ = context.Database
            .ExecuteSqlCommand(
                """
                CREATE INDEX "index" ON "Data" (
                	"FileContent",
                	"FileName"	ASC
                );
                """);
            _ = context.Database
            .ExecuteSqlCommand(
                """
                CREATE TABLE "ReminderDatas" (
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
        if (e.PropertyName is "RegisterBatchWatcher" && Settings.Default.RegisterBatchWatcher)
        {
            if (!Directory.Exists(Settings.Default.BatchFolder) || !Directory.Exists(Settings.Default.BatchSaveFolder))
            {
                Settings.Default.RegisterBatchWatcher = false;
                Settings.Default.BatchFolder = null;
                Settings.Default.BatchSaveFolder = null;
            }
            else
            {
                _ = MessageBox.Show(Translation.GetResStringValue("RESTARTAPP"), AppName);
            }
        }

        if (e.PropertyName is "BatchFolder" or "BatchSaveFolder")
        {
            if (Settings.Default.BatchFolder?.Length == 0 || Settings.Default.BatchSaveFolder?.Length == 0)
            {
                Settings.Default.RegisterBatchWatcher = false;
            }
        }

        if (e.PropertyName is "CustomWidth" or "CustomHeight")
        {
            GetPreviewSize = [new Size(190, 305), new Size(230, 370), new Size(330, 530), new Size(380, 610), new Size(425, 645), new Size(Settings.Default.CustomWidth, Settings.Default.CustomHeight)];
        }

        Settings.Default.Save();
    }

    private async Task FileSystemWatcherOcrFile(Paper paper, string batchsavefolder, string currentfilepath, string currentfilename)
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
            scannedText = await currentfilepath.OcrAsync(Settings.Default.DefaultTtsLang);
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

    private void GenerateAnimationTimer()
    {
        timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(15) };
        timer.Tick += AnimationOnTick;
        timer.Start();
    }

    private void GenerateFlagAnimation()
    {
        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
        {
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
    }

    private void GenerateJumpList()
    {
        if (IsWin7OrAbove())
        {
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
            list.JumpItems.Add(update);
            list.JumpItems.Add(scan);
            list.Apply();
        }
    }

    private void GenerateSystemTrayMenu()
    {
        try
        {
            MenuItem closeMenuItem = new(Translation.GetResStringValue("EXIT"));
            MenuItem settingsMenuItem = new(Translation.GetResStringValue("SETTİNGS"));
            MenuItem updateMenuItem = new(Translation.GetResStringValue("UPDATE"));
            closeMenuItem.Click += (s, e) => Application.Current?.Windows?.Cast<Window>()?.FirstOrDefault().Close();
            settingsMenuItem.Click += (s, e) =>
                                      {
                                          if (OpenSettings.CanExecute(null))
                                          {
                                              OpenSettings.Execute(null);
                                          }
                                      };
            updateMenuItem.Click += (s, e) =>
                                    {
                                        if (CheckUpdate.CanExecute(null))
                                        {
                                            CheckUpdate.Execute(null);
                                        }
                                    };
            ContextMenu menu = new();
            _ = menu.MenuItems.Add(settingsMenuItem);
            _ = menu.MenuItems.Add(updateMenuItem);
            _ = menu.MenuItems.Add("-");
            _ = menu.MenuItems.Add(closeMenuItem);
            AppNotifyIcon = new()
            {
                BalloonTipText = $"{AppName} Sistem Tepsisine Gönderildi.",
                BalloonTipTitle = AppName,
                Text = AppName,
                Visible = true,
                ContextMenu = menu,
                Icon = new System.Drawing.Icon(Application.GetResourceStream(new Uri("pack://application:,,,/GpScanner;component/scanner.ico")).Stream),
            };

            AppNotifyIcon.MouseClick += (s, e) =>
                                        {
                                            Application.Current?.Windows?.Cast<Window>()?.FirstOrDefault()?.Show();
                                            Application.Current.Windows.Cast<Window>().FirstOrDefault().WindowState = WindowState.Maximized;
                                        };
        }
        catch (Exception ex)
        {
            throw new ArgumentException(ex?.Message);
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
            return new ObservableCollection<ContributionData>(collection);
        }
        catch (Exception ex)
        {
            await WriteToLogFile($@"{ProfileFolder}\{ErrorFile}", ex?.Message);
        }
        return null;
    }

    private List<ScannerFileDatas> GetContributionFiles()
    {
        return Dosyalar?.Select(
            scanner =>
            {
                string parentDirectoryName = Directory.GetParent(scanner.FileName)?.Name;
                _ = DateTime.TryParse(parentDirectoryName, out DateTime parsedDateTime);
                return new ScannerFileDatas() { Scanner = scanner, ParentDate = parsedDateTime };
            })
        .ToList();
    }

    private ObservableCollection<Scanner> GetScannerFileData()
    {
        if (Directory.Exists(Twainsettings.Settings.Default.AutoFolder))
        {
            ObservableCollection<Scanner> list = [];
            try
            {
                List<string> files = FastFileSearch.EnumerateFilepaths(Twainsettings.Settings.Default.AutoFolder).Where(s => supportedfilesextension.Contains(Path.GetExtension(s).ToLower())).ToList();
                files.Sort(new StrCmpLogicalComparer());
                foreach (string dosya in files)
                {
                    FileInfo fi = new(dosya);
                    list.Add(new Scanner { FileName = dosya, FolderName = fi.Directory.Name, FileSize = fi.Length / 1048576F });
                }
                files = null;
                return list;
            }
            catch (UnauthorizedAccessException)
            {
                return list;
            }
        }

        return null;
    }

    private async Task<ObservableCollection<string>> GetUnindexedFileData()
    {
        try
        {
            if (Dosyalar is not null)
            {
                List<string> unindexedfiles = Dosyalar.Where(z => unindexedfileextensions.Contains(Path.GetExtension(z.FileName.ToLower()))).Select(z => z.FileName).ToList();
                using AppDbContext context = new();
                List<string> scannedFiles = (await context.Data.AsNoTracking().ToListAsync())?.Select(x => x.FileName).ToList();

                if (unindexedfiles != null && scannedFiles != null)
                {
                    return new ObservableCollection<string>(unindexedfiles.Except(scannedFiles));
                }
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
            return new ObservableCollection<ReminderData>([.. (await context.ReminderData?.AsNoTracking().ToListAsync())?.Where(z => z.Seen)?.OrderBy(z => z.Tarih)]);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async void GpScannerViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "SeçiliGün")
        {
            if (!string.IsNullOrWhiteSpace(AramaMetni))
            {
                AramaMetni = string.Empty;
            }
            string format = Twainsettings.Settings.Default.FolderDateFormat;
            if (MainWindow.cvs is not null)
            {
                MainWindow.cvs.Filter += (s, x) =>
                                         {
                                             Scanner scanner = (Scanner)x.Item;
                                             if (DateTime.TryParse(Directory.GetParent(scanner?.FileName).Name, out DateTime result))
                                             {
                                                 string seçiligün = SeçiliGün.ToString(format);
                                                 x.Accepted = result.ToString(format).StartsWith(seçiligün);
                                             }
                                             else
                                             {
                                                 x.Accepted = false;
                                             }
                                         };
            }
        }

        if (e.PropertyName is "SelectedContribution" && SelectedContribution is not null)
        {
            SeçiliGün = (DateTime)SelectedContribution.ContrubutionDate;
        }

        if (e.PropertyName is "AramaMetni")
        {
            if (string.IsNullOrWhiteSpace(AramaMetni))
            {
                OnPropertyChanged(nameof(SeçiliGün));
                return;
            }

            var datas = await Task.Run(
                () =>
                {
                    ZipProgressIndeterminate = true;
                    using AppDbContext context = new();
                    return context.Data.AsNoTracking().ToList().Where(z => z.FileContent?.IndexOf(AramaMetni, StringComparison.CurrentCultureIgnoreCase) >= 0).Select(z => new { z.FileName }).ToList();
                });

            MainWindow.cvs.Filter += (s, x) =>
                                     {
                                         Scanner scanner = (Scanner)x.Item;
                                         x.Accepted = Path.GetFileNameWithoutExtension(scanner.FileName).IndexOf(AramaMetni, StringComparison.CurrentCultureIgnoreCase) >= 0 || datas?.Any(z => z.FileName == scanner.FileName) == true;
                                     };
            ZipProgressIndeterminate = false;
            datas = null;
        }

        if (e.PropertyName is "SeçiliDil")
        {
            ChangeApplicationLanguage(SeçiliDil);
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
                if (Path.GetExtension(item.FileName.ToLower()) == ".pdf")
                {
                    compressedfiles.Add(new BatchPdfData() { Filename = item.FileName });
                }
            }
            BurnFiles = burnfiles;
            CompressedFiles = compressedfiles;
        }

        if (e.PropertyName is "SelectedContributionYear")
        {
            List<ScannerFileDatas> files = GetContributionFiles();
            if (files?.Any() == true)
            {
                DateTime firstdate = new(SelectedContributionYear, 1, 1);
                DateTime lastdate = new(SelectedContributionYear, 12, 31);
                ContributionData = await GetContributionData(files, firstdate, lastdate);
                ContributionDocumentCount = ContributionData.Sum(z => z.Count);
            }
        }

        if (Settings.Default.NotifyCalendar && ScannerData?.Reminder?.Any(z => z.Tarih < DateTime.Today.AddDays(Settings.Default.NotifyCalendarDateValue)) == true)
        {
            CalendarPanelIsExpanded = true;
        }
    }

    private void InitializeBatchFiles(out List<string> files, out int slicecount, out Scanner scanner, out List<Task> Tasks)
    {
        files = FastFileSearch.EnumerateFilepaths(BatchFolder).Where(s => batchimagefileextensions.Any(ext => ext == Path.GetExtension(s).ToLower())).ToList();
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
        return os.Major > 6 || (os.Major == 6 && os.Minor >= 1);
    }

    private async Task LoadRemainderDatas() => ScannerData = new ScannerData { Reminder = await ReminderYükle(), GörülenReminder = await GörülenReminderYükle() };

    private ObservableCollection<BatchFiles> OrderBatchFiles(ObservableCollection<BatchFiles> batchFolderProcessedFileList) => new(batchFolderProcessedFileList.OrderBy(z => z.Name, new StrCmpLogicalComparer()));

    private async void RegisterSimplePdfFileWatcher()
    {
        string autoFolder = Twainsettings.Settings.Default.AutoFolder;
        if (!string.IsNullOrWhiteSpace(autoFolder))
        {
            try
            {
                FileSystemWatcher watcher = new(autoFolder) { NotifyFilter = NotifyFilters.FileName, Filter = "*.pdf", IncludeSubdirectories = true, EnableRaisingEvents = true };
                watcher.Renamed += (s, e) =>
                                   {
                                       using (AppDbContext context = new())
                                       {
                                           foreach (Data item in context?.Data?.Where(z => z.FileName == e.OldFullPath))
                                           {
                                               item.FileName = e.FullPath;
                                           }
                                           _ = context.SaveChanges();
                                       }
                                       Dosyalar = GetScannerFileData();
                                   };
            }
            catch (Exception ex)
            {
                await WriteToLogFile($@"{ProfileFolder}\{ErrorFile}", ex?.Message);
            }
        }
    }

    private async Task<ObservableCollection<ReminderData>> ReminderYükle()
    {
        try
        {
            using AppDbContext context = new();
            return new ObservableCollection<ReminderData>([.. (await context.ReminderData?.AsNoTracking().ToListAsync())?.Where(z => z.Tarih > DateTime.Today && !z.Seen)?.OrderBy(z => z.Tarih)]);
        }
        catch (Exception)
        {
            return null;
        }
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