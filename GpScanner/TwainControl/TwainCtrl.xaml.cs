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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using System.Xml.Linq;
using Extensions;
using Microsoft.Win32;
using Ocr;
using TwainControl.Properties;
using TwainWpf;
using TwainWpf.TwainNative;
using TwainWpf.Wpf;
using static Extensions.ExtensionMethods;
using Path = System.IO.Path;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace TwainControl
{
    public partial class TwainCtrl : UserControl, INotifyPropertyChanged, IDisposable
    {
        public const double Inch = 2.54;

        public static Task Filesavetask;

        public TwainCtrl()
        {
            InitializeComponent();
            DataContext = this;
            Scanner = new Scanner();
            PdfGeneration.Scanner = Scanner;
            ToolBox.Scanner = Scanner;

            Scanner.PropertyChanged += Scanner_PropertyChanged;
            Settings.Default.PropertyChanged += Default_PropertyChanged;
            PropertyChanged += TwainCtrl_PropertyChanged;

            Papers = BitmapMethods.GetPapers();
            SelectedPaper.PaperType = "A4";
            ToolBox.Paper = SelectedPaper;

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
            }, parameter => !Environment.Is64BitProcess && Scanner.AutoSave && !string.IsNullOrWhiteSpace(Scanner?.FileName) && Scanner?.FileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && Scanner?.Tarayıcılar?.Count > 0);

            ResimSil = new RelayCommand<object>(parameter =>
            {
                _ = Scanner.Resimler?.Remove(parameter as ScannedImage);
                ResetCropMargin();
                GC.Collect();
            }, parameter => true);

            ExploreFile = new RelayCommand<object>(parameter => OpenFolderAndSelectItem(Path.GetDirectoryName(parameter as string), Path.GetFileName(parameter as string)), parameter => true);

            Kaydet = new RelayCommand<object>(parameter =>
            {
                if (parameter is BitmapFrame bitmapFrame)
                {
                    if (Filesavetask?.IsCompleted == false)
                    {
                        _ = MessageBox.Show("İşlem Devam Ediyor. Bitmesini Bekleyin.");
                        return;
                    }
                    SaveFileDialog saveFileDialog = new()
                    {
                        Filter = "Tif Resmi (*.tif)|*.tif|Jpg Resmi (*.jpg)|*.jpg|Pdf Dosyası (*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası (*.pdf)|*.pdf|Xps Dosyası (*.xps)|*.xps",
                        FileName = Scanner.SaveFileName
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

            Tümünüİşaretle = new RelayCommand<object>(parameter =>
            {
                foreach (ScannedImage item in Scanner.Resimler.ToList())
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
                    _ = MessageBox.Show("İşlem Devam Ediyor. Bitmesini Bekleyin.");
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
                        List<ScannedImage> seçiliresimler = Scanner.Resimler.Where(z => z.Seçili).ToList();
                        if (saveFileDialog.FilterIndex == 1)
                        {
                            await SavePdfImage(seçiliresimler, saveFileDialog.FileName, Scanner, SelectedPaper, false, (int)ImgLoadResolution);
                            return;
                        }
                        if (saveFileDialog.FilterIndex == 2)
                        {
                            await SavePdfImage(seçiliresimler, saveFileDialog.FileName, Scanner, SelectedPaper, true, (int)ImgLoadResolution);
                            return;
                        }
                        if (saveFileDialog.FilterIndex == 3)
                        {
                            string filename = saveFileDialog.FileName;
                            string directory = Path.GetDirectoryName(filename);
                            foreach (ScannedImage item in seçiliresimler)
                            {
                                File.WriteAllBytes(directory.SetUniqueFile(Path.GetFileNameWithoutExtension(filename), "jpg"), item.Resim.ToTiffJpegByteArray(Format.Jpg));
                            }
                        }
                        if (saveFileDialog.FilterIndex == 4)
                        {
                            string filename = saveFileDialog.FileName;
                            string directory = Path.GetDirectoryName(filename);
                            TiffBitmapEncoder tifccittencoder = new() { Compression = TiffCompressOption.Ccitt4 };
                            foreach (ScannedImage item in seçiliresimler)
                            {
                                tifccittencoder.Frames.Add(item.Resim);
                            }
                            using FileStream stream = new(directory.SetUniqueFile(Path.GetFileNameWithoutExtension(filename), "tif"), FileMode.Create);
                            tifccittencoder.Save(stream);
                        }
                    });
                }
            }, parameter =>
            {
                Scanner.SeçiliResimSayısı = Scanner.Resimler.Count(z => z.Seçili);
                return !string.IsNullOrWhiteSpace(Scanner?.FileName) && Scanner.SeçiliResimSayısı > 0 && Scanner?.FileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
            });

            SeçiliDirektPdfKaydet = new RelayCommand<object>(parameter =>
            {
                Filesavetask = Task.Run(async () =>
                {
                    if ((ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
                    {
                        await SavePdfImage(Scanner.Resimler.Where(z => z.Seçili).ToList(), PdfGeneration.GetPdfScanPath(), Scanner, SelectedPaper, true, (int)ImgLoadResolution);
                    }
                    if ((ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale)
                    {
                        await SavePdfImage(Scanner.Resimler.Where(z => z.Seçili).ToList(), PdfGeneration.GetPdfScanPath(), Scanner, SelectedPaper, false, (int)ImgLoadResolution);
                    }
                }).ContinueWith((z) => Dispatcher.Invoke(() => OnPropertyChanged(nameof(Scanner.Resimler))));
            }, parameter =>
            {
                Scanner.SeçiliResimSayısı = Scanner.Resimler.Count(z => z.Seçili);
                return !string.IsNullOrWhiteSpace(Scanner?.FileName) && Scanner.SeçiliResimSayısı > 0 && Scanner?.FileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
            });

            ListeTemizle = new RelayCommand<object>(parameter =>
            {
                if (MessageBox.Show(Translation.GetResStringValue("LISTREMOVEWARN"), Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    Scanner.Resimler?.Clear();
                    ResetCropMargin();
                    GC.Collect();
                }
            }, parameter => Scanner?.Resimler?.Count > 0);

            SeçiliListeTemizle = new RelayCommand<object>(parameter =>
            {
                foreach (ScannedImage item in Scanner.Resimler.Where(z => z.Seçili).ToList())
                {
                    _ = Scanner.Resimler?.Remove(item);
                }
                ResetCropMargin();
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
                    .Append(true)
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

            LoadHistogram = new RelayCommand<object>(parameter =>
            {
                RedChart = ((BitmapSource)Scanner.CroppedImage).BitmapSourceToBitmap().GenerateHistogram(System.Windows.Media.Brushes.Red);
                GreenChart = ((BitmapSource)Scanner.CroppedImage).BitmapSourceToBitmap().GenerateHistogram(System.Windows.Media.Brushes.Green);
                BlueChart = ((BitmapSource)Scanner.CroppedImage).BitmapSourceToBitmap().GenerateHistogram(System.Windows.Media.Brushes.Blue);
            }, parameter => Scanner.CroppedImage is not null);

            InsertFileNamePlaceHolder = new RelayCommand<object>(parameter =>
            {
                string placeholder = parameter as string;
                Scanner.FileName = $"{Scanner.FileName.Substring(0, Scanner.CaretPosition)}{placeholder}{Scanner.FileName.Substring(Scanner.CaretPosition, Scanner.FileName.Length - Scanner.CaretPosition)}";
            }, parameter => true);

            SplitImage = new RelayCommand<object>(parameter =>
            {
                BitmapSource image = (BitmapSource)Scanner.CroppedImage;
                _ = Directory.CreateDirectory($@"{PdfGeneration.GetSaveFolder()}\{Translation.GetResStringValue("SPLIT")}");
                for (int i = 0; i < Scanner.EnAdet; i++)
                {
                    for (int j = 0; j < Scanner.BoyAdet; j++)
                    {
                        int x = i * image.PixelWidth / Scanner.EnAdet;
                        int y = j * image.PixelHeight / Scanner.BoyAdet;
                        int width = image.PixelWidth / Scanner.EnAdet;
                        int height = image.PixelHeight / Scanner.BoyAdet;
                        Int32Rect sourceRect = new(x, y, width, height);
                        if (sourceRect.HasArea)
                        {
                            CroppedBitmap croppedBitmap = new(image, sourceRect);
                            File.WriteAllBytes($@"{PdfGeneration.GetSaveFolder()}\{Translation.GetResStringValue("SPLIT")}".SetUniqueFile(Translation.GetResStringValue("SPLIT"), "jpg"), croppedBitmap.ToTiffJpegByteArray(Format.Jpg));
                        }
                    }
                }
                WebAdreseGit.Execute($@"{PdfGeneration.GetSaveFolder()}\{Translation.GetResStringValue("SPLIT")}");
            }, parameter => Scanner.AutoSave && Scanner.CroppedImage is not null && (Scanner.EnAdet > 1 || Scanner.BoyAdet > 1));

            ResetCroppedImage = new RelayCommand<object>(parameter => ResetCropMargin(), parameter => Scanner.CroppedImage is not null);

            WebAdreseGit = new RelayCommand<object>(parameter =>
            {
                string path = parameter as string;
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }

                try
                {
                    _ = Process.Start(path);
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message);
                }
            }, parameter => true);

            SetWatermark = new RelayCommand<object>(parameter => Scanner.CroppedImage = Scanner.CroppedImage.ÜstüneResimÇiz(new System.Windows.Point(Scanner.CroppedImage.Width / 2, Scanner.CroppedImage.Height / 2), System.Windows.Media.Brushes.Red, Scanner.WatermarkTextSize, Scanner.Watermark, Scanner.WatermarkAngle, Scanner.WatermarkFont), parameter => Scanner.CroppedImage is not null && !string.IsNullOrWhiteSpace(Scanner?.Watermark));

            ApplyColorChange = new RelayCommand<object>(parameter => Scanner.CopyCroppedImage = Scanner.CroppedImage, parameter => Scanner.CroppedImage is not null);

            DeskewImage = new RelayCommand<object>(parameter =>
            {
                double skewAngle = GetDeskewAngle(Scanner.CroppedImage, true);
                Scanner.CroppedImage = Scanner.CroppedImage.RotateImage(skewAngle);
            }, parameter => Scanner.CroppedImage is not null);

            OcrPage = new RelayCommand<object>(parameter =>
            {
                ImgData = Scanner.CroppedImage.ToTiffJpegByteArray(Format.Jpg);
                OnPropertyChanged(nameof(ImgData));
            }, parameter => Scanner.CroppedImage is not null);

            PdfBirleştir = new RelayCommand<object>(parameter =>
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
                        PdfGeneration.SavePdfFiles(files);
                    }
                }
            }, parameter => true);

            LoadImage = new RelayCommand<object>(parameter =>
            {
                if (fileloadtask?.IsCompleted == false)
                {
                    _ = MessageBox.Show("İşlem Devam Ediyor. Bitmesini Bekleyin.");
                    return;
                }
                OpenFileDialog openFileDialog = new()
                {
                    Filter = "Tüm Dosyalar (*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.pdf;*.xps;*.eyp)|*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.pdf;*.xps;*.eyp|Resim Dosyası (*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle)|*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle|Pdf Dosyası (*.pdf)|*.pdf|Xps Dosyası (*.xps)|*.xps|Eyp Dosyası (*.eyp)|*.eyp",
                    Multiselect = true
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    AddFiles(openFileDialog.FileNames, DecodeHeight);
                }
            }, parameter => true);

            PrintCroppedImage = new RelayCommand<object>(parameter => PdfViewer.PdfViewer.PrintImageSource(parameter as ImageSource), parameter => Scanner.CroppedImage is not null);

            TransferImage = new RelayCommand<object>(parameter =>
            {
                BitmapFrame bitmapFrame = GenerateBitmapFrame((BitmapSource)Scanner.CroppedImage);
                bitmapFrame.Freeze();
                ScannedImage scannedImage = new() { Seçili = false, Resim = bitmapFrame };
                Scanner?.Resimler.Add(scannedImage);
            }, parameter => Scanner.CroppedImage is not null);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand ApplyColorChange { get; }

        public ObservableCollection<Chart> BlueChart
        {
            get => blueChart;

            set
            {
                if (blueChart != value)
                {
                    blueChart = value;
                    OnPropertyChanged(nameof(BlueChart));
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

        public CroppedBitmap CroppedOcrBitmap
        {
            get => croppedOcrBitmap;

            set
            {
                if (croppedOcrBitmap != value)
                {
                    croppedOcrBitmap = value;
                    OnPropertyChanged(nameof(CroppedOcrBitmap));
                }
            }
        }

        public int DecodeHeight
        {
            get => decodeHeight;

            set
            {
                if (decodeHeight != value)
                {
                    decodeHeight = value;
                    OnPropertyChanged(nameof(DecodeHeight));
                }
            }
        }

        public ICommand DeskewImage { get; }

        public GridLength DocumentGridLength
        {
            get => documentGridLength;

            set
            {
                if (documentGridLength != value)
                {
                    documentGridLength = value;
                    OnPropertyChanged(nameof(DocumentGridLength));
                }
            }
        }

        public bool DocumentPreviewIsExpanded
        {
            get => documentPreviewIsExpanded;

            set
            {
                if (documentPreviewIsExpanded != value)
                {
                    documentPreviewIsExpanded = value;
                    OnPropertyChanged(nameof(DocumentPreviewIsExpanded));
                }
            }
        }

        public bool DragMoveStarted
        {
            get => dragMoveStarted; set

            {
                if (dragMoveStarted != value)
                {
                    dragMoveStarted = value;
                    OnPropertyChanged(nameof(DragMoveStarted));
                }
            }
        }

        public ICommand ExploreFile { get; }

        public ICommand FastScanImage { get; }

        public ObservableCollection<Chart> GreenChart
        {
            get => greenChart;

            set
            {
                if (greenChart != value)
                {
                    greenChart = value;
                    OnPropertyChanged(nameof(GreenChart));
                }
            }
        }

        public byte[] ImgData
        {
            get => ımgData;

            set
            {
                if (ımgData != value)
                {
                    ımgData = value;
                    OnPropertyChanged(nameof(ImgData));
                }
            }
        }

        public double ImgLoadResolution
        {
            get => ımgLoadResolution;

            set
            {
                if (ımgLoadResolution != value)
                {
                    ımgLoadResolution = value;
                    DecodeHeight = (int)(SelectedPaper.Height / Inch * ImgLoadResolution);
                    OnPropertyChanged(nameof(ImgLoadResolution));
                    OnPropertyChanged(nameof(DecodeHeight));
                }
            }
        }

        public ICommand InsertFileNamePlaceHolder { get; }

        public ICommand Kaydet { get; }

        public ICommand KayıtYoluBelirle { get; }

        public ICommand ListeTemizle { get; }

        public ICommand LoadCroppedImage { get; }

        public ICommand LoadHistogram { get; }

        public ICommand LoadImage { get; }

        public ICommand OcrPage { get; }

        public ObservableCollection<Paper> Papers
        {
            get => papers;

            set
            {
                if (papers != value)
                {
                    papers = value;
                    OnPropertyChanged(nameof(Papers));
                }
            }
        }

        public ICommand PdfBirleştir { get; }

        public double PdfLoadProgressValue
        {
            get => pdfLoadProgressValue;

            set
            {
                if (pdfLoadProgressValue != value)
                {
                    pdfLoadProgressValue = value;
                    OnPropertyChanged(nameof(PdfLoadProgressValue));
                }
            }
        }

        public ICommand PrintCroppedImage { get; }

        public ObservableCollection<Chart> RedChart
        {
            get => redChart;

            set
            {
                if (redChart != value)
                {
                    redChart = value;
                    OnPropertyChanged(nameof(RedChart));
                }
            }
        }

        public ICommand RemoveProfile { get; }

        public ICommand ResetCroppedImage { get; }

        public ICommand ResimSil { get; }

        public ICommand SaveProfile { get; }

        public ICommand ScanImage { get; }

        public Scanner Scanner
        {
            get => scanner;

            set
            {
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

        public ScannedImage SeçiliResim
        {
            get => seçiliResim;

            set
            {
                if (seçiliResim != value)
                {
                    seçiliResim = value;
                    OnPropertyChanged(nameof(SeçiliResim));
                }
            }
        }

        public Tuple<string, int, double, bool> SelectedCompressionProfile
        {
            get => selectedCompressionProfile;

            set
            {
                if (selectedCompressionProfile != value)
                {
                    selectedCompressionProfile = value;
                    OnPropertyChanged(nameof(SelectedCompressionProfile));
                }
            }
        }

        public Paper SelectedPaper
        {
            get => selectedPaper;

            set
            {
                if (selectedPaper != value)
                {
                    selectedPaper = value;
                    OnPropertyChanged(nameof(SelectedPaper));
                }
            }
        }

        public ICommand SetWatermark { get; }

        public ICommand SplitImage { get; }

        public ICommand Tersiniİşaretle { get; }

        public ICommand TransferImage { get; }

        public ICommand Tümünüİşaretle { get; }

        public ICommand TümününİşaretiniKaldır { get; }

        public GridLength TwainGuiControlLength
        {
            get => twainGuiControlLength;

            set
            {
                if (twainGuiControlLength != value)
                {
                    twainGuiControlLength = value;
                    OnPropertyChanged(nameof(TwainGuiControlLength));
                }
            }
        }

        public ICommand WebAdreseGit { get; }

        public static double GetDeskewAngle(ImageSource ımageSource, bool fast = false)
        {
            Deskew sk = new((BitmapSource)ımageSource);
            return -1 * sk.GetSkewAngle(fast);
        }

        public static void SaveJpgImage(BitmapFrame scannedImage, string filename)
        {
            File.WriteAllBytes(filename, scannedImage.ToTiffJpegByteArray(Format.Jpg));
        }

        public static async Task SavePdfImage(BitmapFrame scannedImage, string filename, Scanner scanner, Paper paper, bool blackwhite = false, int dpi = 120)
        {
            ObservableCollection<OcrData> ocrtext = null;
            if (scanner?.ApplyPdfSaveOcr == true)
            {
                Ocr.Ocr.ocrcancellationToken = new CancellationTokenSource();
                ocrtext = await scannedImage.ToTiffJpegByteArray(Format.Jpg).OcrAsyc(scanner.SelectedTtsLanguage);
            }
            if (blackwhite)
            {
                PdfGeneration.GeneratePdf(scannedImage, ocrtext, Format.Tiff, paper, scanner.JpegQuality).Save(filename);
                return;
            }
            PdfGeneration.GeneratePdf(scannedImage, ocrtext, Format.Jpg, paper, scanner.JpegQuality).Save(filename);
        }

        public static async Task SavePdfImage(List<ScannedImage> images, string filename, Scanner scanner, Paper paper, bool blackwhite = false, int dpi = 120)
        {
            List<ObservableCollection<OcrData>> scannedtext = null;
            double index = 0;
            int filescount = images.Count;
            if (scanner?.ApplyPdfSaveOcr == true)
            {
                Ocr.Ocr.ocrcancellationToken = new CancellationTokenSource();
                scannedtext = new List<ObservableCollection<OcrData>>();
                foreach (ScannedImage image in images)
                {
                    scannedtext.Add(await image.Resim.ToTiffJpegByteArray(Format.Jpg).OcrAsyc(scanner.SelectedTtsLanguage));
                    index++;
                    scanner.PdfSaveProgressValue = index / filescount;
                }
            }
            if (blackwhite)
            {
                PdfGeneration.GeneratePdf(images, Format.Tiff, paper, scanner.JpegQuality, scannedtext, dpi).Save(filename);
                return;
            }
            PdfGeneration.GeneratePdf(images, Format.Jpg, paper, scanner.JpegQuality, scannedtext, dpi).Save(filename);
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
            foreach (string item in filenames)
            {
                SynchronizationContext uiContext = SynchronizationContext.Current;
                switch (Path.GetExtension(item.ToLower()))
                {
                    case ".pdf":
                        {
                            byte[] filedata = null;
                            fileloadtask = Task.Run(async () =>
                            {
                                try
                                {
                                    filedata = await PdfViewer.PdfViewer.ReadAllFileAsync(item);
                                    if (PdfGeneration.IsValidPdfFile(filedata.Take(4)))
                                    {
                                        await AddPdfFile(decodeheight, uiContext, filedata);
                                    }
                                    filedata = null;
                                }
                                catch (Exception)
                                {
                                    return;
                                }
                            });
                            break;
                        }
                    case ".eyp":
                        {
                            byte[] filedata = null;
                            fileloadtask = Task.Run(async () =>
                            {
                                try
                                {
                                    string eyppdfpath = EypMainPdfExtract(item);
                                    filedata = await PdfViewer.PdfViewer.ReadAllFileAsync(eyppdfpath);
                                    if (PdfGeneration.IsValidPdfFile(filedata.Take(4)))
                                    {
                                        await AddPdfFile(decodeheight, uiContext, filedata);
                                    }
                                    filedata = null;
                                }
                                catch (Exception)
                                {
                                    return;
                                }
                            });
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
                            fileloadtask = Task.Run(() =>
                                           {
                                               try
                                               {
                                                   BitmapFrame bitmapFrame = BitmapMethods.GenerateImageDocumentBitmapFrame(decodeheight, new Uri(item));
                                                   bitmapFrame.Freeze();
                                                   uiContext.Send(_ => Scanner?.Resimler.Add(new ScannedImage() { Resim = bitmapFrame }), null);
                                                   bitmapFrame = null;
                                               }
                                               catch (Exception)
                                               {
                                                   return;
                                               }
                                           });
                            break;
                        }

                    case ".tıf" or ".tiff" or ".tıff" or ".tif":
                        {
                            fileloadtask = Task.Run(() =>
                            {
                                try
                                {
                                    TiffBitmapDecoder decoder = new(new Uri(item), BitmapCreateOptions.None, BitmapCacheOption.None);
                                    for (int i = 0; i < decoder.Frames.Count; i++)
                                    {
                                        byte[] data = decoder.Frames[i].ToTiffJpegByteArray(Format.Jpg);
                                        MemoryStream ms = new(data);
                                        BitmapFrame image = BitmapMethods.GenerateImageDocumentBitmapFrame(decodeheight, ms, SelectedPaper);
                                        image.Freeze();
                                        BitmapSource thumbimage = image.PixelWidth < image.PixelHeight ? image.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Width * SelectedPaper.Height) : image.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Height * SelectedPaper.Width);
                                        thumbimage.Freeze();
                                        BitmapFrame bitmapFrame = BitmapFrame.Create(image, thumbimage);
                                        bitmapFrame.Freeze();
                                        uiContext.Send(_ => Scanner?.Resimler.Add(new ScannedImage() { Resim = bitmapFrame }), null);
                                        bitmapFrame = null;
                                        image = null;
                                        data = null;
                                        ms = null;
                                    }
                                }
                                catch (Exception)
                                {
                                    return;
                                }
                            });
                            break;
                        }
                    case ".xps":
                        {
                            using XpsDocument xpsDoc = new(item, FileAccess.Read);
                            FixedDocumentSequence docSeq = xpsDoc.GetFixedDocumentSequence();
                            DocumentPage docPage = null;
                            BitmapFrame bitmapframe = null;
                            byte[] data = null;
                            fileloadtask = Task.Run(() =>
                            {
                                try
                                {
                                    for (int i = 0; i < docSeq.DocumentPaginator.PageCount; i++)
                                    {
                                        Dispatcher.Invoke(() =>
                                        {
                                            docPage = docSeq.DocumentPaginator.GetPage(i);
                                            RenderTargetBitmap rtb = new((int)docPage.Size.Width, (int)docPage.Size.Height, 96, 96, PixelFormats.Default);
                                            rtb.Render(docPage.Visual);
                                            bitmapframe = BitmapFrame.Create(rtb);
                                            data = bitmapframe.ToTiffJpegByteArray(Format.Jpg, Scanner.JpegQuality);
                                            docPage = null;
                                        });
                                        MemoryStream memoryStream = new(data);
                                        BitmapFrame image = BitmapMethods.GenerateImageDocumentBitmapFrame(decodeheight, memoryStream, SelectedPaper);
                                        image.Freeze();
                                        BitmapSource thumbimage = image.PixelWidth < image.PixelHeight ? image.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Width * SelectedPaper.Height) : image.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Height * SelectedPaper.Width);
                                        thumbimage.Freeze();
                                        BitmapFrame bitmapFrame = BitmapFrame.Create(image, thumbimage);
                                        bitmapFrame.Freeze();
                                        uiContext.Send(_ => Scanner?.Resimler.Add(new ScannedImage() { Resim = bitmapFrame }), null);
                                        bitmapFrame = null;
                                        image = null;
                                        thumbimage = null;
                                        bitmapframe = null;
                                        data = null;
                                        memoryStream = null;
                                    }
                                }
                                catch (Exception)
                                {
                                    return;
                                }
                            });
                            break;
                        }
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public BitmapFrame GenerateBitmapFrame(BitmapSource bitmapSource)
        {
            bitmapSource.Freeze();
            BitmapSource thumbnail = bitmapSource.PixelWidth < bitmapSource.PixelHeight ? bitmapSource.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Width * SelectedPaper.Height) : bitmapSource.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Height * SelectedPaper.Width);
            thumbnail.Freeze();
            BitmapFrame bitmapFrame = BitmapFrame.Create(bitmapSource, thumbnail);
            bitmapFrame.Freeze();
            return bitmapFrame;
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

        private ObservableCollection<Chart> blueChart;

        private CroppedBitmap croppedOcrBitmap;

        private int decodeHeight;

        private bool disposedValue;

        private GridLength documentGridLength = new(5, GridUnitType.Star);

        private bool documentPreviewIsExpanded = true;

        private bool dragMoveStarted;

        private Task fileloadtask;

        private ObservableCollection<Chart> greenChart;

        private double height;

        private byte[] ımgData;

        private double ımgLoadResolution = 120;

        private bool isMouseDown;

        private bool isRightMouseDown;

        private ObservableCollection<Paper> papers;

        private double pdfLoadProgressValue;

        private ObservableCollection<Chart> redChart;

        private Scanner scanner;

        private ScannedImage seçiliResim;

        private Tuple<string, int, double, bool> selectedCompressionProfile;

        private Paper selectedPaper = new();

        private double startupcoordx;

        private double startupcoordy;

        private Twain twain;

        private GridLength twainGuiControlLength = new(3, GridUnitType.Star);

        private double width;

        private static string EypMainPdfExtract(string eypfilepath)
        {
            using ZipArchive archive = ZipFile.Open(eypfilepath, ZipArchiveMode.Read);
            if (archive != null)
            {
                ZipArchiveEntry üstveri = archive.Entries.FirstOrDefault(entry => entry.Name == "Ustveri.xml");
                string source = Path.GetTempPath() + Guid.NewGuid() + ".xml";
                üstveri?.ExtractToFile(source, true);
                XDocument xdoc = XDocument.Load(source);
                XNamespace ns = "urn:dpt:eyazisma:schema:xsd:Tipler-2";
                string DosyaAdi = xdoc.Root?.Element(ns + "DosyaAdi")?.Value;
                ZipArchiveEntry yazi = archive.Entries.FirstOrDefault(entry => entry.Name == DosyaAdi);
                string destinationFileName = Path.GetTempPath() + Guid.NewGuid() + ".pdf";
                yazi.ExtractToFile(destinationFileName, true);
                return destinationFileName;
            }
            return null;
        }

        private async Task AddPdfFile(int decodeheight, SynchronizationContext uiContext, byte[] filedata)
        {
            double totalpagecount = await PdfViewer.PdfViewer.PdfPageCountAsync(filedata);
            for (int i = 1; i <= totalpagecount; i++)
            {
                BitmapFrame bitmapFrame = BitmapMethods.GenerateImageDocumentBitmapFrame(decodeheight, await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, i, (int)ImgLoadResolution), SelectedPaper, Scanner.Deskew);
                bitmapFrame.Freeze();
                uiContext.Send(_ =>
                {
                    Scanner?.Resimler.Add(new ScannedImage() { Resim = bitmapFrame });
                    PdfLoadProgressValue = i / totalpagecount;
                }, null);
                bitmapFrame = null;
            }
        }

        private void ButtonedTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Scanner.CaretPosition = (sender as ButtonedTextBox)?.CaretIndex ?? 0;
        }

        private Int32Rect CropPreviewImage(ImageSource ımageSource)
        {
            int height = ((BitmapSource)ımageSource).PixelHeight - (int)Scanner.CropBottom - (int)Scanner.CropTop;
            int width = ((BitmapSource)ımageSource).PixelWidth - (int)Scanner.CropRight - (int)Scanner.CropLeft;
            return (width < 0 || height < 0) ? default : new((int)Scanner.CropLeft, (int)Scanner.CropTop, width, height);
        }

        private void Default_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "AutoFolder")
            {
                Scanner.AutoSave = Directory.Exists(Settings.Default.AutoFolder);
            }
            Settings.Default.Save();
        }

        private ScanSettings DefaultScanSettings()
        {
            ScanSettings scansettings = new()
            {
                UseDocumentFeeder = Settings.Default.Adf,
                ShowTwainUi = Scanner.ShowUi,
                ShowProgressIndicatorUi = Scanner.ShowProgress,
                UseDuplex = Scanner.Duplex,
                ShouldTransferAllPages = true,
                Resolution = new ResolutionSettings(),
                Page = new PageSettings()
            };
            switch (SelectedPaper.PaperType)
            {
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
                ? bitmap.ConvertBlackAndWhite(Scanner.Eşik).ToBitmapImage(ImageFormat.Jpeg, decodepixelheight)
                : (ColourSetting)Settings.Default.Mode == ColourSetting.GreyScale
                    ? bitmap.ConvertBlackAndWhite(Scanner.Eşik, true).ToBitmapImage(ImageFormat.Jpeg, decodepixelheight)
                    : (ColourSetting)Settings.Default.Mode == ColourSetting.Colour
                                    ? bitmap.ToBitmapImage(ImageFormat.Jpeg, decodepixelheight)
                                    : null;
        }

        private void Fastscan(object sender, ScanningCompleteEventArgs e)
        {
            OnPropertyChanged(nameof(Scanner.DetectPageSeperator));

            Scanner.PdfFilePath = PdfGeneration.GetPdfScanPath();

            OnPropertyChanged(nameof(Scanner.ApplyDataBaseOcr));

            if ((ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
            {
                PdfGeneration.GeneratePdf(Scanner.Resimler.ToList(), Format.Tiff, SelectedPaper, Scanner.JpegQuality, null, (int)Settings.Default.Çözünürlük).Save(Scanner.PdfFilePath);
            }
            if ((ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale)
            {
                PdfGeneration.GeneratePdf(Scanner.Resimler.ToList(), Format.Jpg, SelectedPaper, Scanner.JpegQuality, null, (int)Settings.Default.Çözünürlük).Save(Scanner.PdfFilePath);
            }
            if (Settings.Default.ShowFile)
            {
                ExploreFile.Execute(Scanner.PdfFilePath);
            }
            OnPropertyChanged(nameof(Scanner.Resimler));
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
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    isMouseDown = true;
                    Cursor = Cursors.Cross;
                }
                if (e.RightButton == MouseButtonState.Pressed)
                {
                    isRightMouseDown = true;
                    Cursor = Cursors.Cross;
                }
                startupcoordx = e.GetPosition(scrollviewer).X;
                startupcoordy = e.GetPosition(scrollviewer).Y;
            }
        }

        private void ImgViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.Image img && img.Parent is ScrollViewer scrollviewer)
            {
                double mousemovecoordx = e.GetPosition(scrollviewer).X;
                double mousemovecoordy = e.GetPosition(scrollviewer).Y;

                if (isRightMouseDown)
                {
                    mousemovecoordx += scrollviewer.HorizontalOffset;
                    mousemovecoordy += scrollviewer.VerticalOffset;
                    double widthmultiply = SeçiliResim.Resim.PixelWidth / (double)((scrollviewer.ExtentWidth < scrollviewer.ViewportWidth) ? scrollviewer.ViewportWidth : scrollviewer.ExtentWidth);
                    double heightmultiply = SeçiliResim.Resim.PixelHeight / (double)((scrollviewer.ExtentHeight < scrollviewer.ViewportHeight) ? scrollviewer.ViewportHeight : scrollviewer.ExtentHeight);

                    CroppedBitmap cb = new(SeçiliResim.Resim, new Int32Rect((int)(mousemovecoordx * widthmultiply), (int)(mousemovecoordy * heightmultiply), 1, 1));
                    byte[] pixels = new byte[4];
                    cb.CopyPixels(pixels, 4, 0);
                    cb.Freeze();
                    Scanner.SourceColor = System.Windows.Media.Color.FromRgb(pixels[2], pixels[1], pixels[0]).ToString();
                    if (e.RightButton == MouseButtonState.Released)
                    {
                        isRightMouseDown = false;
                        Cursor = Cursors.Arrow;
                    }
                }

                if (isMouseDown)
                {
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
                    Rectangle r = new()
                    {
                        Stroke = stroke,
                        Fill = fill,
                        StrokeThickness = 2,
                        StrokeDashArray = new DoubleCollection(new double[] { 4, 2 }),
                        Width = Math.Abs(mousemovecoordx - startupcoordx),
                        Height = Math.Abs(mousemovecoordy - startupcoordy)
                    };
                    cnv.Children.Clear();
                    _ = cnv.Children.Add(r);
                    if (startupcoordx < mousemovecoordx && startupcoordy < mousemovecoordy)
                    {
                        Canvas.SetLeft(r, startupcoordx);
                        Canvas.SetTop(r, startupcoordy);
                    }
                    if (startupcoordx > mousemovecoordx && startupcoordy > mousemovecoordy)
                    {
                        Canvas.SetLeft(r, mousemovecoordx);
                        Canvas.SetTop(r, mousemovecoordy);
                    }
                    if (startupcoordx < mousemovecoordx && startupcoordy > mousemovecoordy)
                    {
                        Canvas.SetLeft(r, startupcoordx);
                        Canvas.SetTop(r, mousemovecoordy);
                    }
                    if (startupcoordx > mousemovecoordx && startupcoordy < mousemovecoordy)
                    {
                        Canvas.SetLeft(r, mousemovecoordx);
                        Canvas.SetTop(r, startupcoordy);
                    }
                    if (e.LeftButton == MouseButtonState.Released)
                    {
                        cnv.Children.Clear();
                        width = Math.Abs(e.GetPosition(scrollviewer).X - startupcoordx);
                        height = Math.Abs(e.GetPosition(scrollviewer).Y - startupcoordy);

                        if (startupcoordx < mousemovecoordx && startupcoordy < mousemovecoordy)
                        {
                            ImgData = BitmapMethods.CaptureScreen(startupcoordx, startupcoordy, width, height, scrollviewer, SeçiliResim.Resim);
                        }
                        if (startupcoordx > mousemovecoordx && startupcoordy > mousemovecoordy)
                        {
                            ImgData = BitmapMethods.CaptureScreen(mousemovecoordx, mousemovecoordy, width, height, scrollviewer, SeçiliResim.Resim);
                        }
                        if (startupcoordx < mousemovecoordx && startupcoordy > mousemovecoordy)
                        {
                            ImgData = BitmapMethods.CaptureScreen(startupcoordx, mousemovecoordy, width, height, scrollviewer, SeçiliResim.Resim);
                        }
                        if (startupcoordx > mousemovecoordx && startupcoordy < mousemovecoordy)
                        {
                            ImgData = BitmapMethods.CaptureScreen(mousemovecoordx, startupcoordy, width, height, scrollviewer, SeçiliResim.Resim);
                        }
                        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                        {
                            MemoryStream ms = new(ImgData);
                            BitmapFrame bitmapframe = BitmapMethods.GenerateImageDocumentBitmapFrame(DecodeHeight, ms, SelectedPaper);
                            bitmapframe.Freeze();
                            ScannedImage item = new() { Resim = bitmapframe };
                            Scanner.Resimler.Add(item);
                        }
                        startupcoordx = startupcoordy = 0;
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
                ImgViewer.Zoom += (double)(e.Delta > 0 ? .05 : -.05);
                if (ImgViewer.Zoom <= 0.01)
                {
                    ImgViewer.Zoom = 0.01;
                }
            }
        }

        private void ListBox_Drop(object sender, DragEventArgs e)
        {
            string[] droppedfiles = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (droppedfiles?.Length > 0)
            {
                AddFiles(droppedfiles, DecodeHeight);
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

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            Scanner.PdfPassword = ((PasswordBox)sender).SecurePassword;
        }

        private void PdfMergeButton_Drop(object sender, DragEventArgs e)
        {
            IEnumerable<string> pdffiles = ((string[])e.Data.GetData(DataFormats.FileDrop))?.Where(z => string.Equals(Path.GetExtension(z), ".pdf", StringComparison.OrdinalIgnoreCase));
            if (pdffiles.Any())
            {
                PdfGeneration.SavePdfFiles(pdffiles.ToArray());
            }
        }

        private void ResetCropMargin()
        {
            Scanner.CropBottom = 0;
            Scanner.CropLeft = 0;
            Scanner.CropTop = 0;
            Scanner.CropRight = 0;
            Scanner.EnAdet = 1;
            Scanner.BoyAdet = 1;
            Scanner.Brightness = 0;
            Scanner.Threshold = 0;
            Scanner.Watermark = string.Empty;
            Scanner.CroppedImage = null;
            RedChart = null;
            GreenChart = null;
            BlueChart = null;
        }

        private void Run_Drop(object sender, DragEventArgs e)
        {
            if (sender is Run run)
            {
                ScannedImage droppedData = e.Data.GetData(typeof(ScannedImage)) as ScannedImage;
                ScannedImage target = run.DataContext as ScannedImage;

                int removedIdx = Scanner.Resimler.IndexOf(droppedData);
                int targetIdx = Scanner.Resimler.IndexOf(target);

                if (removedIdx < targetIdx)
                {
                    Scanner.Resimler.Insert(targetIdx + 1, droppedData);
                    Scanner.Resimler.RemoveAt(removedIdx);
                }
                else
                {
                    int remIdx = removedIdx + 1;
                    if (Scanner.Resimler.Count + 1 > remIdx)
                    {
                        Scanner.Resimler.Insert(targetIdx, droppedData);
                        Scanner.Resimler.RemoveAt(remIdx);
                    }
                }
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
            Scanner.ArayüzEtkin = false;
            _settings = DefaultScanSettings();
            _settings.Resolution.ColourSetting = (ColourSetting)Settings.Default.Mode;
            _settings.Resolution.Dpi = (int)Settings.Default.Çözünürlük;
            _settings.Rotation = new RotationSettings { AutomaticDeskew = Scanner.Deskew };
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
                Settings.Default.DateGroupFolder = true;
                Scanner.FileName = selectedprofile[9];
                Settings.Default.DefaultProfile = Scanner.SelectedProfile;
                Settings.Default.Save();
            }
            if (e.PropertyName is "ApplyDataBaseOcr" && Scanner.ApplyDataBaseOcr)
            {
                _ = MessageBox.Show(Translation.GetResStringValue("OCRTIME"), Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void Twain_ScanningComplete(object sender, ScanningCompleteEventArgs e)
        {
            Scanner.ArayüzEtkin = true;
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

        private void TwainCtrl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "SelectedCompressionProfile" && SelectedCompressionProfile is not null)
            {
                Settings.Default.Mode = SelectedCompressionProfile.Item2;
                Settings.Default.Çözünürlük = SelectedCompressionProfile.Item3;
                ImgLoadResolution = SelectedCompressionProfile.Item3;
                Scanner.UseMozJpegEncoding = SelectedCompressionProfile.Item4 && MozJpeg.MozJpeg.MozJpegDllExists;
            }
            if (e.PropertyName is "SelectedPaper" && SelectedPaper is not null)
            {
                DecodeHeight = (int)(SelectedPaper.Height / Inch * ImgLoadResolution);
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
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
                Scanner.ArayüzEtkin = false;
            }
        }
    }
}