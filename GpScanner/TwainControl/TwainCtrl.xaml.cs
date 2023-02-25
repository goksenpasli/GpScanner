using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using System.Xml.Linq;
using System.Xml.Serialization;
using Extensions;
using Extensions.Controls;
using Microsoft.Win32;
using Ocr;
using PdfSharp.Pdf;
using TwainControl.Properties;
using TwainWpf;
using TwainWpf.TwainNative;
using TwainWpf.Wpf;
using UdfParser;
using static Extensions.ExtensionMethods;
using Path = System.IO.Path;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace TwainControl
{
    public partial class TwainCtrl : UserControl, INotifyPropertyChanged, IDisposable
    {
        public const double Inch = 2.54;

        public static DispatcherTimer CameraQrCodeTimer;

        public static Task Filesavetask;

        public TwainCtrl()
        {
            InitializeComponent();
            DataContext = this;
            Scanner = new Scanner();
            PdfGeneration.Scanner = Scanner;

            Scanner.PropertyChanged += Scanner_PropertyChanged;
            Settings.Default.PropertyChanged += Default_PropertyChanged;
            PropertyChanged += TwainCtrl_PropertyChanged;

            Papers = BitmapMethods.GetPapers();
            ToolBox.Paper = SelectedPaper = Papers.FirstOrDefault(z => z.PaperType == "A4");

            if (Settings.Default.UseSelectedProfile)
            {
                Scanner.SelectedProfile = Settings.Default.DefaultProfile;
            }

            ScanImage = new RelayCommand<object>(parameter =>
            {
                GC.Collect();
                ScanCommonSettings();
                twain.SelectSource(Scanner.SeçiliTarayıcı);
                twain.StartScanning(_settings);
            }, parameter => !Environment.Is64BitProcess && Scanner?.Tarayıcılar?.Count > 0);

            FastScanImage = new RelayCommand<object>(parameter =>
            {
                GC.Collect();
                ScanCommonSettings();
                Scanner.Resimler = new ObservableCollection<ScannedImage>();
                twain.SelectSource(Scanner.SeçiliTarayıcı);
                twain.StartScanning(_settings);
                twain.ScanningComplete += Fastscan;
            }, parameter => !Environment.Is64BitProcess && Scanner?.AutoSave == true && !string.IsNullOrWhiteSpace(Scanner?.FileName) && Scanner?.FileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && Scanner?.Tarayıcılar?.Count > 0);

            ResimSil = new RelayCommand<object>(parameter =>
            {
                ScannedImage item = parameter as ScannedImage;
                UndoImageIndex = Scanner.Resimler?.IndexOf(item);
                _ = Scanner.Resimler?.Remove(item);
                UndoImage = item;
                CanUndoImage = true;
                ToolBox.ResetCropMargin();
                GC.Collect();
            }, parameter => true);

            ResimSilGeriAl = new RelayCommand<object>(parameter =>
            {
                Scanner.Resimler?.Insert((int)UndoImageIndex, UndoImage);
                CanUndoImage = false;
                UndoImage = null;
                UndoImageIndex = null;
                GC.Collect();
            }, parameter => CanUndoImage && UndoImage is not null);

            ExploreFile = new RelayCommand<object>(parameter => OpenFolderAndSelectItem(Path.GetDirectoryName(parameter as string), Path.GetFileName(parameter as string)), parameter => true);

            Kaydet = new RelayCommand<object>(parameter =>
            {
                if (parameter is BitmapFrame bitmapFrame)
                {
                    if (Filesavetask?.IsCompleted == false)
                    {
                        _ = MessageBox.Show(Translation.GetResStringValue("TRANSLATEPENDING"));
                        return;
                    }
                    SaveFileDialog saveFileDialog = new()
                    {
                        Filter = "Tif Resmi (*.tif)|*.tif|Jpg Resmi (*.jpg)|*.jpg|Pdf Dosyası (*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası (*.pdf)|*.pdf|Xps Dosyası (*.xps)|*.xps",
                        FileName = Scanner.SaveFileName,
                        FilterIndex = 3,
                    };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        Filesavetask = Task.Run(async () =>
                        {
                            if (saveFileDialog.FilterIndex == 1)
                            {
                                SaveTifImage(bitmapFrame, saveFileDialog.FileName);
                            }
                            if (saveFileDialog.FilterIndex == 2)
                            {
                                SaveJpgImage(bitmapFrame, saveFileDialog.FileName);
                            }
                            if (saveFileDialog.FilterIndex == 3)
                            {
                                await SavePdfImage(bitmapFrame, saveFileDialog.FileName, Scanner, SelectedPaper);
                            }
                            if (saveFileDialog.FilterIndex == 4)
                            {
                                await SavePdfImage(bitmapFrame, saveFileDialog.FileName, Scanner, SelectedPaper, true);
                            }
                            if (saveFileDialog.FilterIndex == 5)
                            {
                                SaveXpsImage(bitmapFrame, saveFileDialog.FileName);
                            }
                        });
                    }
                }
            }, parameter => !string.IsNullOrWhiteSpace(Scanner?.FileName) && Scanner?.FileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);

            SinglePageSavePdf = new RelayCommand<object>(async parameter =>
            {
                if (parameter is object[] dc && dc[0] is int page && dc[1] is string filepath)
                {
                    SaveFileDialog saveFileDialog = new()
                    {
                        Filter = "Pdf Dosyası (*.pdf)|*.pdf",
                        FileName = Scanner.SaveFileName,
                    };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        await Task.Run(() =>
                        {
                            using PdfDocument outputDocument = filepath.ExtractPdfPages(page, page);
                            outputDocument.DefaultPdfCompression();
                            outputDocument.Save(saveFileDialog.FileName);
                        });
                    }
                }
            }, parameter => true);

            Tümünüİşaretle = new RelayCommand<object>(parameter =>
            {
                foreach (ScannedImage item in Scanner.Resimler.ToList())
                {
                    item.Seçili = true;
                }
            }, parameter => Scanner?.Resimler?.Count > 0);

            TümünüİşaretleDikey = new RelayCommand<object>(parameter =>
            {
                TümününİşaretiniKaldır.Execute(null);
                foreach (ScannedImage item in Scanner.Resimler.ToList().Where(item => item.Resim.PixelWidth <= item.Resim.PixelHeight))
                {
                    item.Seçili = true;
                }
            }, parameter => Scanner?.Resimler?.Count > 0);

            TümünüİşaretleYatay = new RelayCommand<object>(parameter =>
            {
                TümününİşaretiniKaldır.Execute(null);
                foreach (ScannedImage item in Scanner.Resimler.ToList().Where(item => item.Resim.PixelHeight < item.Resim.PixelWidth))
                {
                    item.Seçili = true;
                }
            }, parameter => Scanner?.Resimler?.Count > 0);

            TümününİşaretiniKaldır = new RelayCommand<object>(parameter =>
            {
                foreach (ScannedImage item in Scanner.Resimler.ToList())
                {
                    item.Seçili = false;
                }
            }, parameter => Scanner?.Resimler?.Count > 0);

            Tersiniİşaretle = new RelayCommand<object>(parameter =>
            {
                foreach (ScannedImage item in Scanner.Resimler.ToList())
                {
                    item.Seçili = !item.Seçili;
                }
            }, parameter => Scanner?.Resimler?.Count > 0);

            KayıtYoluBelirle = new RelayCommand<object>(parameter =>
            {
                System.Windows.Forms.FolderBrowserDialog dialog = new()
                {
                    Description = Translation.GetResStringValue("AUTOFOLDER"),
                    SelectedPath = Settings.Default.AutoFolder
                };
                string oldpath = Settings.Default.AutoFolder;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    Settings.Default.AutoFolder = dialog.SelectedPath;
                    Scanner.LocalizedPath = GetDisplayName(dialog.SelectedPath);
                }
                if (!string.IsNullOrWhiteSpace(oldpath) && oldpath != Settings.Default.AutoFolder)
                {
                    _ = MessageBox.Show(Translation.GetResStringValue("AUTOFOLDERCHANGE"), Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }, parameter => true);

            Seçilikaydet = new RelayCommand<object>(parameter =>
            {
                if (Filesavetask?.IsCompleted == false)
                {
                    _ = MessageBox.Show(Translation.GetResStringValue("TRANSLATEPENDING"));
                    return;
                }
                SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Pdf Dosyası (*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası (*.pdf)|*.pdf|Jpg Resmi (*.jpg)|*.jpg|Tif Resmi (*.tif)|*.tif",
                    FileName = Scanner.SaveFileName
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    Filesavetask = Task.Run(async () =>
                    {
                        List<ScannedImage> seçiliresimler = Scanner?.Resimler?.Where(z => z.Seçili).ToList();
                        switch (saveFileDialog.FilterIndex)
                        {
                            case 1:
                                await SavePdfImage(seçiliresimler, saveFileDialog.FileName, Scanner, SelectedPaper, false, (int)Settings.Default.ImgLoadResolution);
                                Dispatcher.Invoke(() => SeçiliListeTemizle.Execute(null));
                                return;

                            case 2:
                                await SavePdfImage(seçiliresimler, saveFileDialog.FileName, Scanner, SelectedPaper, true, (int)Settings.Default.ImgLoadResolution);
                                Dispatcher.Invoke(() => SeçiliListeTemizle.Execute(null));
                                return;

                            case 3:
                                {
                                    await SaveJpgImage(seçiliresimler, saveFileDialog.FileName, Scanner);
                                    Dispatcher.Invoke(() => SeçiliListeTemizle.Execute(null));
                                    return;
                                }

                            case 4:
                                {
                                    await SaveTifImage(seçiliresimler, saveFileDialog.FileName, Scanner);
                                    Dispatcher.Invoke(() => SeçiliListeTemizle.Execute(null));
                                    break;
                                }
                        }
                    });
                }
            }, parameter =>
            {
                Scanner.SeçiliResimSayısı = Scanner?.Resimler.Count(z => z.Seçili) ?? 0;
                return !string.IsNullOrWhiteSpace(Scanner?.FileName) && Scanner?.SeçiliResimSayısı > 0 && Scanner?.FileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
            });

            SeçiliDirektPdfKaydet = new RelayCommand<object>(parameter =>
            {
                Filesavetask = Task.Run(async () =>
                {
                    if (Scanner.ApplyDataBaseOcr)
                    {
                        Scanner.PdfFilePath = PdfGeneration.GetPdfScanPath();
                        List<ScannedImage> images = Scanner.Resimler.Where(z => z.Seçili).ToList();
                        for (int i = 0; i < images.Count; i++)
                        {
                            ScannedImage scannedimage = images[i];
                            byte[] imgdata = scannedimage.Resim.ToTiffJpegByteArray(Format.Jpg);
                            ObservableCollection<OcrData> ocrdata = await imgdata.OcrAsyc(Scanner.SelectedTtsLanguage);
                            Dispatcher.Invoke(() =>
                            {
                                DataBaseQrData = imgdata;
                                DataBaseTextData = ocrdata;
                            });
                            ocrdata = null;
                            imgdata = null;
                            Scanner.PdfSaveProgressValue = i / (double)images.Count;
                        }
                    }
                    if ((ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
                    {
                        await SavePdfImage(Scanner.Resimler.Where(z => z.Seçili).ToList(), PdfGeneration.GetPdfScanPath(), Scanner, SelectedPaper, true, (int)Settings.Default.ImgLoadResolution);
                    }
                    if ((ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale)
                    {
                        await SavePdfImage(Scanner.Resimler.Where(z => z.Seçili).ToList(), PdfGeneration.GetPdfScanPath(), Scanner, SelectedPaper, false, (int)Settings.Default.ImgLoadResolution);
                    }
                }).ContinueWith((_) => Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(Scanner.Resimler));
                    SeçiliListeTemizle.Execute(null);
                }));
            }, parameter =>
            {
                Scanner.SeçiliResimSayısı = Scanner?.Resimler.Count(z => z.Seçili) ?? 0;
                return !string.IsNullOrWhiteSpace(Scanner?.FileName) && Scanner?.AutoSave == true && Scanner?.SeçiliResimSayısı > 0 && Scanner?.FileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
            });

            ListeTemizle = new RelayCommand<object>(parameter =>
            {
                if ((Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) && SeçiliListeTemizle.CanExecute(null))
                {
                    SeçiliListeTemizle.Execute(null);
                    return;
                }

                if (MessageBox.Show(Translation.GetResStringValue("LISTREMOVEWARN"), Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    Scanner.Resimler?.Clear();
                    UndoImage = null;
                    ToolBox.ResetCropMargin();
                    GC.Collect();
                }
            }, parameter => Scanner?.Resimler?.Count > 0);

            SeçiliListeTemizle = new RelayCommand<object>(parameter =>
            {
                foreach (ScannedImage item in Scanner.Resimler.Where(z => z.Seçili).ToList())
                {
                    _ = Scanner.Resimler?.Remove(item);
                }
                UndoImage = null;
                ToolBox.ResetCropMargin();
                GC.Collect();
            }, parameter => Scanner?.Resimler?.Any(z => z.Seçili) == true);

            SaveProfile = new RelayCommand<object>(parameter =>
            {
                StringBuilder sb = new();
                string profile = sb
                    .Append(Scanner.ProfileName)
                    .Append("|")
                    .Append(Settings.Default.Çözünürlük)
                    .Append("|")
                    .Append(Settings.Default.Adf)
                    .Append("|")
                    .Append(Settings.Default.Mode)
                    .Append("|")
                    .Append(Scanner.Duplex)
                    .Append("|")
                    .Append(Scanner.ShowUi)
                    .Append("|")
                    .Append(false)//Scanner.SeperateSave
                    .Append("|")
                    .Append(Settings.Default.ShowFile)
                    .Append("|")
                    .Append(Scanner.DetectEmptyPage)
                    .Append("|")
                    .Append(Scanner.FileName)
                    .ToString();
                _ = Settings.Default.Profile.Add(profile);
                Settings.Default.Save();
                Settings.Default.Reload();
                Scanner.ProfileName = "";
            }, parameter => !string.IsNullOrWhiteSpace(Scanner?.ProfileName) && !Settings.Default.Profile.Cast<string>().Select(z => z.Split('|')[0]).Contains(Scanner?.ProfileName) && Scanner?.FileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && Scanner?.ProfileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);

            RemoveProfile = new RelayCommand<object>(parameter =>
            {
                Settings.Default.Profile.Remove(parameter as string);
                Settings.Default.DefaultProfile = null;
                Settings.Default.UseSelectedProfile = false;
                Settings.Default.Save();
                Settings.Default.Reload();
            }, parameter => true);

            LoadCroppedImage = new RelayCommand<object>(parameter =>
            {
                Scanner.CroppedImage = SeçiliResim.Resim;
                Scanner.CroppedImage.Freeze();
                Scanner.CopyCroppedImage = Scanner.CroppedImage;
                Scanner.CroppedImage.Freeze();
            }, parameter => SeçiliResim is not null);

            InsertFileNamePlaceHolder = new RelayCommand<object>(parameter =>
            {
                string placeholder = parameter as string;
                Scanner.FileName = $"{Scanner.FileName.Substring(0, Scanner.CaretPosition)}{placeholder}{Scanner.FileName.Substring(Scanner.CaretPosition, Scanner.FileName.Length - Scanner.CaretPosition)}";
            }, parameter => true);

            WebAdreseGit = new RelayCommand<object>(parameter => GotoPage(parameter as string), parameter => true);

            OcrPage = new RelayCommand<object>(parameter => ImgData = Scanner.CroppedImage.ToTiffJpegByteArray(Format.Jpg), parameter => Scanner?.CroppedImage is not null);

            LoadImage = new RelayCommand<object>(parameter =>
            {
                if (fileloadtask?.IsCompleted == false)
                {
                    _ = MessageBox.Show(Translation.GetResStringValue("TRANSLATEPENDING"));
                    return;
                }
                OpenFileDialog openFileDialog = new()
                {
                    Filter = "Tüm Dosyalar (*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.pdf;*.xps;*.eyp)|*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.pdf;*.xps;*.eyp|Resim Dosyası (*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle)|*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle|Pdf Dosyası (*.pdf)|*.pdf|Xps Dosyası (*.xps)|*.xps|Eyp Dosyası (*.eyp)|*.eyp",
                    Multiselect = true
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    GC.Collect();
                    AddFiles(openFileDialog.FileNames, DecodeHeight);
                    GC.Collect();
                }
            }, parameter => true);

            LoadSingleEypFile = new RelayCommand<object>(parameter =>
            {
                OpenFileDialog openFileDialog = new()
                {
                    Filter = "Eyp Dosyası (*.eyp)|*.eyp",
                    Multiselect = false
                };
                if (openFileDialog.ShowDialog() == true && parameter is PdfViewer.PdfViewer pdfviewer)
                {
                    using PdfDocument document = EypFileExtract(openFileDialog.FileName).Where(z => Path.GetExtension(z.ToLower()) == ".pdf").ToArray().MergePdf();
                    string source = Path.GetTempPath() + Guid.NewGuid() + ".pdf";
                    document.Save(source);
                    pdfviewer.PdfFilePath = source;
                }
            }, parameter => true);

            LoadSingleUdfFile = new RelayCommand<object>(parameter =>
            {
                OpenFileDialog openFileDialog = new()
                {
                    Filter = "Uyap Dokuman Formatı (*.udf)|*.udf",
                    Multiselect = false
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    using ZipArchive archive = ZipFile.Open(openFileDialog.FileName, ZipArchiveMode.Read);
                    if (archive != null && parameter is XpsViewer xpsViewer)
                    {
                        ZipArchiveEntry üstveri = archive.Entries.FirstOrDefault(entry => entry.Name == "content.xml");
                        string source = Path.GetTempPath() + Guid.NewGuid() + ".xml";
                        string xpssource = Path.GetTempPath() + Guid.NewGuid() + ".xps";
                        üstveri?.ExtractToFile(source, true);
                        Template xmldata = DeSerialize<Template>(source);
                        IDocumentPaginatorSource flowDocument = UdfParser.UdfParser.RenderDocument(xmldata);
                        using (XpsDocument xpsDocument = new(xpssource, FileAccess.ReadWrite))
                        {
                            XpsDocumentWriter xw = XpsDocument.CreateXpsDocumentWriter(xpsDocument);
                            xw.Write(flowDocument.DocumentPaginator);
                        }
                        xpsViewer.XpsDataFilePath = xpssource;
                    }
                }
            }, parameter => true);

            AddSinglePdfPage = new RelayCommand<object>(parameter =>
            {
                if (parameter is BitmapSource imageSource)
                {
                    BitmapSource bitmapSource = imageSource.Width < imageSource.Height ? imageSource.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Width * SelectedPaper.Height) :
                    imageSource.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Height * SelectedPaper.Width);
                    BitmapFrame bitmapFrame = BitmapFrame.Create(imageSource, bitmapSource.BitmapSourceToBitmap().ToBitmapImage(ImageFormat.Jpeg, Settings.Default.PreviewWidth));
                    bitmapFrame.Freeze();
                    ScannedImage scannedImage = new() { Seçili = false, Resim = bitmapFrame };
                    Scanner?.Resimler.Add(scannedImage);
                    bitmapFrame = null;
                    GC.Collect();
                }
            }, parameter => parameter is BitmapSource);

            SendMail = new RelayCommand<object>(parameter =>
            {
                try
                {
                    Mail.Mail.SendMail(MailData);
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message);
                }
            }, parameter => !string.IsNullOrWhiteSpace(MailData));

            AddFromClipBoard = new RelayCommand<object>(parameter =>
            {
                System.Windows.Forms.IDataObject clipboardData = System.Windows.Forms.Clipboard.GetDataObject();
                if (clipboardData?.GetDataPresent(System.Windows.Forms.DataFormats.Bitmap) == true)
                {
                    using Bitmap bitmap = (Bitmap)clipboardData.GetData(System.Windows.Forms.DataFormats.Bitmap);
                    BitmapSource image = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    if (image != null)
                    {
                        BitmapFrame bitmapFrame = GenerateBitmapFrame(image, SelectedPaper);
                        bitmapFrame.Freeze();
                        ScannedImage scannedImage = new() { Seçili = false, Resim = bitmapFrame };
                        Scanner?.Resimler.Add(scannedImage);
                        scannedImage = null;
                    }
                }
            }, parameter => true);

            SaveFileList = new RelayCommand<object>(parameter =>
            {
                SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Txt Dosyası (*.txt)|*.txt",
                    FileName = "Filedata.txt"
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    using StreamWriter file = new(saveFileDialog.FileName);
                    foreach (ScannedImage image in Scanner?.Resimler?.GroupBy(z => z.FilePath).Select(z => z.FirstOrDefault()))
                    {
                        file.WriteLine(image.FilePath);
                    }
                }
            }, parameter => Scanner?.Resimler?.Count(z => !string.IsNullOrWhiteSpace(z.FilePath)) > 0);

            LoadFileList = new RelayCommand<object>(parameter =>
            {
                OpenFileDialog openFileDialog = new()
                {
                    Filter = "Txt Dosyası (*.txt)|*.txt",
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    GC.Collect();
                    AddFiles(File.ReadAllLines(openFileDialog.FileName), DecodeHeight);
                    GC.Collect();
                }
            }, parameter => true);

            EypPdfİçerikBirleştir = new RelayCommand<object>(parameter =>
            {
                string[] files = Scanner.UnsupportedFiles.Where(z => string.Equals(Path.GetExtension(z), ".pdf", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (files.Length > 0)
                {
                    files.SavePdfFiles();
                }
            }, parameter => Scanner?.UnsupportedFiles?.Count(z => string.Equals(Path.GetExtension(z), ".pdf", StringComparison.OrdinalIgnoreCase)) > 1);

            EypPdfSeçiliDosyaSil = new RelayCommand<object>(parameter => Scanner?.UnsupportedFiles?.Remove(parameter as string), parameter => true);

            EypPdfDosyaEkle = new RelayCommand<object>(parameter =>
            {
                OpenFileDialog openFileDialog = new()
                {
                    Filter = "Pdf Dosyası (*.pdf)|*.pdf",
                    Multiselect = true
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    string[] files = openFileDialog.FileNames;
                    if (files.Length > 0)
                    {
                        foreach (string item in files)
                        {
                            Scanner?.UnsupportedFiles?.Add(item);
                        }
                    }
                }
            }, parameter => true);

            int cycleindex = 0;
            CycleSelectedDocuments = new RelayCommand<object>(parameter =>
            {
                if (parameter is ListBox listBox)
                {
                    listBox.ScrollIntoView(Scanner?.Resimler?.Where(z => z.Seçili).ElementAtOrDefault(cycleindex));
                    cycleindex++;
                    if (cycleindex >= Scanner?.Resimler?.Count(z => z.Seçili))
                    {
                        cycleindex = 0;
                    }
                }
            }, parameter => parameter is ListBox && Scanner?.Resimler?.Count(z => z.Seçili) > 0);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand AddFromClipBoard { get; }

        public ICommand AddSinglePdfPage { get; }

        public double AllImageRotationAngle {
            get => allImageRotationAngle;

            set {
                if (allImageRotationAngle != value)
                {
                    allImageRotationAngle = value;
                    OnPropertyChanged(nameof(AllImageRotationAngle));
                }
            }
        }

        public byte[] CameraQRCodeData {
            get => cameraQRCodeData;

            set {
                if (cameraQRCodeData != value)
                {
                    cameraQRCodeData = value;
                    OnPropertyChanged(nameof(CameraQRCodeData));
                }
            }
        }

        public bool CanUndoImage {
            get => canUndoImage; set {

                if (canUndoImage != value)
                {
                    canUndoImage = value;
                    OnPropertyChanged(nameof(CanUndoImage));
                }
            }
        }

        public List<Tuple<string, int, double, bool>> CompressionProfiles => new()
            {
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("BW"), 0, (double)Resolution.Low, false),
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.Low, true),
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("BW"), 0, (double)Resolution.Medium, false),
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.Medium, true),
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("BW"), 0, (double)Resolution.Standard, false),
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.Standard, true),
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("BW"), 0, (double)Resolution.High, false),
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.High, true),
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("BW"), 0, (double)Resolution.Ultra, false),
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.Ultra, true),
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("BW"), 0, (double)Resolution.Low, false),
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.Low, false),
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("BW"), 0, (double)Resolution.Medium, false),
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.Medium, false),
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("BW"), 0, (double)Resolution.Standard, false),
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.Standard, false),
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("BW"), 0, (double)Resolution.High, false),
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.High, false),
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("BW"), 0, (double)Resolution.Ultra, false),
                new Tuple<string, int, double, bool>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.Ultra, false),
            };

        public CroppedBitmap CroppedOcrBitmap {
            get => croppedOcrBitmap;

            set {
                if (croppedOcrBitmap != value)
                {
                    croppedOcrBitmap = value;
                    OnPropertyChanged(nameof(CroppedOcrBitmap));
                }
            }
        }

        public ICommand CycleSelectedDocuments { get; }

        public byte[] DataBaseQrData {
            get => dataBaseQrData;

            set {
                if (dataBaseQrData != value)
                {
                    dataBaseQrData = value;
                    OnPropertyChanged(nameof(DataBaseQrData));
                }
            }
        }

        public ObservableCollection<OcrData> DataBaseTextData {
            get => dataBaseTextData; set {

                if (dataBaseTextData != value)
                {
                    dataBaseTextData = value;
                    OnPropertyChanged(nameof(DataBaseTextData));
                }
            }
        }

        public int DecodeHeight {
            get => decodeHeight;

            set {
                if (decodeHeight != value)
                {
                    decodeHeight = value;
                    OnPropertyChanged(nameof(DecodeHeight));
                }
            }
        }

        public GridLength DocumentGridLength {
            get => documentGridLength;

            set {
                if (documentGridLength != value)
                {
                    documentGridLength = value;
                    OnPropertyChanged(nameof(DocumentGridLength));
                }
            }
        }

        public bool DocumentPreviewIsExpanded {
            get => documentPreviewIsExpanded;

            set {
                if (documentPreviewIsExpanded != value)
                {
                    documentPreviewIsExpanded = value;
                    OnPropertyChanged(nameof(DocumentPreviewIsExpanded));
                }
            }
        }

        public bool DragMoveStarted {
            get => dragMoveStarted; set {

                if (dragMoveStarted != value)
                {
                    dragMoveStarted = value;
                    OnPropertyChanged(nameof(DragMoveStarted));
                }
            }
        }

        public ICommand ExploreFile { get; }

        public ICommand EypPdfDosyaEkle { get; }

        public ICommand EypPdfİçerikBirleştir { get; }

        public ICommand EypPdfSeçiliDosyaSil { get; }

        public ICommand FastScanImage { get; }

        public byte[] ImgData {
            get => ımgData;

            set {
                if (ımgData != value)
                {
                    ımgData = value;
                    OnPropertyChanged(nameof(ImgData));
                }
            }
        }

        public ICommand InsertFileNamePlaceHolder { get; }

        public ICommand Kaydet { get; }

        public ICommand KayıtYoluBelirle { get; }

        public ICommand ListeTemizle { get; }

        public ICommand LoadCroppedImage { get; }

        public ICommand LoadFileList { get; }

        public ICommand LoadImage { get; }

        public ICommand LoadSingleEypFile { get; }

        public ICommand LoadSingleUdfFile { get; }

        public string MailData {
            get => mailData;

            set {
                if (mailData != value)
                {
                    mailData = value;
                    OnPropertyChanged(nameof(MailData));
                }
            }
        }

        public ICommand OcrPage { get; }

        public ObservableCollection<Paper> Papers {
            get => papers;

            set {
                if (papers != value)
                {
                    papers = value;
                    OnPropertyChanged(nameof(Papers));
                }
            }
        }

        public double PdfLoadProgressValue {
            get => pdfLoadProgressValue;

            set {
                if (pdfLoadProgressValue != value)
                {
                    pdfLoadProgressValue = value;
                    OnPropertyChanged(nameof(PdfLoadProgressValue));
                }
            }
        }

        public ICommand RemoveProfile { get; }

        public ICommand ResimSil { get; }

        public ICommand ResimSilGeriAl { get; }

        public ICommand SaveFileList { get; }

        public ICommand SaveProfile { get; }

        public ICommand ScanImage { get; }

        public Scanner Scanner {
            get => scanner;

            set {
                if (scanner != value)
                {
                    scanner = value;
                    OnPropertyChanged(nameof(Scanner));
                }
            }
        }

        public ICommand SeçiliDirektPdfKaydet { get; }

        public ICommand Seçilikaydet { get; }

        public ICommand SeçiliListeTemizle { get; }

        public ScannedImage SeçiliResim {
            get => seçiliResim;

            set {
                if (seçiliResim != value)
                {
                    seçiliResim = value;
                    OnPropertyChanged(nameof(SeçiliResim));
                }
            }
        }

        public Tuple<string, int, double, bool> SelectedCompressionProfile {
            get => selectedCompressionProfile;

            set {
                if (selectedCompressionProfile != value)
                {
                    selectedCompressionProfile = value;
                    OnPropertyChanged(nameof(SelectedCompressionProfile));
                }
            }
        }

        public TwainWpf.TwainNative.Orientation SelectedOrientation {
            get => selectedOrientation; set {

                if (selectedOrientation != value)
                {
                    selectedOrientation = value;
                    OnPropertyChanged(nameof(SelectedOrientation));
                }
            }
        }

        public Paper SelectedPaper {
            get => selectedPaper;

            set {
                if (selectedPaper != value)
                {
                    selectedPaper = value;
                    OnPropertyChanged(nameof(SelectedPaper));
                }
            }
        }

        public ICommand SendMail { get; }

        public ICommand SinglePageSavePdf { get; }

        public ICommand Tersiniİşaretle { get; }

        public ICommand Tümünüİşaretle { get; }

        public ICommand TümünüİşaretleDikey { get; }

        public ICommand TümünüİşaretleYatay { get; }

        public ICommand TümününİşaretiniKaldır { get; }

        public GridLength TwainGuiControlLength {
            get => twainGuiControlLength;

            set {
                if (twainGuiControlLength != value)
                {
                    twainGuiControlLength = value;
                    OnPropertyChanged(nameof(TwainGuiControlLength));
                }
            }
        }

        public ScannedImage UndoImage {
            get => undoImage; set {

                if (undoImage != value)
                {
                    undoImage = value;
                    OnPropertyChanged(nameof(UndoImage));
                }
            }
        }

        public int? UndoImageIndex {
            get => undoImageIndex;

            set {
                if (undoImageIndex != value)
                {
                    undoImageIndex = value;
                    OnPropertyChanged(nameof(UndoImageIndex));
                }
            }
        }

        public ICommand WebAdreseGit { get; }

        public static BitmapFrame GenerateBitmapFrame(BitmapSource bitmapSource, Paper thumbnailpaper)
        {
            bitmapSource.Freeze();
            BitmapSource thumbnail = bitmapSource.PixelWidth < bitmapSource.PixelHeight ? bitmapSource.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / thumbnailpaper.Width * thumbnailpaper.Height) : bitmapSource.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / thumbnailpaper.Height * thumbnailpaper.Width);
            thumbnail.Freeze();
            BitmapFrame bitmapFrame = BitmapFrame.Create(bitmapSource, thumbnail);
            bitmapFrame.Freeze();
            return bitmapFrame;
        }

        public static void GotoPage(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    _ = Process.Start(path);
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message);
                }
            }
        }

        public static void SaveJpgImage(BitmapFrame scannedImage, string filename)
        {
            File.WriteAllBytes(filename, scannedImage.ToTiffJpegByteArray(Format.Jpg));
        }

        public static async Task SaveJpgImage(List<ScannedImage> images, string filename, Scanner scanner)
        {
            await Task.Run(async () =>
            {
                string directory = Path.GetDirectoryName(filename);
                double index = 0;
                int filescount = images.Count;
                Uri uri = new("pack://application:,,,/TwainControl;component/Icons/okay.png", UriKind.Absolute);
                foreach (ScannedImage scannedimage in images)
                {
                    File.WriteAllBytes(directory.SetUniqueFile(Path.GetFileNameWithoutExtension(filename), "jpg"), scannedimage.Resim.ToTiffJpegByteArray(Format.Jpg));
                    index++;
                    scanner.PdfSaveProgressValue = index / filescount;
                    if (uri != null)
                    {
                        scannedimage.Resim = await BitmapMethods.GenerateImageDocumentBitmapFrameAsync(uri, 0);
                    }
                }
                scanner.PdfSaveProgressValue = 0;
                GC.Collect();
            });
        }

        public static async Task SavePdfImage(BitmapFrame scannedImage, string filename, Scanner scanner, Paper paper, bool blackwhite = false)
        {
            ObservableCollection<OcrData> ocrtext = null;
            if (scanner?.ApplyPdfSaveOcr == true)
            {
                ocrtext = await scannedImage.ToTiffJpegByteArray(Format.Jpg).OcrAsyc(scanner.SelectedTtsLanguage);
            }
            if (blackwhite)
            {
                scannedImage.GeneratePdf(ocrtext, Format.Tiff, paper, Settings.Default.JpegQuality, (int)Settings.Default.ImgLoadResolution).Save(filename);
                return;
            }
            scannedImage.GeneratePdf(ocrtext, Format.Jpg, paper, Settings.Default.JpegQuality, (int)Settings.Default.ImgLoadResolution).Save(filename);
        }

        public static async Task SavePdfImage(List<ScannedImage> images, string filename, Scanner scanner, Paper paper, bool blackwhite = false, int dpi = 120)
        {
            List<ObservableCollection<OcrData>> scannedtext = null;
            double index = 0;
            int filescount = images.Count;
            if (scanner?.ApplyPdfSaveOcr == true)
            {
                scannedtext = new List<ObservableCollection<OcrData>>();
                scanner.ProgressState = TaskbarItemProgressState.Normal;
                foreach (ScannedImage image in images)
                {
                    scannedtext.Add(await image.Resim.ToTiffJpegByteArray(Format.Jpg).OcrAsyc(scanner.SelectedTtsLanguage));
                    index++;
                    scanner.PdfSaveProgressValue = index / filescount;
                }
                scanner.PdfSaveProgressValue = 0;
            }
            if (blackwhite)
            {
                (await images.GeneratePdf(Format.Tiff, paper, Settings.Default.JpegQuality, scannedtext, dpi)).Save(filename);
                return;
            }
          (await images.GeneratePdf(Format.Jpg, paper, Settings.Default.JpegQuality, scannedtext, dpi)).Save(filename);
        }

        public static async Task SaveTifImage(List<ScannedImage> images, string filename, Scanner scanner)
        {
            await Task.Run(() =>
            {
                double index = 0;
                int filescount = images.Count;
                TiffBitmapEncoder tifccittencoder = new() { Compression = TiffCompressOption.Ccitt4 };
                foreach (ScannedImage scannedimage in images)
                {
                    tifccittencoder.Frames.Add(scannedimage.Resim);
                    index++;
                    scanner.PdfSaveProgressValue = index / filescount;
                }
                scanner.PdfSaveProgressValue = 0;
                GC.Collect();
                scanner.SaveProgressIndeterminate = true;
                using FileStream stream = new(filename, FileMode.Create);
                tifccittencoder.Save(stream);
                scanner.SaveProgressIndeterminate = false;
            });
        }

        public static void SaveTifImage(BitmapFrame scannedImage, string filename)
        {
            if ((ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
            {
                File.WriteAllBytes(filename, scannedImage.ToTiffJpegByteArray(Format.Tiff));
                return;
            }
            if ((ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale)
            {
                File.WriteAllBytes(filename, scannedImage.ToTiffJpegByteArray(Format.TiffRenkli));
            }
        }

        public static void SaveXpsImage(BitmapFrame scannedImage, string filename)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                System.Windows.Controls.Image image = new();
                image.BeginInit();
                image.Source = scannedImage;
                image.EndInit();
                using XpsDocument xpsd = new(filename, FileAccess.Write);
                XpsDocumentWriter xw = XpsDocument.CreateXpsDocumentWriter(xpsd);
                xw.Write(image);
                image = null;
            });
        }

        public void AddFiles(string[] filenames, int decodeheight)
        {
            fileloadtask = Task.Run(async () =>
            {
                try
                {
                    foreach (string filename in filenames)
                    {
                        switch (Path.GetExtension(filename.ToLower()))
                        {
                            case ".pdf":
                                {
                                    byte[] filedata = await PdfViewer.PdfViewer.ReadAllFileAsync(filename);
                                    if (filedata.IsValidPdfFile())
                                    {
                                        await AddPdfFile(filedata, filename);
                                    }
                                    filedata = null;
                                    break;
                                }
                            case ".eyp":
                                {
                                    List<string> files = EypFileExtract(filename);
                                    await Dispatcher.BeginInvoke(() => files.ForEach(z => Scanner?.UnsupportedFiles?.Add(z)));
                                    AddFiles(files.ToArray(), DecodeHeight);
                                    break;
                                }

                            case ".jpg":
                            case ".jpeg":
                            case ".jfif":
                            case ".jfıf":
                            case ".jpe":
                            case ".png":
                            case ".gif":
                            case ".gıf":
                            case ".bmp":
                                {
                                    BitmapFrame bitmapFrame = await BitmapMethods.GenerateImageDocumentBitmapFrameAsync(new Uri(filename), decodeheight, Settings.Default.DefaultPictureResizeRatio);
                                    bitmapFrame.Freeze();
                                    ScannedImage img = new() { Resim = bitmapFrame, FilePath = filename };
                                    await Dispatcher.BeginInvoke(() => Scanner?.Resimler.Add(img));
                                    img = null;
                                    bitmapFrame = null;
                                    break;
                                }

                            case ".tıf" or ".tiff" or ".tıff" or ".tif":
                                {
                                    TiffBitmapDecoder decoder = new(new Uri(filename), BitmapCreateOptions.None, BitmapCacheOption.None);
                                    for (int i = 0; i < decoder.Frames.Count; i++)
                                    {
                                        byte[] data = decoder.Frames[i].ToTiffJpegByteArray(Format.Jpg);
                                        MemoryStream ms = new(data);
                                        BitmapFrame image = await BitmapMethods.GenerateImageDocumentBitmapFrame(ms, SelectedPaper);
                                        image.Freeze();
                                        BitmapSource thumbimage = image.PixelWidth < image.PixelHeight ? image.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Width * SelectedPaper.Height) : image.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Height * SelectedPaper.Width);
                                        thumbimage.Freeze();
                                        BitmapFrame bitmapFrame = BitmapFrame.Create(image, thumbimage);
                                        bitmapFrame.Freeze();
                                        ScannedImage img = new() { Resim = bitmapFrame, FilePath = filename };
                                        Dispatcher.Invoke(() => Scanner?.Resimler.Add(img));
                                        img = null;
                                        bitmapFrame = null;
                                        image = null;
                                        data = null;
                                        ms = null;
                                    }
                                    break;
                                }
                            case ".xps":
                                {
                                    FixedDocumentSequence docSeq = null;
                                    DocumentPage docPage = null;
                                    await Dispatcher.BeginInvoke(() =>
                                    {
                                        using XpsDocument xpsDoc = new(filename, FileAccess.Read);
                                        docSeq = xpsDoc.GetFixedDocumentSequence();
                                    });
                                    BitmapFrame bitmapframe = null;
                                    for (int i = 0; i < docSeq.DocumentPaginator.PageCount; i++)
                                    {
                                        await Dispatcher.BeginInvoke(() =>
                                        {
                                            docPage = docSeq.DocumentPaginator.GetPage(i);
                                            RenderTargetBitmap rtb = new((int)docPage.Size.Width, (int)docPage.Size.Height, 96, 96, PixelFormats.Default);
                                            rtb.Render(docPage.Visual);
                                            bitmapframe = BitmapFrame.Create(rtb);
                                            bitmapframe.Freeze();
                                        });
                                        BitmapSource thumbimage = bitmapframe.PixelWidth < bitmapframe.PixelHeight ? bitmapframe.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Width * SelectedPaper.Height) : bitmapframe.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Height * SelectedPaper.Width);
                                        thumbimage.Freeze();
                                        BitmapFrame bitmapFrame = BitmapFrame.Create(bitmapframe, thumbimage);
                                        bitmapFrame.Freeze();
                                        ScannedImage img = new() { Resim = bitmapFrame, FilePath = filename };
                                        await Dispatcher.BeginInvoke(() => Scanner?.Resimler.Add(img));
                                        img = null;
                                        bitmapFrame = null;
                                        thumbimage = null;
                                    }
                                    break;
                                }
                        }
                    }
                }
                catch (Exception ex)
                {
                    filenames = null;
                    _ = MessageBox.Show(ex.Message);
                }
            });
        }

        public void Dispose()
        {
            Dispose(true);
        }

        internal static T DeSerialize<T>(string xmldatapath) where T : class, new()
        {
            try
            {
                XmlSerializer serializer = new(typeof(T));
                using StreamReader stream = new(xmldatapath);
                return serializer.Deserialize(stream) as T;
            }
            catch (Exception Ex)
            {
                _ = MessageBox.Show(Ex.Message);
                return null;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Scanner.Resimler = null;
                    twain = null;
                    Scanner.CroppedImage = null;
                    Scanner.CopyCroppedImage = null;
                }

                disposedValue = true;
            }
        }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ScanSettings _settings;

        private double allImageRotationAngle;

        private byte[] cameraQRCodeData;

        private bool canUndoImage;

        private CroppedBitmap croppedOcrBitmap;

        private byte[] dataBaseQrData;

        private ObservableCollection<OcrData> dataBaseTextData;

        private int decodeHeight;

        private bool disposedValue;

        private GridLength documentGridLength = new(5, GridUnitType.Star);

        private bool documentPreviewIsExpanded = true;

        private bool dragMoveStarted;

        private Task fileloadtask;

        private double height;

        private byte[] ımgData;

        private bool isMouseDown;

        private bool isRightMouseDown;

        private string mailData;

        private System.Windows.Point mousedowncoord;

        private ObservableCollection<Paper> papers;

        private double pdfLoadProgressValue;

        private Scanner scanner;

        private ScannedImage seçiliResim;

        private Tuple<string, int, double, bool> selectedCompressionProfile;

        private TwainWpf.TwainNative.Orientation selectedOrientation = TwainWpf.TwainNative.Orientation.Default;

        private Paper selectedPaper = new();

        private Twain twain;

        private GridLength twainGuiControlLength = new(3, GridUnitType.Star);

        private ScannedImage undoImage;

        private int? undoImageIndex;

        private double width;

        private static List<string> EypFileExtract(string eypfilepath)
        {
            using ZipArchive archive = ZipFile.Open(eypfilepath, ZipArchiveMode.Read);
            if (archive != null)
            {
                List<string> data = new();
                ZipArchiveEntry üstveri = archive.Entries.FirstOrDefault(entry => entry.Name == "NihaiOzet.xml");
                string source = Path.GetTempPath() + Guid.NewGuid() + ".xml";
                üstveri?.ExtractToFile(source, true);
                XDocument xdoc = XDocument.Load(source);
                if (xdoc != null)
                {
                    foreach (string file in xdoc.Descendants().Select(z => Path.GetFileName((string)z.Attribute("URI"))).Where(z => !string.IsNullOrEmpty(z)))
                    {
                        ZipArchiveEntry zipArchiveEntry = archive.Entries.FirstOrDefault(entry => entry.Name == file);
                        if (zipArchiveEntry != null)
                        {
                            string destinationFileName = Path.GetTempPath() + Guid.NewGuid() + Path.GetExtension(file.ToLower());
                            zipArchiveEntry.ExtractToFile(destinationFileName, true);
                            data.Add(destinationFileName);
                        }
                    }
                }
                return data;
            }
            return null;
        }

        private async Task AddPdfFile(byte[] filedata, string filepath = null)
        {
            double totalpagecount = await PdfViewer.PdfViewer.PdfPageCountAsync(filedata);
            for (int i = 1; i <= totalpagecount; i++)
            {
                BitmapFrame bitmapFrame = await BitmapMethods.GenerateImageDocumentBitmapFrame(await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, i, (int)Settings.Default.ImgLoadResolution), SelectedPaper, Scanner.Deskew);
                bitmapFrame.Freeze();
                Dispatcher.Invoke(() =>
                {
                    ScannedImage item = new() { Resim = bitmapFrame, FilePath = filepath };
                    Scanner?.Resimler.Add(item);
                    item = null;
                    PdfLoadProgressValue = i / totalpagecount;
                });
                bitmapFrame = null;
            }
            _ = Dispatcher.Invoke(() => PdfLoadProgressValue = 0);
            filedata = null;
            GC.Collect();
        }

        private void ButtonedTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Scanner.CaretPosition = (sender as ButtonedTextBox)?.CaretIndex ?? 0;
        }

        private async void CameraUserControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is CameraUserControl cameraUserControl)
            {
                if (e.PropertyName is "ResimData" && cameraUserControl.ResimData is not null)
                {
                    MemoryStream ms = new(cameraUserControl.ResimData);
                    BitmapFrame bitmapFrame = await BitmapMethods.GenerateImageDocumentBitmapFrame(ms, SelectedPaper);
                    bitmapFrame.Freeze();
                    Scanner.Resimler.Add(new ScannedImage() { Resim = bitmapFrame });
                    ms = null;
                }
                if (e.PropertyName is "DetectQRCode")
                {
                    if (cameraUserControl.DetectQRCode)
                    {
                        CameraQrCodeTimer = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(1) };
                        CameraQrCodeTimer.Tick += (s, f2) =>
                        {
                            using MemoryStream ms = new();
                            cameraUserControl.EncodeBitmapImage(ms);
                            CameraQRCodeData = ms.ToArray();
                            OnPropertyChanged(nameof(CameraQRCodeData));
                        };
                        CameraQrCodeTimer?.Start();
                        return;
                    }
                    CameraQrCodeTimer?.Stop();
                }
            }
        }

        private Int32Rect CropPreviewImage(ImageSource ımageSource)
        {
            int height = ((BitmapSource)ımageSource).PixelHeight - (int)Scanner.CropBottom - (int)Scanner.CropTop;
            int width = ((BitmapSource)ımageSource).PixelWidth - (int)Scanner.CropRight - (int)Scanner.CropLeft;
            return (width < 0 || height < 0) ? default : new((int)Scanner.CropLeft, (int)Scanner.CropTop, width, height);
        }

        private void Default_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "ImgLoadResolution")
            {
                DecodeHeight = (int)(SelectedPaper.Height / Inch * Settings.Default.ImgLoadResolution);
            }
            if (e.PropertyName is "AutoFolder")
            {
                Scanner.AutoSave = Directory.Exists(Settings.Default.AutoFolder);
            }
            if (e.PropertyName is "Adf" && !Settings.Default.Adf)
            {
                Scanner.DetectEmptyPage = false;
                Scanner.Duplex = false;
            }
            Settings.Default.Save();
        }

        private ScanSettings DefaultScanSettings()
        {
            ScanSettings scansettings = new()
            {
                UseAutoScanCache = true,
                UseDocumentFeeder = Settings.Default.Adf,
                ShowTwainUi = Scanner.ShowUi,
                ShowProgressIndicatorUi = Scanner.ShowProgress,
                UseDuplex = Scanner.Duplex,
                ShouldTransferAllPages = true,
                Resolution = new ResolutionSettings() { Dpi = (int)Settings.Default.Çözünürlük, ColourSetting = (ColourSetting)Settings.Default.Mode },
                Page = new PageSettings() { Orientation = SelectedOrientation }
            };
            switch (SelectedPaper.PaperType)
            {
                case "A0":
                    scansettings.Page.Size = PageType.A0;
                    break;

                case "A1":
                    scansettings.Page.Size = PageType.A1;
                    break;

                case "A2":
                    scansettings.Page.Size = PageType.A2;
                    break;

                case "A3":
                    scansettings.Page.Size = PageType.A3;
                    break;

                case "A4":
                    scansettings.Page.Size = PageType.A4;
                    break;

                case "A5":
                    scansettings.Page.Size = PageType.A5;
                    break;

                case "B0":
                    scansettings.Page.Size = PageType.ISOB0;
                    break;

                case "B1":
                    scansettings.Page.Size = PageType.ISOB1;
                    break;

                case "B2":
                    scansettings.Page.Size = PageType.ISOB2;
                    break;

                case "B3":
                    scansettings.Page.Size = PageType.ISOB3;
                    break;

                case "B4":
                    scansettings.Page.Size = PageType.ISOB4;
                    break;

                case "B5":
                    scansettings.Page.Size = PageType.ISOB5;
                    break;

                case "Letter":
                    scansettings.Page.Size = PageType.UsLetter;
                    break;

                case "Legal":
                    scansettings.Page.Size = PageType.UsLegal;
                    break;

                case "Executive":
                    scansettings.Page.Size = PageType.UsExecutive;
                    break;
            }
            return scansettings;
        }

        private BitmapSource EvrakOluştur(Bitmap bitmap)
        {
            int decodepixelheight = (int)(SelectedPaper.Height / Inch * Settings.Default.Çözünürlük);
            return (ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite
                ? bitmap.ConvertBlackAndWhite().ToBitmapImage(ImageFormat.Jpeg, decodepixelheight)
                : (ColourSetting)Settings.Default.Mode == ColourSetting.GreyScale
                    ? bitmap.ConvertBlackAndWhite(160, true).ToBitmapImage(ImageFormat.Jpeg, decodepixelheight)
                    : (ColourSetting)Settings.Default.Mode == ColourSetting.Colour
                                    ? bitmap.ToBitmapImage(ImageFormat.Jpeg, decodepixelheight)
                                    : null;
        }

        private async void Fastscan(object sender, ScanningCompleteEventArgs e)
        {
            OnPropertyChanged(nameof(Scanner.DetectPageSeperator));
            Scanner.PdfFilePath = PdfGeneration.GetPdfScanPath();

            if (Scanner.ApplyDataBaseOcr)
            {
                Tümünüİşaretle.Execute(null);
                foreach (ScannedImage scannedimage in Scanner.Resimler.Where(z => z.Seçili).ToList())
                {
                    DataBaseTextData = await scannedimage.Resim.ToTiffJpegByteArray(Format.Jpg).OcrAsyc(Scanner.SelectedTtsLanguage);
                }
            }
            if ((ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
            {
                (await Scanner.Resimler.ToList().GeneratePdf(Format.Tiff, SelectedPaper, Settings.Default.JpegQuality, null, (int)Settings.Default.Çözünürlük)).Save(Scanner.PdfFilePath);
            }
            if ((ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale)
            {
                (await Scanner.Resimler.ToList().GeneratePdf(Format.Jpg, SelectedPaper, Settings.Default.JpegQuality, null, (int)Settings.Default.Çözünürlük)).Save(Scanner.PdfFilePath);
            }
            if (Settings.Default.ShowFile)
            {
                ExploreFile.Execute(Scanner.PdfFilePath);
            }

            OnPropertyChanged(nameof(Scanner.Resimler));
            SeçiliListeTemizle.Execute(null);
            twain.ScanningComplete -= Fastscan;
        }

        private void GridSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TwainGuiControlLength = new(3, GridUnitType.Star);
            DocumentGridLength = new(5, GridUnitType.Star);
        }

        private void GridSplitter_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            TwainGuiControlLength = new(1, GridUnitType.Star);
            DocumentGridLength = new(0, GridUnitType.Star);
        }

        private void ImgViewer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.Image img && img.Parent is ScrollViewer scrollviewer)
            {
                if (e.LeftButton == MouseButtonState.Pressed && Keyboard.IsKeyDown(Key.LeftCtrl))
                {
                    isMouseDown = true;
                    Cursor = Cursors.Cross;
                }
                if (e.RightButton == MouseButtonState.Pressed)
                {
                    isRightMouseDown = true;
                    Cursor = Cursors.Cross;
                }
                mousedowncoord = e.GetPosition(scrollviewer);
            }
        }

        private async void ImgViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.Image img && img.Parent is ScrollViewer scrollviewer)
            {
                if (isRightMouseDown)
                {
                    System.Windows.Point mousemovecoord = (img.DesiredSize.Width < img.ActualWidth) ? e.GetPosition(img) : e.GetPosition(scrollviewer);
                    mousemovecoord.X += scrollviewer.HorizontalOffset;
                    mousemovecoord.Y += scrollviewer.VerticalOffset;
                    double widthmultiply = SeçiliResim.Resim.PixelWidth / (double)((img.DesiredSize.Width < img.ActualWidth) ? img.ActualWidth : img.DesiredSize.Width);
                    double heightmultiply = SeçiliResim.Resim.PixelHeight / (double)((img.DesiredSize.Height < img.ActualHeight) ? img.ActualHeight : img.DesiredSize.Height);

                    Int32Rect sourceRect = new((int)(mousemovecoord.X * widthmultiply), (int)(mousemovecoord.Y * heightmultiply), 1, 1);
                    CroppedBitmap croppedbitmap = new(SeçiliResim.Resim, sourceRect);
                    byte[] pixels = new byte[4];
                    croppedbitmap.CopyPixels(pixels, 4, 0);
                    croppedbitmap.Freeze();
                    Scanner.SourceColor = System.Windows.Media.Color.FromRgb(pixels[2], pixels[1], pixels[0]).ToString();
                    if (e.RightButton == MouseButtonState.Released)
                    {
                        isRightMouseDown = false;
                        Cursor = Cursors.Arrow;
                    }
                }

                if (isMouseDown)
                {
                    System.Windows.Point mousemovecoord = e.GetPosition(scrollviewer);
                    SolidColorBrush fill = new()
                    {
                        Color = System.Windows.Media.Color.FromArgb(80, 0, 255, 0)
                    };
                    fill.Freeze();
                    SolidColorBrush stroke = new()
                    {
                        Color = System.Windows.Media.Color.FromArgb(80, 255, 0, 0)
                    };
                    stroke.Freeze();
                    Rectangle selectionbox = new()
                    {
                        Stroke = stroke,
                        Fill = fill,
                        StrokeThickness = 2,
                        StrokeDashArray = new DoubleCollection(new double[] { 4, 2 }),
                        Width = Math.Abs(mousemovecoord.X - mousedowncoord.X),
                        Height = Math.Abs(mousemovecoord.Y - mousedowncoord.Y)
                    };
                    cnv.Children.Clear();
                    _ = cnv.Children.Add(selectionbox);
                    if (mousedowncoord.X < mousemovecoord.X)
                    {
                        Canvas.SetLeft(selectionbox, mousedowncoord.X);
                        selectionbox.Width = mousemovecoord.X - mousedowncoord.X;
                    }
                    else
                    {
                        Canvas.SetLeft(selectionbox, mousemovecoord.X);
                        selectionbox.Width = mousedowncoord.X - mousemovecoord.X;
                    }

                    if (mousedowncoord.Y < mousemovecoord.Y)
                    {
                        Canvas.SetTop(selectionbox, mousedowncoord.Y);
                        selectionbox.Height = mousemovecoord.Y - mousedowncoord.Y;
                    }
                    else
                    {
                        Canvas.SetTop(selectionbox, mousemovecoord.Y);
                        selectionbox.Height = mousedowncoord.Y - mousemovecoord.Y;
                    }
                    if (e.LeftButton == MouseButtonState.Released)
                    {
                        cnv.Children.Clear();
                        width = Math.Abs(mousemovecoord.X - mousedowncoord.X);
                        height = Math.Abs(mousemovecoord.Y - mousedowncoord.Y);

                        if (mousedowncoord.X < mousemovecoord.X && mousedowncoord.Y < mousemovecoord.Y)
                        {
                            ImgData = BitmapMethods.CaptureScreen(mousedowncoord.X, mousedowncoord.Y, width, height, scrollviewer, SeçiliResim.Resim);
                        }
                        if (mousedowncoord.X > mousemovecoord.X && mousedowncoord.Y > mousemovecoord.Y)
                        {
                            ImgData = BitmapMethods.CaptureScreen(mousemovecoord.X, mousemovecoord.Y, width, height, scrollviewer, SeçiliResim.Resim);
                        }
                        if (mousedowncoord.X < mousemovecoord.X && mousedowncoord.Y > mousemovecoord.Y)
                        {
                            ImgData = BitmapMethods.CaptureScreen(mousedowncoord.X, mousemovecoord.Y, width, height, scrollviewer, SeçiliResim.Resim);
                        }
                        if (mousedowncoord.X > mousemovecoord.X && mousedowncoord.Y < mousemovecoord.Y)
                        {
                            ImgData = BitmapMethods.CaptureScreen(mousemovecoord.X, mousedowncoord.Y, width, height, scrollviewer, SeçiliResim.Resim);
                        }
                        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        {
                            if (ImgData is not null)
                            {
                                MemoryStream ms = new(ImgData);
                                BitmapFrame bitmapframe = await BitmapMethods.GenerateImageDocumentBitmapFrame(ms, SelectedPaper);
                                bitmapframe.Freeze();
                                ScannedImage item = new() { Resim = bitmapframe };
                                Scanner.Resimler.Add(item);
                            }
                        }

                        mousedowncoord.X = mousedowncoord.Y = 0;
                        isMouseDown = false;
                        Cursor = Cursors.Arrow;
                        ImgData = null;
                    }
                }
            }
        }

        private void ImgViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                double change = (double)(e.Delta > 0 ? .05 : -.05);
                if (ImgViewer.Zoom + change <= 0.01)
                {
                    ImgViewer.Zoom = 0.01;
                }
                else
                {
                    ImgViewer.Zoom += change;
                }
            }
        }

        private void LbEypContent_Drop(object sender, DragEventArgs e)
        {
            string[] droppedfiles = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (droppedfiles?.Length > 0)
            {
                foreach (string file in droppedfiles.Where(file => string.Equals(Path.GetExtension(file), ".pdf", StringComparison.OrdinalIgnoreCase)))
                {
                    Scanner?.UnsupportedFiles?.Add(file);
                }
            }
        }

        private void ListBox_Drop(object sender, DragEventArgs e)
        {
            if (fileloadtask?.IsCompleted == false)
            {
                _ = MessageBox.Show(Application.Current.MainWindow, Translation.GetResStringValue("TRANSLATEPENDING"));
                return;
            }
            string[] droppedfiles = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (droppedfiles?.Length > 0)
            {
                GC.Collect();
                AddFiles(droppedfiles, DecodeHeight);
                GC.Collect();
            }
        }

        private void ListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                Settings.Default.PreviewWidth += e.Delta > 0 ? 10 : -10;
                if (Settings.Default.PreviewWidth <= 85)
                {
                    Settings.Default.PreviewWidth = 85;
                }
                if (Settings.Default.PreviewWidth >= 300)
                {
                    Settings.Default.PreviewWidth = 300;
                }
            }
        }

        private void Run_Drop(object sender, DragEventArgs e)
        {
            if (sender is Run run && e.Data.GetData(typeof(ScannedImage)) is ScannedImage droppedData && run.DataContext is ScannedImage target)
            {
                int removedIdx = Scanner.Resimler.IndexOf(droppedData);
                int targetIdx = Scanner.Resimler.IndexOf(target);

                if (removedIdx < targetIdx)
                {
                    Scanner.Resimler.Insert(targetIdx + 1, droppedData);
                    Scanner.Resimler.RemoveAt(removedIdx);
                    return;
                }
                int remIdx = removedIdx + 1;
                if (Scanner.Resimler.Count + 1 > remIdx)
                {
                    Scanner.Resimler.Insert(targetIdx, droppedData);
                    Scanner.Resimler.RemoveAt(remIdx);
                }
            }
        }

        private void Run_EypDrop(object sender, DragEventArgs e)
        {
            if (sender is Run run && e.Data.GetData(typeof(string)) is string droppedData && run.DataContext is string target)
            {
                int removedIdx = Scanner.UnsupportedFiles.IndexOf(droppedData);
                int targetIdx = Scanner.UnsupportedFiles.IndexOf(target);

                if (removedIdx < targetIdx)
                {
                    Scanner.UnsupportedFiles.Insert(targetIdx + 1, droppedData);
                    Scanner.UnsupportedFiles.RemoveAt(removedIdx);
                    return;
                }
                int remIdx = removedIdx + 1;
                if (Scanner.UnsupportedFiles.Count + 1 > remIdx)
                {
                    Scanner.UnsupportedFiles.Insert(targetIdx, droppedData);
                    Scanner.UnsupportedFiles.RemoveAt(remIdx);
                }
            }
        }

        private void Run_EypPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Run run && e.LeftButton == MouseButtonState.Pressed)
            {
                _ = DragDrop.DoDragDrop(run, run.DataContext, DragDropEffects.Move);
            }
        }

        private void Run_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Run run && e.LeftButton == MouseButtonState.Pressed)
            {
                DragMoveStarted = true;
                _ = DragDrop.DoDragDrop(run, run.DataContext, DragDropEffects.Move);
                DragMoveStarted = false;
            }
        }

        private void ScanCommonSettings()
        {
            _settings = DefaultScanSettings();
            _settings.Rotation = new RotationSettings { AutomaticBorderDetection = true, AutomaticRotate = true, AutomaticDeskew = true };
        }

        private void Scanner_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "CropLeft" or "CropTop" or "CropRight" or "CropBottom" && SeçiliResim != null)
            {
                Int32Rect sourceRect = CropPreviewImage(SeçiliResim.Resim);
                if (sourceRect.HasArea)
                {
                    Scanner.CroppedImage = new CroppedBitmap(SeçiliResim.Resim, sourceRect);
                    Scanner.CroppedImage.Freeze();
                    Scanner.CopyCroppedImage = Scanner.CroppedImage;
                    Scanner.CopyCroppedImage.Freeze();
                    Scanner.CropDialogExpanded = true;
                }
            }
            if (e.PropertyName is "SelectedProfile" && !string.IsNullOrWhiteSpace(Scanner.SelectedProfile))
            {
                string[] selectedprofile = Scanner.SelectedProfile.Split('|');
                Settings.Default.Çözünürlük = double.Parse(selectedprofile[1]);
                Settings.Default.Adf = bool.Parse(selectedprofile[2]);
                Settings.Default.Mode = int.Parse(selectedprofile[3]);
                Scanner.Duplex = bool.Parse(selectedprofile[4]);
                Scanner.ShowUi = bool.Parse(selectedprofile[5]);
                Settings.Default.ShowFile = bool.Parse(selectedprofile[7]);
                Scanner.DetectEmptyPage = bool.Parse(selectedprofile[8]);
                Scanner.FileName = selectedprofile[9];
                Settings.Default.DefaultProfile = Scanner.SelectedProfile;
                Settings.Default.Save();
            }
            if (e.PropertyName is "UsePageSeperator")
            {
                OnPropertyChanged(nameof(Scanner.UsePageSeperator));
            }
        }

        private void Twain_ScanningComplete(object sender, ScanningCompleteEventArgs e)
        {
        }

        private void Twain_TransferImage(object sender, TransferImageEventArgs e)
        {
            if (e.Image != null)
            {
                using Bitmap bitmap = e.Image;
                if (Scanner.DetectEmptyPage && bitmap.IsEmptyPage(Settings.Default.EmptyThreshold))
                {
                    return;
                }
                BitmapSource evrak = EvrakOluştur(bitmap);
                evrak.Freeze();
                BitmapSource önizleme = evrak.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Width * SelectedPaper.Height);
                önizleme.Freeze();
                BitmapFrame bitmapFrame = BitmapFrame.Create(evrak, önizleme);
                bitmapFrame.Freeze();
                Scanner?.Resimler?.Add(new ScannedImage() { Resim = bitmapFrame });
                evrak = null;
                önizleme = null;
                bitmapFrame = null;
            }
        }

        private async void TwainCtrl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "SelectedCompressionProfile" && SelectedCompressionProfile is not null)
            {
                Settings.Default.Mode = SelectedCompressionProfile.Item2;
                Settings.Default.Çözünürlük = SelectedCompressionProfile.Item3;
                Settings.Default.ImgLoadResolution = SelectedCompressionProfile.Item3;
                Scanner.UseMozJpegEncoding = SelectedCompressionProfile.Item4 && MozJpeg.MozJpeg.MozJpegDllExists;
            }
            if (e.PropertyName is "SelectedPaper" && SelectedPaper is not null)
            {
                DecodeHeight = (int)(SelectedPaper.Height / Inch * Settings.Default.ImgLoadResolution);
                ToolBox.Paper = SelectedPaper;
            }
            if (e.PropertyName is "AllImageRotationAngle" && AllImageRotationAngle != 0)
            {
                if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                {
                    foreach (ScannedImage image in Scanner.Resimler.Where(z => z.Seçili).ToList())
                    {
                        image.Resim = await image.Resim.RotateImageAsync(AllImageRotationAngle);
                    }
                    AllImageRotationAngle = 0;
                    return;
                }
                foreach (ScannedImage image in Scanner.Resimler.ToList())
                {
                    image.Resim = await image.Resim.RotateImageAsync(AllImageRotationAngle);
                }
                AllImageRotationAngle = 0;
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                try
                {
                    twain = new Twain(new WindowMessageHook(Window.GetWindow(Parent)));
                    Scanner.Tarayıcılar = twain.SourceNames;
                    if (Scanner.Tarayıcılar?.Count > 0)
                    {
                        Scanner.SeçiliTarayıcı = Scanner.Tarayıcılar[0];
                    }
                    twain.TransferImage += Twain_TransferImage;
                    twain.ScanningComplete += Twain_ScanningComplete;
                }
                catch (Exception)
                {
                }
            }
        }
    }
}