﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Extensions;
using GpScanner.Properties;
using Microsoft.Win32;
using Ocr;
using PdfSharp.Pdf;
using TwainControl;
using static Extensions.ExtensionMethods;
using InpcBase = Extensions.InpcBase;
using Twainsettings = TwainControl.Properties;

namespace GpScanner.ViewModel
{
    public class GpScannerViewModel : InpcBase
    {
        public Task Filesavetask;

        public CancellationTokenSource ocrcancellationToken;

        public GpScannerViewModel()
        {
            if (string.IsNullOrWhiteSpace(Settings.Default.DatabaseFile))
            {
                XmlDataPath = Settings.Default.DatabaseFile = Path.GetDirectoryName(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath) + @"\Data.xml";
                Settings.Default.Save();
            }
            if (Settings.Default.WatchFolderPdfFileChange && AnyDataExists)
            {
                RegisterSimplePdfFileWatcher();
            }

            PdfGeneration.Scanner.SelectedTtsLanguage = Settings.Default.DefaultTtsLang;
            Settings.Default.PropertyChanged += Default_PropertyChanged;
            PropertyChanged += GpScannerViewModel_PropertyChanged;

            GenerateFoldTimer();
            Dosyalar = GetScannerFileData();
            ChartData = GetChartsData();
            SeçiliDil = Settings.Default.DefaultLang;
            SeçiliGün = DateTime.Today;
            SelectedSize = GetPreviewSize[Settings.Default.PreviewIndex];
            ScannerData = new ScannerData() { Data = DataYükle() };
            TesseractViewModel = new TesseractViewModel();
            TranslateViewModel = new TranslateViewModel();

            RegisterSti = new RelayCommand<object>(parameter => StillImageHelper.Register(), parameter => true);

            UnRegisterSti = new RelayCommand<object>(parameter => StillImageHelper.Unregister(), parameter => true);

            PdfBirleştir = new RelayCommand<object>(async parameter =>
            {
                if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                {
                    await Task.Run(() =>
                    {
                        using PdfDocument outputDocument = new();
                        IEnumerable<string> pdffilelist = Dosyalar.Where(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)).Select(z => z.FileName);
                        pdffilelist.ToArray().MergePdf().Save(PdfGeneration.GetPdfScanPath());
                        ReloadFileDatas();
                    });
                    return;
                }
                SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Pdf Dosyası(*.pdf)|*.pdf",
                    FileName = Translation.GetResStringValue("MERGE")
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        await Task.Run(() =>
                        {
                            using PdfDocument outputDocument = new();
                            IEnumerable<string> pdffilelist = Dosyalar.Where(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)).Select(z => z.FileName);
                            pdffilelist.ToArray().MergePdf().Save(saveFileDialog.FileName);
                        });
                    }
                    catch (Exception ex)
                    {
                        _ = MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }, parameter =>
            {
                CheckedPdfCount = Dosyalar?.Count(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase));
                return CheckedPdfCount > 1;
            });

            OcrPage = new RelayCommand<object>(async parameter =>
            {
                if (parameter is TwainCtrl twainCtrl)
                {
                    byte[] imgdata = twainCtrl.SeçiliResim.Resim.ToTiffJpegByteArray(Format.Jpg);
                    OcrIsBusy = true;
                    ScannedText = await imgdata.OcrAsyc(Settings.Default.DefaultTtsLang);
                    if (ScannedText != null)
                    {
                        TranslateViewModel.Metin = string.Join(" ", ScannedText.Select(z => z.Text));
                        TranslateViewModel.TaramaGeçmiş.Add(TranslateViewModel.Metin);
                        OcrIsBusy = false;
                    }
                    if (DetectBarCode)
                    {
                        string result = await Task.Run(() => QrCode.QrCode.GetImageBarcodeResult(twainCtrl.SeçiliResim.Resim));
                        if (result != null)
                        {
                            BarcodeList.Add(result);
                        }
                    }
                    imgdata = null;
                    GC.Collect();
                }
            }, parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang) && parameter is TwainCtrl twainCtrl && twainCtrl.SeçiliResim is not null);

            OcrPdfThumbnailPage = new RelayCommand<object>(async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    byte[] filedata = await PdfViewer.PdfViewer.ReadAllFileAsync(pdfviewer.PdfFilePath);
                    if (filedata != null)
                    {
                        OcrIsBusy = true;
                        ObservableCollection<OcrData> ocrdata;
                        MemoryStream ms;
                        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                        {
                            for (int i = 1; i <= pdfviewer.ToplamSayfa; i++)
                            {
                                ms = await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, i, (int)Twainsettings.Settings.Default.ImgLoadResolution);
                                ocrdata = await ms.ToArray().OcrAsyc(Settings.Default.DefaultTtsLang);
                                ScannerData.Data.Add(new Data() { Id = DataSerialize.RandomNumber(), FileName = pdfviewer.PdfFilePath, FileContent = string.Join(" ", ocrdata?.Select(z => z.Text)) });
                            }
                            DatabaseSave.Execute(null);
                            filedata = null;
                            ocrdata = null;
                            ms = null;
                            OcrIsBusy = false;
                            GC.Collect();
                            return;
                        }
                        ms = await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, pdfviewer.Sayfa, (int)Twainsettings.Settings.Default.ImgLoadResolution);
                        ocrdata = await ms.ToArray().OcrAsyc(Settings.Default.DefaultTtsLang);
                        ScannerData.Data.Add(new Data() { Id = DataSerialize.RandomNumber(), FileName = pdfviewer.PdfFilePath, FileContent = string.Join(" ", ocrdata?.Select(z => z.Text)) });
                        DatabaseSave.Execute(null);
                        filedata = null;
                        ocrdata = null;
                        ms = null;
                        OcrIsBusy = false;
                        GC.Collect();
                    }
                }
            }, parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang) && !OcrIsBusy);

            OpenOriginalFile = new RelayCommand<object>(parameter =>
            {
                if (parameter is string filepath)
                {
                    DocumentViewerWindow documentViewerWindow = new();
                    if (documentViewerWindow.DataContext is DocumentViewerModel documentViewerModel)
                    {
                        documentViewerWindow.Owner = Application.Current?.MainWindow;
                        documentViewerModel.Scanner = ToolBox.Scanner;
                        documentViewerModel.PdfFilePath = filepath;
                        List<string> files = Directory.EnumerateFiles(Path.GetDirectoryName(documentViewerModel.PdfFilePath), "*.*").Where(z => supportedfilesextension.Any(ext => ext == Path.GetExtension(z).ToLower())).ToList();
                        files.Sort(new StrCmpLogicalComparer());
                        documentViewerModel.DirectoryAllPdfFiles = files;
                        documentViewerModel.Index = Array.IndexOf(documentViewerModel.DirectoryAllPdfFiles.ToArray(), documentViewerModel.PdfFilePath);
                        documentViewerWindow.Show();
                        documentViewerWindow.Lb?.ScrollIntoView(filepath);
                    }
                }
            }, parameter => parameter is string filepath && File.Exists(filepath));

            ChangeDataFolder = new RelayCommand<object>(parameter =>
            {
                OpenFileDialog openFileDialog = new()
                {
                    Filter = "Xml Dosyası(*.xml)|*.xml",
                    FileName = "Data.xml"
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    XmlDataPath = Settings.Default.DatabaseFile = openFileDialog.FileName;
                    Settings.Default.Save();
                }
            }, parameter => true);

            Tümünüİşaretle = new RelayCommand<object>(parameter =>
            {
                if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
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
            }, parameter => Dosyalar?.Count > 0);

            TümününİşaretiniKaldır = new RelayCommand<object>(parameter =>
            {
                if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
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
            }, parameter => Dosyalar?.Count > 0);

            Tersiniİşaretle = new RelayCommand<object>(parameter =>
            {
                if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
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
            }, parameter => Dosyalar?.Count > 0);

            ExploreFile = new RelayCommand<object>(parameter => OpenFolderAndSelectItem(Path.GetDirectoryName(parameter as string), Path.GetFileName(parameter as string)), parameter => true);

            CheckUpdate = new RelayCommand<object>(parameter => _ = Process.Start("twux32.exe", $"/w:{new WindowInteropHelper(Application.Current.MainWindow).Handle} https://github.com/goksenpasli/GpScanner/releases/download/2.0/GpScanner-Setup.txt"), parameter => File.Exists("twux32.exe"));

            SavePatchProfile = new RelayCommand<object>(parameter =>
            {
                StringBuilder sb = new();
                string profile = sb
                    .Append(PatchFileName)
                    .Append("|")
                    .Append(PatchTag)
                    .ToString();
                _ = Settings.Default.PatchCodes.Add(profile);
                Settings.Default.Save();
                Settings.Default.Reload();
            }, parameter => !string.IsNullOrWhiteSpace(PatchFileName) && !string.IsNullOrWhiteSpace(PatchTag) && !Settings.Default.PatchCodes.Cast<string>().Select(z => z.Split('|')[0]).Contains(PatchFileName) && PatchTag?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);

            AddFtpSites = new RelayCommand<object>(parameter =>
            {
                StringBuilder sb = new();
                string profile = sb
                    .Append(FtpSite)
                    .Append("|")
                    .Append(FtpUserName)
                    .Append("|")
                    .Append(FtpPassword.Encrypt())
                    .ToString();
                _ = Settings.Default.FtpSites.Add(profile);
                Settings.Default.Save();
                Settings.Default.Reload();
            }, parameter => !string.IsNullOrWhiteSpace(FtpSite));

            RemoveSelectedFtp = new RelayCommand<object>(parameter =>
            {
                Settings.Default.FtpSites.Remove(parameter as string);
                FtpSite = string.Empty;
                FtpUserName = string.Empty;
                FtpPassword = string.Empty;
                Settings.Default.SelectedFtp = string.Empty;
                Settings.Default.Save();
                Settings.Default.Reload();
            }, parameter => true);

            UploadFtp = new RelayCommand<object>(async parameter =>
            {
                if (parameter is Scanner scanner && File.Exists(scanner.FileName))
                {
                    string[] ftpdata = Settings.Default.SelectedFtp.Split('|');
                    await FtpUploadAsync(ftpdata[0], ftpdata[1], ftpdata[2], scanner);
                }
            }, parameter => !string.IsNullOrWhiteSpace(Settings.Default.SelectedFtp));

            SaveQrImage = new RelayCommand<object>(parameter =>
            {
                SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Jpg Resmi (*.jpg)|*.jpg",
                    FileName = "QR"
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    TwainCtrl.SaveJpgImage(BitmapFrame.Create(parameter as WriteableBitmap), saveFileDialog.FileName);
                }
            }, parameter => parameter is WriteableBitmap writeableBitmap && writeableBitmap is not null);

            RemovePatchProfile = new RelayCommand<object>(parameter =>
            {
                Settings.Default.PatchCodes.Remove(parameter as string);
                PatchProfileName = null;
                Settings.Default.Save();
                Settings.Default.Reload();
            }, parameter => true);

            ModifyGridWidth = new RelayCommand<object>(parameter =>
            {
                switch (parameter)
                {
                    case "0":
                        MainWindowDocumentGuiControlLength = new(1, GridUnitType.Star);
                        MainWindowGuiControlLength = new(3, GridUnitType.Star);
                        return;

                    case "1":
                        MainWindowDocumentGuiControlLength = new(1, GridUnitType.Star);
                        MainWindowGuiControlLength = new(0, GridUnitType.Star);
                        return;
                }
            }, parameter => true);

            SetBatchFolder = new RelayCommand<object>(parameter =>
            {
                System.Windows.Forms.FolderBrowserDialog dialog = new()
                {
                    Description = $"{Translation.GetResStringValue("GRAPH")} {Translation.GetResStringValue("FILE")}"
                };
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    BatchFolder = dialog.SelectedPath;
                }
            }, parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang));

            SetBatchWatchFolder = new RelayCommand<object>(parameter =>
            {
                System.Windows.Forms.FolderBrowserDialog dialog = new()
                {
                    Description = Translation.GetResStringValue("BATCHDESC")
                };
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    Settings.Default.BatchFolder = dialog.SelectedPath;
                    Settings.Default.Save();
                }
            }, parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang));

            StartPdfBatch = new RelayCommand<object>(async parameter =>
            {
                if (Filesavetask?.IsCompleted == false)
                {
                    _ = MessageBox.Show(Translation.GetResStringValue("TASKSRUNNING"));
                    return;
                }
                List<string> files = Win32FileScanner.EnumerateFilepaths(BatchFolder, -1).Where(s => imagefileextensions.Any(ext => ext == Path.GetExtension(s).ToLower())).ToList();
                int slicecount = files.Count > Environment.ProcessorCount ? files.Count / Environment.ProcessorCount : 1;
                Scanner scanner = ToolBox.Scanner;
                BatchTxtOcrs = new List<BatchTxtOcr>();
                List<Task> Tasks = new();
                ocrcancellationToken = new CancellationTokenSource();
                foreach (List<string> item in TwainCtrl.ChunkBy(files, slicecount))
                {
                    if (item.Count > 0)
                    {
                        BatchTxtOcr batchTxtOcr = new();
                        Paper paper = ToolBox.Paper;
                        Task task = Task.Run(async () =>
                        {
                            for (int i = 0; i < item.Count; i++)
                            {
                                if (ocrcancellationToken?.IsCancellationRequested == false)
                                {
                                    string pdffile = Path.ChangeExtension(item.ElementAtOrDefault(i), ".pdf");
                                    if (scanner?.ApplyPdfSaveOcr == true)
                                    {
                                        ObservableCollection<OcrData> scannedText = await item.ElementAtOrDefault(i).OcrAsyc(scanner.SelectedTtsLanguage);
                                        batchTxtOcr.ProgressValue = (i + 1) / (double)item.Count;
                                        batchTxtOcr.FilePath = Path.GetFileName(item.ElementAtOrDefault(i));
                                        item.ElementAtOrDefault(i).GeneratePdf(paper, scannedText).Save(pdffile);
                                    }
                                    else
                                    {
                                        item.ElementAtOrDefault(i).GeneratePdf(paper, null).Save(pdffile);
                                    }
                                    GC.Collect();
                                }
                            }
                        }, ocrcancellationToken.Token);
                        BatchTxtOcrs.Add(batchTxtOcr);
                        Tasks.Add(task);
                    }
                }

                if (scanner?.ApplyPdfSaveOcr == true)
                {
                    BatchDialogOpen = true;
                }
                Filesavetask = Task.WhenAll(Tasks);
                await Filesavetask;
                if (Filesavetask?.IsCompleted == true && Shutdown)
                {
                    ViewModel.Shutdown.DoExitWin(ViewModel.Shutdown.EWX_SHUTDOWN);
                }
            }, parameter => !string.IsNullOrWhiteSpace(BatchFolder));

            CancelBatchOcr = new RelayCommand<object>(parameter => ocrcancellationToken?.Cancel(), parameter => BatchTxtOcrs?.Count > 0);

            StartTxtBatch = new RelayCommand<object>(async parameter =>
            {
                if (Filesavetask?.IsCompleted == false)
                {
                    _ = MessageBox.Show(Translation.GetResStringValue("TASKSRUNNING"));
                    return;
                }
                List<string> files = Win32FileScanner.EnumerateFilepaths(BatchFolder, -1).Where(s => imagefileextensions.Any(ext => ext == Path.GetExtension(s).ToLower())).ToList();
                int slicecount = files.Count > Environment.ProcessorCount ? files.Count / Environment.ProcessorCount : 1;
                Scanner scanner = ToolBox.Scanner;
                BatchTxtOcrs = new List<BatchTxtOcr>();
                List<Task> Tasks = new();
                ocrcancellationToken = new CancellationTokenSource();
                foreach (List<string> item in TwainCtrl.ChunkBy(files, slicecount))
                {
                    if (item.Count > 0)
                    {
                        BatchTxtOcr batchTxtOcr = new();
                        Task task = Task.Run(async () =>
                        {
                            List<string> scannedtext = new();
                            for (int i = 0; i < item.Count; i++)
                            {
                                if (ocrcancellationToken?.IsCancellationRequested == false)
                                {
                                    string image = item[i];
                                    string txtfile = Path.ChangeExtension(image, ".txt");
                                    string content = string.Join(" ", (await image.OcrAsyc(scanner.SelectedTtsLanguage)).Select(z => z.Text));
                                    File.WriteAllText(txtfile, content);
                                    batchTxtOcr.ProgressValue = (i + 1) / (double)item.Count;
                                    batchTxtOcr.FilePath = Path.GetFileName(image);
                                    GC.Collect();
                                }
                            }
                        }, ocrcancellationToken.Token);
                        BatchTxtOcrs.Add(batchTxtOcr);
                        Tasks.Add(task);
                    }
                }
                BatchDialogOpen = true;
                Filesavetask = Task.WhenAll(Tasks);
                await Filesavetask;
                if (Filesavetask?.IsCompleted == true && Shutdown)
                {
                    ViewModel.Shutdown.DoExitWin(ViewModel.Shutdown.EWX_SHUTDOWN);
                }
            }, parameter => !string.IsNullOrWhiteSpace(BatchFolder));

            DatabaseSave = new RelayCommand<object>(parameter => ScannerData.Serialize());

            ResetSettings = new RelayCommand<object>(parameter =>
            {
                if (MessageBox.Show($"{Translation.GetResStringValue("SETTİNGS")} {Translation.GetResStringValue("RESET")}", Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    (parameter as Twainsettings.Settings)?.Reset();
                    Settings.Default.Reset();
                }
            });

            CancelOcr = new RelayCommand<object>(parameter => Ocr.Ocr.ocrcancellationToken?.Cancel());

            DateBack = new RelayCommand<object>(parameter => SeçiliGün = SeçiliGün.Value.AddDays(-1), parameter => SeçiliGün > DateTime.MinValue);

            DateForward = new RelayCommand<object>(parameter => SeçiliGün = SeçiliGün.Value.AddDays(1), parameter => SeçiliGün < DateTime.Today);

            CycleSelectedDocuments = new RelayCommand<object>(async parameter =>
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
                        await Task.Delay(900);
                        currentFile.BorderAnimation = false;
                    }
                }
            }, parameter => MainWindow.cvs?.View?.OfType<Scanner>().Count(z => z.Seçili) > 0);

            ReadOcrDataFile = new RelayCommand<object>(parameter =>
            {
                if (parameter is Scanner scanner)
                {
                    IEnumerable<Data> data = DataYükle()?.Where(z => z.FileName == scanner.FileName);
                    scanner.FileOcrContent = string.Join(" ", data?.Select(z => z.FileContent));
                }
            }, parameter => true);

            PrintImage = new RelayCommand<object>(parameter => PdfViewer.PdfViewer.PrintImageSource(parameter as ImageSource, 300, false), parameter => parameter is ImageSource);
        }

        public static event EventHandler<PropertyChangedEventArgs> StaticPropertyChanged;

        public static bool IsAdministrator {
            get {
                using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static string XmlDataPath {
            get => xmlDataPath;

            set {
                if (xmlDataPath != value)
                {
                    xmlDataPath = value;
                    StaticPropertyChanged?.Invoke(null, new(nameof(XmlDataPath)));
                }
            }
        }

        public ICommand AddFtpSites { get; }

        public bool AnyDataExists {
            get => DataYükle()?.Count > 0;

            set {
                if (anyDataExists != value)
                {
                    anyDataExists = value;
                    OnPropertyChanged(nameof(AnyDataExists));
                }
            }
        }

        public string AramaMetni {
            get => aramaMetni;

            set {
                if (aramaMetni != value)
                {
                    aramaMetni = value;
                    OnPropertyChanged(nameof(AramaMetni));
                }
            }
        }

        public string BarcodeContent {
            get => barcodeContent;

            set {
                if (barcodeContent != value)
                {
                    barcodeContent = value;
                    OnPropertyChanged(nameof(BarcodeContent));
                }
            }
        }

        public ObservableCollection<string> BarcodeList {
            get => barcodeList;

            set {
                if (barcodeList != value)
                {
                    barcodeList = value;
                    OnPropertyChanged(nameof(BarcodeList));
                }
            }
        }

        public bool BatchDialogOpen {
            get => batchDialogOpen; set {

                if (batchDialogOpen != value)
                {
                    batchDialogOpen = value;
                    OnPropertyChanged(nameof(BatchDialogOpen));
                }
            }
        }

        public string BatchFolder {
            get => batchFolder;

            set {
                if (batchFolder != value)
                {
                    batchFolder = value;
                    OnPropertyChanged(nameof(BatchFolder));
                }
            }
        }

        public List<BatchTxtOcr> BatchTxtOcrs {
            get => batchTxtOcrs;

            set {
                if (batchTxtOcrs != value)
                {
                    batchTxtOcrs = value;
                    OnPropertyChanged(nameof(BatchTxtOcrs));
                }
            }
        }

        public XmlLanguage CalendarLang {
            get => calendarLang;

            set {
                if (calendarLang != value)
                {
                    calendarLang = value;
                    OnPropertyChanged(nameof(CalendarLang));
                }
            }
        }

        public ICommand CancelBatchOcr { get; }

        public ICommand CancelOcr { get; }

        public ICommand ChangeDataFolder { get; }

        public ObservableCollection<Chart> ChartData {
            get => chartData;

            set {
                if (chartData != value)
                {
                    chartData = value;
                    OnPropertyChanged(nameof(ChartData));
                }
            }
        }

        public int? CheckedPdfCount {
            get => checkedPdfCount;

            set {
                if (checkedPdfCount != value)
                {
                    checkedPdfCount = value;
                    OnPropertyChanged(nameof(CheckedPdfCount));
                }
            }
        }

        public ICommand CheckUpdate { get; }

        public ICommand CycleSelectedDocuments { get; }

        public ICommand DatabaseSave { get; }

        public ICommand DateBack { get; }

        public ICommand DateForward { get; }

        public bool DetectBarCode {
            get => detectBarCode;

            set {
                if (detectBarCode != value)
                {
                    detectBarCode = value;
                    OnPropertyChanged(nameof(DetectBarCode));
                }
            }
        }

        public bool DetectPageSeperator {
            get => detectPageSeperator;

            set {
                if (detectPageSeperator != value)
                {
                    detectPageSeperator = value;
                    OnPropertyChanged(nameof(DetectPageSeperator));
                }
            }
        }

        public bool DocumentPanelIsExpanded {
            get => documentPanelIsExpanded;

            set {
                if (documentPanelIsExpanded != value)
                {
                    documentPanelIsExpanded = value;
                    OnPropertyChanged(nameof(DocumentPanelIsExpanded));
                }
            }
        }

        public ObservableCollection<Scanner> Dosyalar {
            get => dosyalar;

            set {
                if (dosyalar != value)
                {
                    dosyalar = value;
                    OnPropertyChanged(nameof(Dosyalar));
                }
            }
        }

        public ICommand ExploreFile { get; }

        public double Fold {
            get => fold;

            set {
                if (fold != value)
                {
                    fold = value;
                    OnPropertyChanged(nameof(Fold));
                }
            }
        }

        public string FtpPassword {
            get => ftpPassword; set {

                if (ftpPassword != value)
                {
                    ftpPassword = value;
                    OnPropertyChanged(nameof(FtpPassword));
                }
            }
        }

        public string FtpSite {
            get => ftpSite;

            set {
                if (ftpSite != value)
                {
                    ftpSite = value;
                    OnPropertyChanged(nameof(FtpSite));
                }
            }
        }

        public string FtpUserName {
            get => ftpUserName; set {

                if (ftpUserName != value)
                {
                    ftpUserName = value;
                    OnPropertyChanged(nameof(FtpUserName));
                }
            }
        }

        public ObservableCollection<Size> GetPreviewSize {
            get => new()
            {
                    new Size(175,280),
                    new Size(230,370),
                    new Size(280,450),
                    new Size(350,563),
                    new Size(425,645),
            };

            set {
                if (getPreviewSize != value)
                {
                    getPreviewSize = value;
                    OnPropertyChanged(nameof(GetPreviewSize));
                }
            }
        }

        public bool ListBoxBorderAnimation {
            get => listBoxBorderAnimation;

            set {
                if (listBoxBorderAnimation != value)
                {
                    listBoxBorderAnimation = value;
                    OnPropertyChanged(nameof(ListBoxBorderAnimation));
                }
            }
        }

        public GridLength MainWindowDocumentGuiControlLength {
            get => mainWindowDocumentGuiControlLength; set {

                if (mainWindowDocumentGuiControlLength != value)
                {
                    mainWindowDocumentGuiControlLength = value;
                    OnPropertyChanged(nameof(MainWindowDocumentGuiControlLength));
                }
            }
        }

        public GridLength MainWindowGuiControlLength {
            get => mainWindowGuiControlLength; set {

                if (mainWindowGuiControlLength != value)
                {
                    mainWindowGuiControlLength = value;
                    OnPropertyChanged(nameof(MainWindowGuiControlLength));
                }
            }
        }

        public ICommand ModifyGridWidth { get; }

        public bool OcrIsBusy {
            get => ocrısBusy;

            set {
                if (ocrısBusy != value)
                {
                    ocrısBusy = value;
                    OnPropertyChanged(nameof(OcrIsBusy));
                }
            }
        }

        public ICommand OcrPage { get; }

        public ICommand OcrPdfThumbnailPage { get; }

        public ICommand OpenOriginalFile { get; }

        public string PatchFileName {
            get => patchFileName; set {

                if (patchFileName != value)
                {
                    patchFileName = value;
                    OnPropertyChanged(nameof(PatchFileName));
                }
            }
        }

        public string PatchProfileName {
            get => patchProfileName;

            set {
                if (patchProfileName != value)
                {
                    patchProfileName = value;
                    OnPropertyChanged(nameof(PatchProfileName));
                }
            }
        }

        public string PatchTag {
            get => patchTag;

            set {
                if (patchTag != value)
                {
                    patchTag = value;
                    OnPropertyChanged(nameof(PatchTag));
                }
            }
        }

        public bool PdfBatchRunning {
            get => pdfBatchRunning; set {

                if (pdfBatchRunning != value)
                {
                    pdfBatchRunning = value;
                    OnPropertyChanged(nameof(PdfBatchRunning));
                }
            }
        }

        public ICommand PdfBirleştir { get; }

        public double PdfMergeProgressValue {
            get => pdfMergeProgressValue; set {

                if (pdfMergeProgressValue != value)
                {
                    pdfMergeProgressValue = value;
                    OnPropertyChanged(nameof(PdfMergeProgressValue));
                }
            }
        }

        public bool PdfOnlyText {
            get => pdfOnlyText;

            set {
                if (pdfOnlyText != value)
                {
                    pdfOnlyText = value;
                    OnPropertyChanged(nameof(PdfOnlyText));
                }
            }
        }

        public ICommand PrintImage { get; }

        public Brush ProgressBarForegroundBrush {
            get => progressBarForegroundBrush;

            set {
                if (progressBarForegroundBrush != value)
                {
                    progressBarForegroundBrush = value;
                    OnPropertyChanged(nameof(ProgressBarForegroundBrush));
                }
            }
        }

        public ICommand ReadOcrDataFile { get; }

        public ICommand RegisterSti { get; }

        public ICommand RemovePatchProfile { get; }

        public ICommand RemoveSelectedFtp { get; }

        public ICommand ResetSettings { get; }

        public ICommand SavePatchProfile { get; }

        public ICommand SaveQrImage { get; }

        public ObservableCollection<OcrData> ScannedText {
            get => scannedText;

            set {
                if (scannedText != value)
                {
                    scannedText = value;
                    OnPropertyChanged(nameof(ScannedText));
                }
            }
        }

        public ScannerData ScannerData { get; set; }

        public string SeçiliDil {
            get => seçiliDil;

            set {
                if (seçiliDil != value)
                {
                    seçiliDil = value;
                    OnPropertyChanged(nameof(SeçiliDil));
                }
            }
        }

        public DateTime? SeçiliGün {
            get => seçiliGün; set {

                if (seçiliGün != value)
                {
                    seçiliGün = value;
                    OnPropertyChanged(nameof(SeçiliGün));
                }
            }
        }

        public Scanner SelectedDocument {
            get => selectedDocument;

            set {
                if (selectedDocument != value)
                {
                    selectedDocument = value;
                    OnPropertyChanged(nameof(SelectedDocument));
                }
            }
        }

        public string SelectedFtp {
            get => selectedFtp; set {

                if (selectedFtp != value)
                {
                    selectedFtp = value;
                    OnPropertyChanged(nameof(SelectedFtp));
                }
            }
        }

        public Size SelectedSize {
            get => selectedSize;

            set {
                if (selectedSize != value)
                {
                    selectedSize = value;
                    OnPropertyChanged(nameof(SelectedSize));
                }
            }
        }

        public ICommand SetBatchFolder { get; }

        public ICommand SetBatchWatchFolder { get; }

        public int[] SettingsPagePdfDpiList { get; } = PdfViewer.PdfViewer.DpiList;

        public int[] SettingsPagePictureResizeList { get; } = new int[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

        public bool Shutdown {
            get => shutdown; set {

                if (shutdown != value)
                {
                    shutdown = value;
                    OnPropertyChanged(nameof(Shutdown));
                }
            }
        }

        public bool Sıralama {
            get => sıralama;

            set {
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

        public TesseractViewModel TesseractViewModel {
            get => tesseractViewModel;

            set {
                if (tesseractViewModel != value)
                {
                    tesseractViewModel = value;
                    OnPropertyChanged(nameof(TesseractViewModel));
                }
            }
        }

        public TranslateViewModel TranslateViewModel {
            get => translateViewModel;

            set {
                if (translateViewModel != value)
                {
                    translateViewModel = value;
                    OnPropertyChanged(nameof(TranslateViewModel));
                }
            }
        }

        public ICommand Tümünüİşaretle { get; }

        public ICommand TümününİşaretiniKaldır { get; }

        public ICommand UnRegisterSti { get; }

        public ICommand UploadFtp { get; }

        public static void BackupDataXmlFile()
        {
            if (File.Exists(Settings.Default.DatabaseFile))
            {
                FileInfo fi = new(Settings.Default.DatabaseFile);
                if (fi.Length > 0)
                {
                    File.Copy(fi.FullName, fi.FullName + DateTime.Today.DayOfWeek + ".bak", true);
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
                    return XmlDataPath.DeSerialize<ScannerData>().Data;
                }
                _ = Directory.CreateDirectory(Path.GetDirectoryName(XmlDataPath));
                return new ObservableCollection<Data>();
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        public static async Task FtpUploadAsync(string uri, string userName, string password, Scanner scanner)
        {
            try
            {
                using WebClient webClient = new();
                webClient.Credentials = new NetworkCredential(userName, password.Decrypt());
                webClient.UploadProgressChanged += (sender, args) => scanner.FtpLoadProgressValue = args.ProgressPercentage;
                string address = $"{uri}/{Directory.GetParent(scanner.FileName).Name}{Path.GetFileName(scanner.FileName)}";
                _ = await webClient.UploadFileTaskAsync(address, WebRequestMethods.Ftp.UploadFile, scanner.FileName);
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void AddBarcodeToList(string barcodecontent)
        {
            if (!string.IsNullOrWhiteSpace(barcodecontent))
            {
                BarcodeList.Add(barcodecontent);
            }
        }

        public ObservableCollection<Chart> GetChartsData()
        {
            ObservableCollection<Chart> list = new();
            try
            {
                IOrderedEnumerable<IGrouping<int, Scanner>> chartdata = Dosyalar?.Where(z => DateTime.TryParse(Directory.GetParent(z.FileName).Name, out DateTime _))?.GroupBy(z => DateTime.Parse(Directory.GetParent(z.FileName).Name).Day)?.OrderBy(z => z.Key);
                if (chartdata != null)
                {
                    foreach (IGrouping<int, Scanner> chart in chartdata)
                    {
                        list.Add(new Chart() { Description = chart.Key.ToString(), ChartBrush = RandomColor(), ChartValue = chart.Count() });
                    }
                }
                return list;
            }
            catch (Exception)
            {
                return list;
            }
        }

        public string GetPatchCodeResult(string barcode)
        {
            if (!string.IsNullOrWhiteSpace(barcode))
            {
                IEnumerable<string> patchcodes = Settings.Default.PatchCodes.Cast<string>();
                return patchcodes.Any(z => z.Split('|')[0] == barcode) ? patchcodes?.FirstOrDefault(z => z.Split('|')[0] == barcode)?.Split('|')[1] : "Tarama";
            }
            return string.Empty;
        }

        public ObservableCollection<Scanner> GetScannerFileData()
        {
            if (Directory.Exists(Twainsettings.Settings.Default.AutoFolder))
            {
                ObservableCollection<Scanner> list = new();
                try
                {
                    List<string> files = Directory.EnumerateFiles(Twainsettings.Settings.Default.AutoFolder, "*.*", SearchOption.AllDirectories).Where(s => supportedfilesextension.Any(ext => ext == Path.GetExtension(s).ToLower())).ToList();
                    files.Sort(new StrCmpLogicalComparer());
                    foreach (string dosya in files)
                    {
                        list.Add(new Scanner() { FileName = dosya });
                    }
                    return list;
                }
                catch (UnauthorizedAccessException)
                {
                    return list;
                }
            }
            return null;
        }

        public void RegisterBatchImageFileWatcher(Scanner scanner, Paper paper, string batchsavefolder)
        {
            FileSystemWatcher watcher = new(batchsavefolder)
            {
                NotifyFilter = NotifyFilters.FileName,
                Filter = "*.*",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };
            watcher.Created += async (s, e) =>
            {
                if (imagefileextensions.Contains(Path.GetExtension(e.Name.ToLower())))
                {
                    await Task.Delay(1000);
                    ObservableCollection<OcrData> scannedText = await e.FullPath.OcrAsyc(scanner.SelectedTtsLanguage);
                    await Task.Run(() =>
                    {
                        PdfBatchRunning = true;
                        using PdfDocument pfdocument = e.FullPath.GeneratePdf(paper, scannedText);
                        pfdocument.Save($"{batchsavefolder}\\{Path.ChangeExtension(e.Name, ".pdf")}");
                        GC.Collect();
                        PdfBatchRunning = false;
                    });
                }
            };
        }

        public void ReloadFileDatas()
        {
            Dosyalar = GetScannerFileData();
            ChartData = GetChartsData();
            SeçiliGün = DateTime.Today;
        }

        private static DispatcherTimer timer;

        private static string xmlDataPath = Settings.Default.DatabaseFile;

        private readonly string[] imagefileextensions = new string[] { ".tiff", ".tıf", ".tıff", ".tif", ".jpg", ".jpe", ".gif", ".jpeg", ".jfif", ".jfıf", ".png", ".bmp" };

        private readonly string[] supportedfilesextension = new string[] { ".pdf", ".tıff", ".tıf", ".tiff", ".tif", ".jpg", ".png", ".bmp", ".zip", ".xps", ".mp4", ".3gp", ".wmv", ".mpg", ".mov", ".avi", ".mpeg", ".xml", ".xsl", ".xslt", ".xaml" };

        private bool anyDataExists;

        private string aramaMetni;

        private string barcodeContent;

        private ObservableCollection<string> barcodeList = new();

        private bool batchDialogOpen;

        private string batchFolder;

        private List<BatchTxtOcr> batchTxtOcrs;

        private XmlLanguage calendarLang;

        private ObservableCollection<Chart> chartData;

        private int? checkedPdfCount = 0;

        private int cycleIndex;

        private bool detectBarCode = true;

        private bool detectPageSeperator;

        private bool documentPanelIsExpanded;

        private ObservableCollection<Scanner> dosyalar;

        private double fold = 0.3;

        private string ftpPassword = string.Empty;

        private string ftpSite = string.Empty;

        private string ftpUserName = string.Empty;

        private ObservableCollection<Size> getPreviewSize;

        private bool listBoxBorderAnimation;

        private GridLength mainWindowDocumentGuiControlLength = new(1, GridUnitType.Star);

        private GridLength mainWindowGuiControlLength = new(3, GridUnitType.Star);

        private bool ocrısBusy;

        private string patchFileName;

        private string patchProfileName = string.Empty;

        private string patchTag;

        private bool pdfBatchRunning;

        private double pdfMergeProgressValue;

        private bool pdfOnlyText;

        private Brush progressBarForegroundBrush = Brushes.Green;

        private ObservableCollection<OcrData> scannedText = new();

        private string seçiliDil;

        private DateTime? seçiliGün;

        private Scanner selectedDocument;

        private string selectedFtp;

        private Size selectedSize;

        private bool shutdown;

        private bool sıralama;

        private TesseractViewModel tesseractViewModel;

        private TranslateViewModel translateViewModel;

        private void Default_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "RegisterBatchWatcher" && Settings.Default.RegisterBatchWatcher && !Directory.Exists(Settings.Default.BatchFolder))
            {
                Settings.Default.RegisterBatchWatcher = false;
                Settings.Default.BatchFolder = null;
            }

            if (e.PropertyName is "DefaultTtsLang")
            {
                PdfGeneration.Scanner.SelectedTtsLanguage = Settings.Default.DefaultTtsLang;
            }
            Settings.Default.Save();
        }

        private void GenerateFoldTimer()
        {
            timer = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(15) };
            timer.Tick += OnTick;
            timer.Start();
        }

        private void GpScannerViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "SeçiliGün")
            {
                MainWindow.cvs.Filter += (s, x) =>
                {
                    Scanner scanner = x.Item as Scanner;
                    string seçiligün = SeçiliGün.Value.ToString(Twainsettings.Settings.Default.FolderDateFormat);
                    x.Accepted = Directory.GetParent(scanner?.FileName).Name.StartsWith(seçiligün);
                };
            }
            if (e.PropertyName is "Sıralama")
            {
                MainWindow.cvs?.SortDescriptions.Clear();
                if (Sıralama)
                {
                    MainWindow.cvs?.SortDescriptions.Add(new SortDescription("FileName", ListSortDirection.Descending));
                    return;
                }
                MainWindow.cvs?.SortDescriptions.Add(new SortDescription("FileName", ListSortDirection.Ascending));
            }
            if (e.PropertyName is "AramaMetni")
            {
                if (string.IsNullOrEmpty(AramaMetni))
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
                switch (SeçiliDil)
                {
                    case "TÜRKÇE":
                        TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                        CalendarLang = XmlLanguage.GetLanguage("tr-TR");
                        break;

                    case "ENGLISH":
                        TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("en-EN");
                        CalendarLang = XmlLanguage.GetLanguage("en-EN");
                        break;

                    case "FRANÇAIS":
                        TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                        CalendarLang = XmlLanguage.GetLanguage("fr-FR");
                        break;

                    case "ITALIANO":
                        TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
                        CalendarLang = XmlLanguage.GetLanguage("it-IT");
                        break;

                    case "عربي":
                        TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("ar-AR");
                        CalendarLang = XmlLanguage.GetLanguage("ar-AR");
                        break; 
                    
                    case "РУССКИЙ":
                        TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                        CalendarLang = XmlLanguage.GetLanguage("ru-RU");
                        break; 
                    
                    case "DEUTSCH":
                        TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                        CalendarLang = XmlLanguage.GetLanguage("de-DE");
                        break;  
                    
                    case "日本":
                        TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("ja-JP");
                        CalendarLang = XmlLanguage.GetLanguage("ja-JP");
                        break;
                }
                Settings.Default.DefaultLang = SeçiliDil;
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (StillImageHelper.FirstLanuchScan)
            {
                TimerFold();
                return;
            }
            Fold -= 0.01;
            if (Fold <= 0)
            {
                TimerFold();
            }
            void TimerFold()
            {
                Fold = 0;
                timer.Stop();
                timer.Tick -= OnTick;
            }
        }

        private void RegisterSimplePdfFileWatcher()
        {
            FileSystemWatcher watcher = new(Twainsettings.Settings.Default.AutoFolder)
            {
                NotifyFilter = NotifyFilters.FileName,
                Filter = "*.pdf",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };
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
}