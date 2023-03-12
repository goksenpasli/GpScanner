using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Extensions;
using GpScanner.Properties;
using Microsoft.Win32;
using Ocr;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using TwainControl;
using ZXing;
using ZXing.Common;
using ZXing.QrCode.Internal;
using ZXing.Rendering;
using static Extensions.ExtensionMethods;
using InpcBase = Extensions.InpcBase;
using Twainsettings = TwainControl.Properties;

namespace GpScanner.ViewModel
{
    public class GpScannerViewModel : InpcBase
    {
        public Task Filesavetask;

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
                        _ = MessageBox.Show(ex.Message);
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
                    Result result = GetImageBarcodeResult(twainCtrl.SeçiliResim.Resim);
                    if (result != null)
                    {
                        BarcodeContent = result.Text;
                        BarcodeList.Add(BarcodeContent);
                    }
                    imgdata = null;
                    GC.Collect();
                }
            }, parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang) && parameter is TwainCtrl twainCtrl && twainCtrl.SeçiliResim is not null);

            MergeSelectedImagesToPdfFile = new RelayCommand<object>(async parameter =>
            {
                if (parameter is object[] data && data[0] is TwainCtrl twainCtrl && data[1] is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    string temporarypdf = Path.GetTempPath() + Guid.NewGuid() + ".pdf";
                    IEnumerable<ScannedImage> seçiliresimler = twainCtrl.Scanner.Resimler.Where(z => z.Seçili);
                    if (seçiliresimler.Any())
                    {
                        PdfDocument pdfDocument = await seçiliresimler.ToList().GeneratePdf(Format.Jpg, twainCtrl.SelectedPaper, Twainsettings.Settings.Default.JpegQuality, null, (int)TwainControl.Properties.Settings.Default.Çözünürlük);
                        string pdfFilePath = pdfviewer.PdfFilePath;
                        pdfDocument.Save(temporarypdf);
                        (new string[] { temporarypdf, pdfFilePath }).MergePdf().Save(pdfFilePath);
                        MainWindow.NotifyPdfChange(pdfviewer, temporarypdf, pdfFilePath);
                        twainCtrl.SeçiliListeTemizle.Execute(null);
                        GC.Collect();
                    }
                }
            }, parameter => true);

            OcrPdfThumbnailPage = new RelayCommand<object>(async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer)
                {
                    OcrIsBusy = true;
                    byte[] filedata = await PdfViewer.PdfViewer.ReadAllFileAsync(pdfviewer.PdfFilePath);
                    MemoryStream ms = await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, pdfviewer.Sayfa, (int)Twainsettings.Settings.Default.ImgLoadResolution);
                    if (ms != null)
                    {
                        ObservableCollection<OcrData> ocrdata = await ms.ToArray().OcrAsyc(Settings.Default.DefaultTtsLang);
                        ScannerData.Data.Add(new Data() { Id = DataSerialize.RandomNumber(), FileName = pdfviewer.PdfFilePath, FileContent = string.Join(" ", ocrdata?.Select(z => z.Text)) });
                        DatabaseSave.Execute(null);
                        OcrIsBusy = false;
                        filedata = null;
                        ms = null;
                    }
                    GC.Collect();
                }
            }, parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang) && !OcrIsBusy && parameter is PdfViewer.PdfViewer pdfviewer && pdfviewer.Source is not null);

            OcrPdfImportViewerThumbnailPage = new RelayCommand<object>(async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer)
                {
                    OcrIsBusy = true;
                    byte[] filedata = await PdfViewer.PdfViewer.ReadAllFileAsync(pdfviewer.PdfFilePath);
                    MemoryStream ms = await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, pdfviewer.Sayfa, (int)Twainsettings.Settings.Default.ImgLoadResolution);
                    if (ms != null)
                    {
                        ScannedText = await ms.ToArray().OcrAsyc(Settings.Default.DefaultTtsLang);
                        if (ScannedText != null)
                        {
                            TranslateViewModel.Metin = string.Join(" ", ScannedText.Select(z => z.Text));
                            TranslateViewModel.TaramaGeçmiş.Add(TranslateViewModel.Metin);
                            OcrIsBusy = false;
                        }
                        filedata = null;
                        ms = null;
                    }
                    GC.Collect();
                }
            }, parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang) && !OcrIsBusy && parameter is PdfViewer.PdfViewer pdfviewer && pdfviewer.Source is not null);

            AddAllFileToControlPanel = new RelayCommand<object>(async parameter =>
            {
                if (parameter is object[] data && data[0] is TwainCtrl twainCtrl && data[1] is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                    {
                        twainCtrl.AddFiles(new string[] { pdfviewer.PdfFilePath }, twainCtrl.DecodeHeight);
                        GC.Collect();
                        return;
                    }
                    if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                    {
                        string savefilename = Path.GetTempPath() + Guid.NewGuid() + ".pdf";
                        await SaveFile(pdfviewer.PdfFilePath, savefilename, pdfviewer.Sayfa, pdfviewer.ToplamSayfa);
                        twainCtrl.AddFiles(new string[] { savefilename }, twainCtrl.DecodeHeight);
                        GC.Collect();
                        return;
                    }
                    byte[] filedata = await PdfViewer.PdfViewer.ReadAllFileAsync(pdfviewer.PdfFilePath);
                    MemoryStream ms = await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, pdfviewer.Sayfa, (int)Twainsettings.Settings.Default.ImgLoadResolution);
                    BitmapFrame bitmapFrame = await BitmapMethods.GenerateImageDocumentBitmapFrame(ms, twainCtrl.SelectedPaper, false);
                    bitmapFrame.Freeze();
                    ScannedImage scannedImage = new() { Seçili = false, Resim = bitmapFrame };
                    twainCtrl.Scanner?.Resimler.Add(scannedImage);
                    filedata = null;
                    bitmapFrame = null;
                    scannedImage = null;
                    ms = null;
                    GC.Collect();
                }
            }, parameter => true);

            OpenOriginalFile = new RelayCommand<object>(parameter =>
            {
                if (parameter is string filepath)
                {
                    DocumentViewerWindow documentViewerWindow = new();
                    if (documentViewerWindow.DataContext is DocumentViewerModel documentViewerModel)
                    {
                        documentViewerWindow.Owner = Application.Current.MainWindow;
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

            ExtractPdfFile = new RelayCommand<object>(async parameter =>
            {
                if (parameter is string loadfilename && File.Exists(loadfilename))
                {
                    SaveFileDialog saveFileDialog = new()
                    {
                        Filter = "Pdf Dosyası(*.pdf)|*.pdf",
                        FileName = $"{Path.GetFileNameWithoutExtension(loadfilename)} {Translation.GetResStringValue("PAGENUMBER")} {SayfaBaşlangıç}-{SayfaBitiş}.pdf"
                    };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        string savefilename = saveFileDialog.FileName;
                        int start = SayfaBaşlangıç;
                        int end = SayfaBitiş;
                        await SaveFile(loadfilename, savefilename, start, end);
                    }
                }
            }, parameter => SayfaBaşlangıç <= SayfaBitiş);

            ArrangePdfFile = new RelayCommand<object>(async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    string oldpdfpath = pdfviewer.PdfFilePath;
                    int start = SayfaBaşlangıç - 1;
                    int end = SayfaBitiş - 1;
                    await ArrangeFile(pdfviewer.PdfFilePath, pdfviewer.PdfFilePath, start, end);
                    pdfviewer.PdfFilePath = null;
                    pdfviewer.PdfFilePath = oldpdfpath;
                }
            }, parameter => SayfaBaşlangıç != SayfaBitiş);

            RemoveSelectedPage = new RelayCommand<object>(async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    string path = pdfviewer.PdfFilePath;
                    if (MessageBox.Show($"{Translation.GetResStringValue("PAGENUMBER")} {SayfaBaşlangıç}-{SayfaBitiş} {Translation.GetResStringValue("DELETE")}", Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                    {
                        await RemovePdfPage(path, SayfaBaşlangıç, SayfaBitiş);
                        pdfviewer.PdfFilePath = null;
                        pdfviewer.PdfFilePath = path;
                        pdfviewer.Sayfa = 1;
                        SayfaBaşlangıç = SayfaBitiş = 1;
                    }
                }
            }, parameter => parameter is PdfViewer.PdfViewer pdfviewer && pdfviewer.ToplamSayfa > 1 && SayfaBaşlangıç <= SayfaBitiş && (SayfaBitiş - SayfaBaşlangıç + 1) < pdfviewer.ToplamSayfa);

            RotateSelectedPage = new RelayCommand<object>(parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    string path = pdfviewer.PdfFilePath;
                    int currentpage = pdfviewer.Sayfa;
                    using PdfDocument inputDocument = PdfReader.Open(pdfviewer.PdfFilePath, PdfDocumentOpenMode.Import);
                    if ((Keyboard.IsKeyDown(Key.LeftCtrl) && Keyboard.IsKeyDown(Key.LeftAlt)) || (Keyboard.IsKeyDown(Key.RightCtrl) && Keyboard.IsKeyDown(Key.RightAlt)))
                    {
                        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        {
                            SavePageRotated(path, inputDocument, -90);
                            pdfviewer.PdfFilePath = null;
                            pdfviewer.PdfFilePath = path;
                            return;
                        }
                        SavePageRotated(path, inputDocument, 90);
                        pdfviewer.PdfFilePath = null;
                        pdfviewer.PdfFilePath = path;
                        return;
                    }
                    SavePageRotated(path, inputDocument, (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) ? -90 : 90, pdfviewer.Sayfa - 1);
                    pdfviewer.PdfFilePath = null;
                    pdfviewer.Sayfa = currentpage;
                    pdfviewer.PdfFilePath = path;
                }
            }, parameter => true);

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
            }, parameter => true);

            StartBatch = new RelayCommand<object>(parameter =>
            {
                List<string> files = Win32FileScanner.EnumerateFilepaths(BatchFolder, -1).Where(s => (new string[] { ".tiff", ".tıf", ".tıff", ".tif", ".jpg", ".jpe", ".gif", ".jpeg", ".jfif", ".jfıf", ".png", ".bmp" }).Any(ext => ext == Path.GetExtension(s).ToLower())).ToList();
                BatchTxtOcrs = new List<BatchTxtOcr>();
                if (files.Count > 0)
                {
                    Scanner scanner = ToolBox.Scanner;
                    Paper paper = ToolBox.Paper;
                    List<ObservableCollection<OcrData>> scannedtext = null;
                    BatchTxtOcr batchTxtOcr = new();
                    Filesavetask = Task.Run(async () =>
                    {
                        if (scanner?.ApplyPdfSaveOcr == true)
                        {
                            scannedtext = new List<ObservableCollection<OcrData>>();
                            for (int i = 0; i < files.Count; i++)
                            {
                                string image = files[i];
                                scannedtext.Add(await image.OcrAsyc(scanner.SelectedTtsLanguage));
                                batchTxtOcr.ProgressValue = (i + 1) / (double)files.Count;
                                GC.Collect();
                            }
                        }
                        string filename = $"{Twainsettings.Settings.Default.AutoFolder}\\{Guid.NewGuid()}.pdf";
                        files.GeneratePdf(paper, scannedtext).Save(filename);
                        GC.Collect();
                    });
                    BatchDialogOpen = true;
                    BatchTxtOcrs.Add(batchTxtOcr);
                }
            }, parameter => !string.IsNullOrWhiteSpace(BatchFolder) && !string.IsNullOrWhiteSpace(Twainsettings.Settings.Default.AutoFolder));

            StartTxtBatch = new RelayCommand<object>(parameter =>
            {
                List<string> files = Win32FileScanner.EnumerateFilepaths(BatchFolder, -1).Where(s => (new string[] { ".tiff", ".tıf", ".tıff", ".tif", ".jpg", ".jpe", ".gif", ".jpeg", ".jfif", ".jfıf", ".png", ".bmp" }).Any(ext => ext == Path.GetExtension(s).ToLower())).ToList();
                int slicecount = files.Count > Environment.ProcessorCount ? files.Count / Environment.ProcessorCount : 1;
                Scanner scanner = ToolBox.Scanner;
                BatchTxtOcrs = new List<BatchTxtOcr>();
                List<Task> Tasks = new();
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
                                string image = item[i];
                                string txtfile = Path.ChangeExtension(image, ".txt");
                                string content = string.Join(" ", (await image.OcrAsyc(scanner.SelectedTtsLanguage)).Select(z => z.Text));
                                File.WriteAllText(txtfile, content);
                                batchTxtOcr.ProgressValue = (i + 1) / (double)item.Count;
                                batchTxtOcr.FilePath = Path.GetFileName(image);
                                GC.Collect();
                            }
                        });
                        BatchTxtOcrs.Add(batchTxtOcr);
                        Tasks.Add(task);
                    }
                }
                Filesavetask = Task.WhenAll(Tasks);
                BatchDialogOpen = true;
            }, parameter => !string.IsNullOrWhiteSpace(BatchFolder));

            DatabaseSave = new RelayCommand<object>(parameter => ScannerData.Serialize());

            ResetSettings = new RelayCommand<object>(parameter =>
            {
                if (MessageBox.Show($"{Translation.GetResStringValue("SETTİNGS")} {Translation.GetResStringValue("RESET")}", Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    Twainsettings.Settings twainsettings = parameter as Twainsettings.Settings;
                    twainsettings.Reset();
                    Settings.Default.Reset();
                }
            });

            CancelOcr = new RelayCommand<object>(parameter => Ocr.Ocr.ocrcancellationToken?.Cancel());

            DateBack = new RelayCommand<object>(parameter => SeçiliGün = SeçiliGün.Value.AddDays(-1), parameter => SeçiliGün > DateTime.MinValue);

            DateForward = new RelayCommand<object>(parameter => SeçiliGün = SeçiliGün.Value.AddDays(1), parameter => SeçiliGün < DateTime.Today);

            int cycleindex = 0;
            CycleSelectedDocuments = new RelayCommand<object>(async parameter =>
            {
                if (parameter is ListBox listBox)
                {
                    IEnumerable<Scanner> listboxfiles = MainWindow.cvs.View.OfType<Scanner>();
                    Scanner currentfile = listboxfiles?.Where(z => z.Seçili).ElementAtOrDefault(cycleindex);
                    if (currentfile is null)
                    {
                        listBox.ScrollIntoView(currentfile);
                        currentfile.BorderAnimation = true;
                        cycleindex++;
                        if (cycleindex >= listboxfiles.Count(z => z.Seçili))
                        {
                            cycleindex = 0;
                        }
                        await Task.Delay(900);
                        currentfile.BorderAnimation = false;
                    }
                }
            }, parameter => MainWindow.cvs?.View?.OfType<Scanner>().Count(z => z.Seçili) > 0);

            ReadPdfTag = new RelayCommand<object>(parameter =>
            {
                if (parameter is string filepath && File.Exists(filepath))
                {
                    using PdfDocument reader = PdfReader.Open(filepath, PdfDocumentOpenMode.ReadOnly);
                    StringBuilder stringBuilder = new();
                    _ = stringBuilder.AppendLine(filepath).
                    AppendFormat("PDF {0:#.#}", reader.Version / 10d).AppendLine().
                    AppendLine(reader.Info.Title).
                    Append(reader.PageCount).AppendLine().
                    AppendLine(reader.Info.Producer).
                    AppendLine(reader.Info.Keywords).
                    AppendLine(reader.Info.Creator).
                    AppendLine(reader.Info.Author).
                    Append(reader.Info.CreationDate).AppendLine().
                    Append(reader.Info.ModificationDate).AppendLine().
                    AppendFormat("{0:##.##} MB", reader.FileSize / 1048576d).AppendLine();
                    _ = MessageBox.Show(stringBuilder.ToString(), Application.Current?.MainWindow?.Title);
                }
            }, parameter => true);

            ReadOcrDataFile = new RelayCommand<object>(parameter =>
            {
                if (parameter is Scanner scanner)
                {
                    IEnumerable<Data> data = DataYükle()?.Where(z => z.FileName == scanner.FileName);
                    scanner.FileOcrContent = string.Join(" ", data?.Select(z => z.FileContent));
                    scanner.QrData = data.Select(z => z.QrData);
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

        public ICommand AddAllFileToControlPanel { get; }

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

        public ICommand ArrangePdfFile { get; }

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

        public ResultPoint[] BarcodePosition {
            get => barcodePosition; set {
                if (barcodePosition != value)
                {
                    barcodePosition = value;
                    OnPropertyChanged(nameof(BarcodePosition));
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

        public ICommand ExtractPdfFile { get; }

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

        public ObservableCollection<Size> GetPreviewSize {
            get => new()
            {
                    new Size(240,385),
                    new Size(320,495),
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

        public ICommand MergeSelectedImagesToPdfFile { get; }

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

        public ICommand OcrPdfImportViewerThumbnailPage { get; }

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

        public ICommand ReadPdfTag { get; }

        public ICommand RegisterSti { get; }

        public ICommand RemovePatchProfile { get; }

        public ICommand RemoveSelectedPage { get; }

        public ICommand ResetSettings { get; }

        public ICommand RotateSelectedPage { get; }

        public ICommand SavePatchProfile { get; }

        public ICommand SaveQrImage { get; }

        public int SayfaBaşlangıç {
            get => sayfaBaşlangıç;

            set {
                if (sayfaBaşlangıç != value)
                {
                    sayfaBaşlangıç = value;
                    OnPropertyChanged(nameof(SayfaBaşlangıç));
                }
            }
        }

        public int SayfaBitiş {
            get => sayfaBitiş; set {
                if (sayfaBitiş != value)
                {
                    sayfaBitiş = value;
                    OnPropertyChanged(nameof(SayfaBitiş));
                }
            }
        }

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

        public int[] SettingsPagePdfDpiList { get; } = PdfViewer.PdfViewer.DpiList;

        public int[] SettingsPagePictureResizeList { get; } = new int[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

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

        public ICommand StartBatch { get; }

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

        public static async Task ArrangeFile(string loadfilename, string savefilename, int start, int end)
        {
            await Task.Run(() =>
            {
                using PdfDocument outputDocument = loadfilename.ArrangePdfPages(start, end);
                outputDocument.DefaultPdfCompression();
                outputDocument.Save(savefilename);
            });
        }

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

        public static WriteableBitmap GenerateQr(string text, int width = 120, int height = 120)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                BarcodeWriter barcodeWriter = new()
                {
                    Format = BarcodeFormat.QR_CODE,
                    Renderer = new BitmapRenderer()
                };
                EncodingOptions encodingOptions = new()
                {
                    Width = width,
                    Height = height,
                    Margin = 0
                };
                encodingOptions.Hints.Add(EncodeHintType.ERROR_CORRECTION, ErrorCorrectionLevel.L);
                barcodeWriter.Options = encodingOptions;
                return barcodeWriter.WriteAsWriteableBitmap(text);
            }
            return null;
        }

        public static Result GetImageBarcodeResult(BitmapFrame bitmapFrame)
        {
            if (bitmapFrame is not null)
            {
                BarcodeReader reader = new();
                reader.Options.TryHarder = true;
                return reader.Decode(bitmapFrame);
            }
            return null;
        }

        public static async Task RemovePdfPage(string pdffilepath, int start, int end)
        {
            await Task.Run(() =>
            {
                PdfDocument inputDocument = PdfReader.Open(pdffilepath, PdfDocumentOpenMode.Import);
                for (int i = end; i >= start; i--)
                {
                    inputDocument.Pages.RemoveAt(i - 1);
                }
                inputDocument.Save(pdffilepath);
            });
        }

        public static void WriteAppExceptions(DispatcherUnhandledExceptionEventArgs e)
        {
            if (IsAdministrator)
            {
                using EventLog eventLog = new("GPSCANNER");
                eventLog.Source = "GPSCANNER";
                eventLog.WriteEntry(e.Exception.Message, EventLogEntryType.Error, 101, 1);
                eventLog.WriteEntry(e.Exception.StackTrace, EventLogEntryType.Error, 101, 1);
            }
            else
            {
                string path = $@"{Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)}\error.log";
                File.AppendAllText(path, DateTime.Now.ToString());
                File.AppendAllText(path, e.Exception.Message + Environment.NewLine);
                File.AppendAllText(path, e.Exception.StackTrace + Environment.NewLine);
            }
        }

        public void AddBarcodeToList()
        {
            if (BarcodeContent is not null)
            {
                BarcodeList.Add(BarcodeContent);
            }
        }

        public ObservableCollection<Chart> GetChartsData()
        {
            ObservableCollection<Chart> list = new();
            try
            {
                foreach (IGrouping<int, Scanner> chart in Dosyalar.Where(z => DateTime.TryParse(Directory.GetParent(z.FileName).Name, out DateTime _)).GroupBy(z => DateTime.Parse(Directory.GetParent(z.FileName).Name).Day).OrderBy(z => z.Key))
                {
                    list.Add(new Chart() { Description = chart.Key.ToString(), ChartBrush = RandomColor(), ChartValue = chart.Count() });
                }
                return list;
            }
            catch (Exception)
            {
                return list;
            }
        }

        public Result GetImageBarcodeResult(byte[] imgbyte)
        {
            if (imgbyte != null)
            {
                using MemoryStream ms = new(imgbyte);
                BitmapImage bitmapImage = new();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = ms;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                BarcodeReader reader = new();
                reader.Options.TryHarder = true;
                Result result = reader.Decode(bitmapImage);
                imgbyte = null;
                bitmapImage = null;
                GC.Collect();
                return result;
            }
            return null;
        }

        public Result[] GetMultipleImageBarcodeResult(BitmapFrame bitmapFrame)
        {
            if (bitmapFrame is not null)
            {
                BarcodeReader reader = new();
                reader.Options.TryHarder = true;
                return reader.DecodeMultiple(bitmapFrame);
            }
            return null;
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

        public void ReloadFileDatas()
        {
            Dosyalar = GetScannerFileData();
            ChartData = GetChartsData();
        }

        public class StrCmpLogicalComparer : Comparer<string>
        {
            public override int Compare(string x, string y)
            {
                return StrCmpLogicalW(x, y);
            }
        }

        private static DispatcherTimer timer;

        private static string xmlDataPath = Settings.Default.DatabaseFile;

        private readonly string[] supportedfilesextension = new string[] { ".pdf", ".tiff", ".tif", ".jpg", ".png", ".bmp", ".zip", ".xps", ".mp4", ".3gp", ".wmv", ".mpg", ".mov", ".avi", ".mpeg" };

        private bool anyDataExists;

        private string aramaMetni;

        private string barcodeContent;

        private ObservableCollection<string> barcodeList = new();

        private ResultPoint[] barcodePosition;

        private bool batchDialogOpen;

        private string batchFolder;

        private List<BatchTxtOcr> batchTxtOcrs;

        private XmlLanguage calendarLang;

        private ObservableCollection<Chart> chartData;

        private int? checkedPdfCount = 0;

        private bool detectBarCode = true;

        private bool detectPageSeperator;

        private ObservableCollection<Scanner> dosyalar;

        private double fold = 0.3;

        private ObservableCollection<Size> getPreviewSize;

        private bool listBoxBorderAnimation;

        private GridLength mainWindowDocumentGuiControlLength = new(1, GridUnitType.Star);

        private GridLength mainWindowGuiControlLength = new(3, GridUnitType.Star);

        private bool ocrısBusy;

        private string patchFileName;

        private string patchProfileName = string.Empty;

        private string patchTag;

        private double pdfMergeProgressValue;

        private bool pdfOnlyText;

        private Brush progressBarForegroundBrush = Brushes.Green;

        private int sayfaBaşlangıç = 1;

        private int sayfaBitiş = 1;

        private ObservableCollection<OcrData> scannedText = new();

        private string seçiliDil;

        private DateTime? seçiliGün;

        private Scanner selectedDocument;

        private Size selectedSize;

        private bool sıralama;

        private TesseractViewModel tesseractViewModel;

        private TranslateViewModel translateViewModel;

        private static async Task SaveFile(string loadfilename, string savefilename, int start, int end)
        {
            await Task.Run(() =>
            {
                using PdfDocument outputDocument = loadfilename.ExtractPdfPages(start, end);
                outputDocument.DefaultPdfCompression();
                outputDocument.Save(savefilename);
            });
        }

        [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string x, string y);

        private void Default_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
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
                    string seçiligün = SeçiliGün.Value.ToString(TwainControl.Properties.Settings.Default.FolderDateFormat);
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

        private void SavePageRotated(string savepath, PdfDocument inputDocument, int angle)
        {
            foreach (PdfPage page in inputDocument.Pages)
            {
                page.Rotate += angle;
            }
            inputDocument.Save(savepath);
        }

        private void SavePageRotated(string savepath, PdfDocument inputDocument, int angle, int pageindex)
        {
            inputDocument.Pages[pageindex].Rotate += angle;
            inputDocument.Save(savepath);
        }
    }
}