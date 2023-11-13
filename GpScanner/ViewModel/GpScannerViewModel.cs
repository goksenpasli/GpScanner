﻿using Extensions;
using GpScanner.Properties;
using Microsoft.SharePoint.Client;
using Ocr;
using PdfCompressor;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
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
using File = System.IO.File;
using FlowDirection = System.Windows.FlowDirection;
using InpcBase = Extensions.InpcBase;
using ListBox = System.Windows.Controls.ListBox;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Twainsettings = TwainControl.Properties;

namespace GpScanner.ViewModel;

public class BatchFiles : TessFiles;

public class GpScannerViewModel : InpcBase
{
    public Task Filesavetask;
    public CancellationTokenSource ocrcancellationToken;
    private static DispatcherTimer timer;
    private readonly string AppName = Application.Current?.MainWindow?.Title;
    private readonly List<string> batchimagefileextensions = [".tiff", ".tıf", ".tıff", ".tif", ".jpg", ".jpe", ".gif", ".jpeg", ".jfif", ".jfıf", ".png", ".bmp"];
    private readonly string[] supportedfilesextension = [".pdf", ".eyp", ".tıff", ".tıf", ".tiff", ".tif", ".jpg", ".png", ".bmp", ".zip", ".xps", ".mp4", ".3gp", ".wmv", ".mpg", ".mov", ".avi", ".mpeg", ".xml", ".xsl", ".xslt", ".xaml"];
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
    private int checkedPdfCount = 0;
    private ObservableCollection<BatchPdfData> compressedFiles = [];
    private ObservableCollection<ContributionData> contributionData;
    private int cycleIndex;
    private bool detectBarCode = true;
    private bool detectPageSeperator;
    private bool documentPanelIsExpanded;
    private ObservableCollection<Scanner> dosyalar;
    private ObservableCollection<string> fileSystemWatcherProcessedFileList;
    private double fold = 0.3;
    private string ftpPassword = string.Empty;
    private string ftpSite = string.Empty;
    private string ftpUserName = string.Empty;
    private int ındexedFileCount;
    private FlowDirection langFlowDirection = FlowDirection.LeftToRight;
    private bool listBoxBorderAnimation;
    private GridLength mainWindowDocumentGuiControlLength = new(1, GridUnitType.Star);
    private GridLength mainWindowGuiControlLength = new(3, GridUnitType.Star);
    private double mirror;
    private DateTime notifyDate = DateTime.Today;
    private bool ocrısBusy;
    private int? ocrPdfThumbnailPageNumber;
    private string patchFileName;
    private string patchProfileName = string.Empty;
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
    private Scanner selectedDocument;
    private string selectedFtp;
    private Size selectedSize;
    private bool shutdown;
    private bool sıralama;
    private TesseractViewModel tesseractViewModel;
    private TranslateViewModel translateViewModel;
    private double zipProgress;
    private bool zipProgressIndeterminate;

    public GpScannerViewModel()
    {
        if (string.IsNullOrWhiteSpace(Settings.Default.DatabaseFile))
        {
            XmlDataPath = Settings.Default.DatabaseFile = $@"{Path.GetDirectoryName(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath)}\Data.xml";
            Settings.Default.Save();
        }
        RegisterSimplePdfFileWatcher();
        TesseractViewModel = new TesseractViewModel();
        TranslateViewModel = new TranslateViewModel();
        Settings.Default.PropertyChanged += Default_PropertyChanged;
        PropertyChanged += GpScannerViewModel_PropertyChanged;

        GenerateAnimationTimer();
        Dosyalar = GetScannerFileData();
        SeçiliDil = Settings.Default.DefaultLang;
        GenerateJumpList();
        SeçiliGün = DateTime.Today;
        SelectedSize = GetPreviewSize[Settings.Default.PreviewIndex];
        ScannerData = new ScannerData { Data = DataYükle(), Reminder = ReminderYükle() };
        UnIndexedFiles = GetUnindexedFileData();

        if (Settings.Default.NotifyCalendar && ScannerData?.Reminder?.Any(z => z.Tarih < DateTime.Today.AddDays(Settings.Default.NotifyCalendarDateValue)) == true)
        {
            CalendarPanelIsExpanded = true;
        }

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
                                List<string> pdffilelist = Dosyalar.Where(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
                                                           .Select(z => z.FileName)
                                                           .ToList();
                                pdffilelist.ToArray().MergePdf().Save(saveFileDialog.FileName);
                            });
                    }
                    catch (Exception ex)
                    {
                        _ = MessageBox.Show(ex.Message, AppName, MessageBoxButton.OK, MessageBoxImage.Error);
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
                    try
                    {
                        Task task = Task.Run(
                            () =>
                            {
                                List<string> pdffilelist = Dosyalar.Where(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
                                                           .Select(z => z.FileName)
                                                           .ToList();
                                using ZipArchive archive = ZipFile.Open(saveFileDialog.FileName, ZipArchiveMode.Update);
                                for (int i = 0; i < pdffilelist.Count; i++)
                                {
                                    string fPath = pdffilelist[i];
                                    _ = archive.CreateEntryFromFile(fPath, Path.GetFileName(fPath));
                                    ZipProgress = (i + 1) / (double)pdffilelist.Count;
                                }
                                ZipProgressIndeterminate = true;
                            });
                        await task;
                        if (task?.IsCompleted == true)
                        {
                            ZipProgress = 0;
                            ZipProgressIndeterminate = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        _ = MessageBox.Show(ex.Message, AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
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
                        TranslateViewModel.TaramaGeçmiş.Add(TranslateViewModel.Metin);
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
                if (parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    byte[] filedata = await PdfViewer.PdfViewer.ReadAllFileAsync(pdfviewer.PdfFilePath);
                    if (filedata != null)
                    {
                        OcrIsBusy = true;
                        ObservableCollection<OcrData> ocrdata;
                        if (Keyboard.Modifiers == ModifierKeys.Alt)
                        {
                            for (int i = 1; i <= pdfviewer.ToplamSayfa; i++)
                            {
                                using MemoryStream ms = await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, i, Twainsettings.Settings.Default.ImgLoadResolution);
                                ocrdata = await ms.ToArray().OcrAsync(Settings.Default.DefaultTtsLang);
                                ScannerData.Data.Add(new Data { Id = DataSerialize.RandomNumber(), FileName = pdfviewer.PdfFilePath, FileContent = string.Join(" ", ocrdata?.Select(z => z.Text)) });
                                OcrPdfThumbnailPageNumber = i;
                            }
                            OcrPdfThumbnailPageNumber = null;
                        }
                        else
                        {
                            using MemoryStream ms = await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, pdfviewer.Sayfa, Twainsettings.Settings.Default.ImgLoadResolution);
                            ocrdata = await ms.ToArray().OcrAsync(Settings.Default.DefaultTtsLang);
                            ScannerData.Data.Add(new Data { Id = DataSerialize.RandomNumber(), FileName = pdfviewer.PdfFilePath, FileContent = string.Join(" ", ocrdata?.Select(z => z.Text)) });
                        }

                        DatabaseSave.Execute(null);
                        filedata = null;
                        ocrdata = null;
                        OcrIsBusy = false;
                    }
                }
            },
            parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang) && (Settings.Default.ThumbMultipleOcrEnabled || !OcrIsBusy));

        UnindexedFileOcr = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is string pdffilepath && PdfViewer.PdfViewer.IsValidPdfFile(pdffilepath))
                {
                    OcrIsBusy = true;
                    BitmapImage bitmapImage = await PdfViewer.PdfViewer.ConvertToImgAsync(pdffilepath, 1, Twainsettings.Settings.Default.ImgLoadResolution);
                    ObservableCollection<OcrData> ocrdata = await bitmapImage.ToTiffJpegByteArray(Format.Jpg).OcrAsync(Settings.Default.DefaultTtsLang);
                    string ocrtext = string.Join(" ", ocrdata?.Select(z => z.Text));
                    if (string.IsNullOrEmpty(ocrtext))
                    {
                        ocrtext = " ";
                    }
                    ScannerData.Data.Add(new Data { Id = DataSerialize.RandomNumber(), FileName = pdffilepath, FileContent = ocrtext });
                    DatabaseSave.Execute(null);
                    ocrdata = null;
                    OcrIsBusy = false;
                    _ = UnIndexedFiles?.Remove(pdffilepath);
                }
            },
            parameter => parameter is string pdffilepath && File.Exists(pdffilepath) && !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang));

        UnindexedAllFilesOcr = new RelayCommand<object>(
            async parameter =>
            {
                int i = 0;
                foreach (string unIndexedFile in UnIndexedFiles.ToList())
                {
                    OcrIsBusy = true;
                    string item = unIndexedFile;
                    BitmapImage bitmapImage = await PdfViewer.PdfViewer.ConvertToImgAsync(item, 1, Twainsettings.Settings.Default.ImgLoadResolution);
                    ObservableCollection<OcrData> ocrdata = await bitmapImage.ToTiffJpegByteArray(Format.Jpg).OcrAsync(Settings.Default.DefaultTtsLang);
                    string ocrtext = string.Join(" ", ocrdata?.Select(z => z.Text));
                    if (string.IsNullOrEmpty(ocrtext))
                    {
                        ocrtext = " ";
                    }
                    ScannerData.Data.Add(new Data { Id = DataSerialize.RandomNumber(), FileName = item, FileContent = ocrtext });
                    DatabaseSave.Execute(null);
                    ocrdata = null;
                    OcrIsBusy = false;
                    _ = UnIndexedFiles?.Remove(item);
                    IndexedFileCount = i + 1;
                    i++;
                    GC.Collect();
                }
            },
            parameter => !OcrIsBusy && UnIndexedFiles?.Count > 0 && !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang));

        WordOcrPdfThumbnailPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    byte[] filedata = await PdfViewer.PdfViewer.ReadAllFileAsync(pdfviewer.PdfFilePath);
                    if (filedata != null)
                    {
                        OcrIsBusy = true;
                        ObservableCollection<OcrData> ocrdata;
                        using MemoryStream ms = await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, pdfviewer.Sayfa, Twainsettings.Settings.Default.ImgLoadResolution);
                        ocrdata = await ms.ToArray().WordFileOcrAsync(Settings.Default.DefaultTtsLang);
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
                }
            },
            parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang) && !OcrIsBusy);

        OpenOriginalFile = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is string filepath)
                {
                    DocumentViewerWindow documentViewerWindow = new();
                    if (documentViewerWindow.DataContext is DocumentViewerModel documentViewerModel)
                    {
                        documentViewerWindow.Owner = Application.Current?.MainWindow;
                        documentViewerModel.Scanner = ToolBox.Scanner;
                        documentViewerModel.FilePath = filepath;
                        string directory = Path.GetDirectoryName(documentViewerModel.FilePath);
                        List<string> files = Directory.EnumerateFiles(directory, "*.*").Where(z => supportedfilesextension.Any(ext => ext == Path.GetExtension(z).ToLower())).ToList();
                        files.Sort(new StrCmpLogicalComparer());
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
                OpenFileDialog openFileDialog = new() { Filter = "Xml Dosyası(*.xml)|*.xml", FileName = "Data.xml" };
                if (openFileDialog.ShowDialog() == true)
                {
                    XmlDataPath = Settings.Default.DatabaseFile = openFileDialog.FileName;
                    Settings.Default.Save();
                }
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

        CheckUpdate = new RelayCommand<object>(
            parameter =>
            {
                FileVersionInfo version = FileVersionInfo.GetVersionInfo(Process.GetCurrentProcess().MainModule.FileName);
                _ = Process.Start(
                    "twux32.exe",
                    $"/w:{new WindowInteropHelper(Application.Current.MainWindow).Handle} https://github.com/goksenpasli/GpScanner/releases/download/{version.FileMajorPart}.{version.FileMinorPart}/GpScanner-Setup.txt");
                Settings.Default.LastCheckDate = DateTime.Now;
                Settings.Default.Save();
            },
            parameter => Policy.CheckPolicy("CheckUpdate"));

        SavePatchProfile = new RelayCommand<object>(
            parameter =>
            {
                string profile = $"{PatchFileName}|{PatchTag}";
                _ = Settings.Default.PatchCodes.Add(profile);
                Settings.Default.Save();
                Settings.Default.Reload();
            },
            parameter => !string.IsNullOrWhiteSpace(PatchFileName) &&
            !string.IsNullOrWhiteSpace(PatchTag) &&
            !Settings.Default.PatchCodes.Cast<string>().Select(z => z.Split('|')[0]).Contains(PatchFileName) &&
            TwainCtrl.FileNameValid(PatchTag));

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
                SaveFileDialog saveFileDialog = new() { Filter = "Jpg Resmi (*.jpg)|*.jpg", FileName = "QR" };
                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllBytes(saveFileDialog.FileName, BitmapFrame.Create(parameter as WriteableBitmap).ToTiffJpegByteArray(Format.Jpg));
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
                    DetectPageSeperator = false;
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
                List<string> files = FastFileSearch.EnumerateFilepaths(BatchFolder).Where(s => batchimagefileextensions.Any(ext => ext == Path.GetExtension(s).ToLower())).ToList();
                int slicecount = files.Count > Settings.Default.ProcessorCount ? files.Count / Settings.Default.ProcessorCount : 1;
                Scanner scanner = ToolBox.Scanner;
                scanner.ProgressState = TaskbarItemProgressState.Normal;
                BatchTxtOcrs = [];
                List<Task> Tasks = [];
                ocrcancellationToken = new CancellationTokenSource();
                foreach (List<string> item in TwainCtrl.ChunkBy(files, slicecount))
                {
                    if (item.Count > 0)
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
                                            BitmapFrame.Create(new Uri(item.ElementAtOrDefault(i)))
                                            .GeneratePdf(scannedText, Format.Jpg, paper, Twainsettings.Settings.Default.JpegQuality, Twainsettings.Settings.Default.ImgLoadResolution)
                                            .Save(pdffile);
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
            },
            parameter => BatchFolderProcessedFileList?.Any(z => z.Checked) == true);

        CancelBatchOcr = new RelayCommand<object>(
            parameter =>
            {
                if (MessageBox.Show(
                    $"{Translation.GetResStringValue("TRANSLATEPENDING")}\n{Translation.GetResStringValue("RESET")}",
                    AppName,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No) ==
                MessageBoxResult.Yes)
                {
                    ocrcancellationToken?.Cancel();
                    BatchTxtOcrs = null;
                }
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

                List<string> files = FastFileSearch.EnumerateFilepaths(BatchFolder).Where(s => batchimagefileextensions.Any(ext => ext == Path.GetExtension(s).ToLower())).ToList();
                int slicecount = files.Count > Settings.Default.ProcessorCount ? files.Count / Settings.Default.ProcessorCount : 1;
                Scanner scanner = ToolBox.Scanner;
                scanner.ProgressState = TaskbarItemProgressState.Normal;
                BatchTxtOcrs = [];
                List<Task> Tasks = [];
                ocrcancellationToken = new CancellationTokenSource();
                foreach (List<string> item in TwainCtrl.ChunkBy(files, slicecount))
                {
                    if (item.Count > 0)
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

        DatabaseSave = new RelayCommand<object>(parameter => ScannerData.Serialize());

        ResetSettings = new RelayCommand<object>(
            parameter =>
            {
                if (MessageBox.Show($"{Translation.GetResStringValue("SETTİNGS")} {Translation.GetResStringValue("RESET")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) ==
                MessageBoxResult.Yes)
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
                    if (currentFile is not null)
                    {
                        listBox.ScrollIntoView(currentFile);
                        currentFile.BorderAnimation = true;
                        cycleIndex = (cycleIndex + 1) % listboxFiles.Count(z => z.Seçili);
                        await Task.Delay(1000);
                        currentFile.BorderAnimation = false;
                    }
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

        AddToCalendar = new RelayCommand<object>(
            parameter =>
            {
                ReminderData reminderData = new() { Açıklama = CalendarDesc, Tarih = NotifyDate, FileName = (parameter as Scanner)?.FileName, Id = DataSerialize.RandomNumber() };
                ScannerData.Reminder.Add(reminderData);
                DatabaseSave.Execute(null);
                CalendarDesc = null;
            },
            parameter => parameter is Scanner scanner && File.Exists(scanner?.FileName) && !string.IsNullOrWhiteSpace(CalendarDesc));

        ApplyCalendarData = new RelayCommand<object>(
            parameter =>
            {
                DatabaseSave.Execute(null);
                ScannerData.Reminder = ReminderYükle();
            },
            parameter => ScannerData?.Reminder?.Any() == true);

        LoadContributionData = new RelayCommand<object>(parameter => ContributionData = GetContributionData(), parameter => true);
    }

    public static bool IsAdministrator
    {
        get
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public static string XmlDataPath { get; set; } = Settings.Default.DatabaseFile;

    public ICommand AddFtpSites { get; }

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

    public ICommand CycleSelectedDocuments { get; }

    public ICommand DatabaseSave { get; }

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

    public bool DetectPageSeperator
    {
        get => detectPageSeperator;

        set
        {
            if (detectPageSeperator != value)
            {
                detectPageSeperator = value;
                OnPropertyChanged(nameof(DetectPageSeperator));
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

    public ObservableCollection<Size> GetPreviewSize => new() { new Size(190, 305), new Size(230, 370), new Size(330, 530), new Size(380, 610), new Size(425, 645) };

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
        get => patchProfileName;

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

    public ICommand SavePatchProfile { get; }

    public ICommand SaveQrImage { get; }

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

    public Scanner SelectedDocument
    {
        get => selectedDocument;

        set
        {
            if (selectedDocument != value)
            {
                selectedDocument = value;
                OnPropertyChanged(nameof(SelectedDocument));
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

    public bool Sıralama
    {
        get => sıralama;

        set
        {
            if (sıralama != value)
            {
                sıralama = value;
                OnPropertyChanged(nameof(Sıralama));
            }
        }
    }

    public ICommand StartPdfBatch { get; }

    public ICommand StartTxtBatch { get; }

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

    public ObservableCollection<string> UnIndexedFiles { get; set; }

    public RelayCommand<object> UnindexedAllFilesOcr { get; }

    public RelayCommand<object> UnindexedFileOcr { get; }

    public ICommand UnRegisterSti { get; }

    public RelayCommand<object> UploadSharePoint { get; }

    public RelayCommand<object> WordOcrPdfThumbnailPage { get; }

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

    public static void BackupDataXmlFile()
    {
        if (File.Exists(Settings.Default.DatabaseFile))
        {
            FileInfo fi = new(Settings.Default.DatabaseFile);
            if (fi.Length > 0)
            {
                File.Copy(fi.FullName, $"{fi.FullName}{DateTime.Today.DayOfWeek}.bak", true);
            }
        }
    }

    public static ObservableCollection<Data> DataYükle()
    {
        try
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                return null;
            }

            if (File.Exists(XmlDataPath))
            {
                return XmlDataPath.DeSerialize<ScannerData>()?.Data;
            }

            _ = Directory.CreateDirectory(Path.GetDirectoryName(XmlDataPath));
            return [];
        }
        catch (Exception ex)
        {
            _ = MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
    }

    public void AddBarcodeToList(string barcodecontent)
    {
        if (!string.IsNullOrWhiteSpace(barcodecontent))
        {
            BarcodeList?.Add(barcodecontent);
        }
    }

    public string GetPatchCodeResult(string barcode)
    {
        if (!string.IsNullOrWhiteSpace(barcode))
        {
            List<string> patchcodes = Settings.Default.PatchCodes.Cast<string>().ToList();
            string matchingPatchCode = patchcodes?.Find(z => z.Split('|')[0] == barcode);
            return matchingPatchCode != null ? matchingPatchCode.Split('|')[1] : "Tarama";
        }

        return string.Empty;
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

    public void ReloadFileDatas()
    {
        Dosyalar = GetScannerFileData();
        SeçiliGün = DateTime.Today;
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
                                                 ? BitmapFrame.Create(new Uri(currentfilepath))
                                                   .GeneratePdf(scannedText, Format.Jpg, paper, Twainsettings.Settings.Default.JpegQuality, Twainsettings.Settings.Default.ImgLoadResolution)
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

    private ObservableCollection<ContributionData> GetContributionData()
    {
        try
        {
            ObservableCollection<ContributionData> contributiondata = [];
            var files = Dosyalar?.Select(
                scanner =>
                {
                    string parentDirectoryName = Directory.GetParent(scanner.FileName)?.Name;
                    _ = DateTime.TryParse(parentDirectoryName, out DateTime parsedDateTime);
                    return new { Scanner = scanner, ParentDate = parsedDateTime };
                })
                                 .ToList();
            if (files?.Any() == true)
            {
                DateTime first = files.Where(z => z.ParentDate > DateTime.MinValue).Min(z => z.ParentDate);
                DateTime last = files.Max(z => z.ParentDate);
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
                return new ObservableCollection<ContributionData>(contributiondata.Where(z => z.ContrubutionDate >= DateTime.Today.AddYears(-1)).Take(53 * 7).OrderBy(z => z.ContrubutionDate));
            }
        }
        catch (Exception)
        {
        }
        return null;
    }

    private ObservableCollection<Scanner> GetScannerFileData()
    {
        if (Directory.Exists(Twainsettings.Settings.Default.AutoFolder))
        {
            ObservableCollection<Scanner> list = [];
            try
            {
                List<string> files = Directory
                    .EnumerateFiles(Twainsettings.Settings.Default.AutoFolder, "*.*", SearchOption.AllDirectories)
                                     .Where(s => supportedfilesextension.Contains(Path.GetExtension(s).ToLower()))
                                     .ToList();
                files.Sort(new StrCmpLogicalComparer());
                foreach (string dosya in files)
                {
                    list.Add(new Scanner { FileName = dosya, FolderName = Directory.GetParent(dosya).Name });
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

    private ObservableCollection<string> GetUnindexedFileData()
    {
        try
        {
            if (Dosyalar == null || ScannerData == null)
            {
                return null;
            }

            IEnumerable<string> pdfFiles = Dosyalar.Where(z => string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)).Select(z => z.FileName);

            IEnumerable<string> scannedFiles = ScannerData.Data?.Where(x => !string.IsNullOrEmpty(x.FileContent)).Select(x => x.FileName);

            return pdfFiles == null || scannedFiles == null ? null : new ObservableCollection<string>(pdfFiles.Except(scannedFiles));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void GpScannerViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "SeçiliGün")
        {
            MainWindow.cvs.Filter += (s, x) =>
                                     {
                                         Scanner scanner = x.Item as Scanner;
                                         if (DateTime.TryParse(Directory.GetParent(scanner?.FileName).Name, out DateTime result))
                                         {
                                             string seçiligün = SeçiliGün.ToString(Twainsettings.Settings.Default.FolderDateFormat);
                                             x.Accepted = result.ToString(Twainsettings.Settings.Default.FolderDateFormat).StartsWith(seçiligün);
                                         }
                                         else
                                         {
                                             x.Accepted = false;
                                         }
                                     };
        }

        if (e.PropertyName is "SelectedContribution" && SelectedContribution is not null)
        {
            SeçiliGün = (DateTime)SelectedContribution.ContrubutionDate;
        }

        if (e.PropertyName is "Sıralama")
        {
            MainWindow.cvs?.SortDescriptions.Clear();
            SortDescription sortDescription = Sıralama ? new SortDescription("FileName", ListSortDirection.Descending) : new SortDescription("FileName", ListSortDirection.Ascending);
            MainWindow.cvs?.SortDescriptions.Add(sortDescription);
        }

        if (e.PropertyName is "AramaMetni")
        {
            if (string.IsNullOrWhiteSpace(AramaMetni))
            {
                OnPropertyChanged(nameof(SeçiliGün));
                return;
            }

            MainWindow.cvs.Filter += (s, x) =>
                                     {
                                         Scanner scanner = x.Item as Scanner;
                                         x.Accepted = Path.GetFileNameWithoutExtension(scanner?.FileName).Contains(AramaMetni, StringComparison.OrdinalIgnoreCase) ||
                                         ScannerData.Data.Any(z => z.FileName == scanner?.FileName && z.FileContent?.Contains(AramaMetni, StringComparison.OrdinalIgnoreCase) == true);
                                     };
        }

        if (e.PropertyName is "SeçiliDil")
        {
            LangFlowDirection = FlowDirection.LeftToRight;
            switch (SeçiliDil)
            {
                case "TÜRKÇE":
                    TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                    CalendarLang = XmlLanguage.GetLanguage("tr-TR");
                    TesseractViewModel.SeçiliDil = "Turkish";
                    break;

                case "ENGLISH":
                    TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("en-EN");
                    CalendarLang = XmlLanguage.GetLanguage("en-EN");
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
            }

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
                compressedfiles.Add(new BatchPdfData() { Filename = item.FileName });
            }
            BurnFiles = burnfiles;
            CompressedFiles = compressedfiles;
        }
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

    private ObservableCollection<BatchFiles> OrderBatchFiles(ObservableCollection<BatchFiles> batchFolderProcessedFileList) => new(batchFolderProcessedFileList.OrderBy(z => z.Name, new StrCmpLogicalComparer()));

    private void RegisterSimplePdfFileWatcher()
    {
        string autoFolder = Twainsettings.Settings.Default.AutoFolder;
        if (!string.IsNullOrWhiteSpace(autoFolder))
        {
            FileSystemWatcher watcher = new(autoFolder) { NotifyFilter = NotifyFilters.FileName, Filter = "*.pdf", IncludeSubdirectories = true, EnableRaisingEvents = true };
            watcher.Renamed += (s, e) =>
                               {
                                   foreach (Data item in ScannerData?.Data?.Where(z => z.FileName == e.OldFullPath))
                                   {
                                       item.FileName = e.FullPath;
                                   }

                                   DatabaseSave.Execute(null);
                                   Dosyalar = GetScannerFileData();
                               };
        }
    }

    private ObservableCollection<ReminderData> ReminderYükle()
    {
        try
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                return null;
            }

            if (File.Exists(XmlDataPath))
            {
                return new ObservableCollection<ReminderData>(XmlDataPath.DeSerialize<ScannerData>()?.Reminder?.Where(z => z.Tarih > DateTime.Today && !z.Seen)?.OrderBy(z => z.Tarih));
            }

            _ = Directory.CreateDirectory(Path.GetDirectoryName(XmlDataPath));
            return [];
        }
        catch (Exception ex)
        {
            _ = MessageBox.Show(ex.Message, AppName, MessageBoxButton.OK, MessageBoxImage.Error);
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
}