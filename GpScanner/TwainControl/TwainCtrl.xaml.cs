using System;
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
using Extensions;
using Microsoft.Win32;
using TwainControl.Properties;
using TwainWpf;
using TwainWpf.Wpf;
using static Extensions.ExtensionMethods;
using Path = System.IO.Path;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace TwainControl
{
    public partial class TwainCtrl : UserControl, INotifyPropertyChanged, IDisposable
    {
        public static Task filesavetask;

        public TwainCtrl()
        {
            InitializeComponent();
            DataContext = this;
            Scanner = new Scanner();
            PdfGeneration.Scanner = Scanner;
            Scanner.PropertyChanged += Scanner_PropertyChanged;
            Settings.Default.PropertyChanged += Default_PropertyChanged;

            DecodeHeight = (int)(A4Height / 2.54 * ImgLoadResolution);
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
                if (parameter is ScannedImage scannedImage)
                {
                    SaveFileDialog saveFileDialog = new()
                    {
                        Filter = "Tif Resmi (*.tif)|*.tif|Jpg Resmi (*.jpg)|*.jpg|Pdf Dosyası (*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası (*.pdf)|*.pdf",
                        FileName = Scanner.SaveFileName
                    };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        if (saveFileDialog.FilterIndex == 1)
                        {
                            if ((ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
                            {
                                File.WriteAllBytes(saveFileDialog.FileName, scannedImage.Resim.ToTiffJpegByteArray(Format.Tiff));
                                return;
                            }
                            if ((ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale)
                            {
                                File.WriteAllBytes(saveFileDialog.FileName, scannedImage.Resim.ToTiffJpegByteArray(Format.TiffRenkli));
                                return;
                            }
                        }
                        if (saveFileDialog.FilterIndex == 2)
                        {
                            File.WriteAllBytes(saveFileDialog.FileName, scannedImage.Resim.ToTiffJpegByteArray(Format.Jpg));
                            return;
                        }
                        if (saveFileDialog.FilterIndex == 3)
                        {
                            PdfGeneration.GeneratePdf(scannedImage.Resim, null, Format.Jpg, Scanner.JpegQuality).Save(saveFileDialog.FileName);
                            return;
                        }
                        if (saveFileDialog.FilterIndex == 4)
                        {
                            PdfGeneration.GeneratePdf(scannedImage.Resim, null, Format.Tiff, Scanner.JpegQuality).Save(saveFileDialog.FileName);
                        }
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
                if (filesavetask?.IsCompleted == false)
                {
                    _ = MessageBox.Show("İşlem Devam Ediyor. Bitmesini Bekleyin.");
                    return;
                }
                SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Pdf Dosyası (*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası (*.pdf)|*.pdf|Zip Dosyası (*.zip)|*.zip|Jpg Resmi (*.jpg)|*.jpg",
                    FileName = Scanner.SaveFileName
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    filesavetask = Task.Run(() =>
                    {
                        if (saveFileDialog.FilterIndex == 1)
                        {
                            PdfGeneration.GeneratePdf(Scanner.Resimler.Where(z => z.Seçili), Format.Jpg, Scanner.JpegQuality).Save(saveFileDialog.FileName);
                            return;
                        }
                        if (saveFileDialog.FilterIndex == 2)
                        {
                            PdfGeneration.GeneratePdf(Scanner.Resimler.Where(z => z.Seçili), Format.Tiff, Scanner.JpegQuality).Save(saveFileDialog.FileName);
                            return;
                        }
                        if (saveFileDialog.FilterIndex == 3)
                        {
                            string dosyayolu = $"{Path.GetTempPath()}{Guid.NewGuid()}.pdf";
                            PdfGeneration.GeneratePdf(Scanner.Resimler.Where(z => z.Seçili), Format.Jpg, Scanner.JpegQuality).Save(dosyayolu);
                            using ZipArchive archive = ZipFile.Open(saveFileDialog.FileName, ZipArchiveMode.Update);
                            _ = archive.CreateEntryFromFile(dosyayolu, $"{Scanner.SaveFileName}.pdf", CompressionLevel.Optimal);
                            File.Delete(dosyayolu);
                            return;
                        }
                        if (saveFileDialog.FilterIndex == 4)
                        {
                            string filename = saveFileDialog.FileName;
                            string directory = Path.GetDirectoryName(filename);
                            foreach (ScannedImage item in Scanner.Resimler.Where(z => z.Seçili))
                            {
                                File.WriteAllBytes(directory.SetUniqueFile(Path.GetFileNameWithoutExtension(filename), "jpg"), item.Resim.ToTiffJpegByteArray(Format.Jpg));
                            }
                        }
                    });
                }
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

            InsertFileNamePlaceHolder = new RelayCommand<object>(parameter =>
            {
                string placeholder = parameter as string;
                Scanner.FileName = $"{Scanner.FileName.Substring(0, Scanner.CaretPosition)}{placeholder}{Scanner.FileName.Substring(Scanner.CaretPosition, Scanner.FileName.Length - Scanner.CaretPosition)}";
            }, parameter => true);

            SaveImage = new RelayCommand<object>(parameter =>
            {
                SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Pdf Dosyası (*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası (*.pdf)|*.pdf|Jpg Dosyası (*.jpg)|*.jpg|Png Dosyası (*.png)|*.png",
                    FileName = Scanner.FileName
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    switch (saveFileDialog.FilterIndex)
                    {
                        case 1:
                            PdfGeneration.GeneratePdf((BitmapSource)parameter, null, Format.Jpg, Scanner.JpegQuality).Save(saveFileDialog.FileName);
                            return;

                        case 2:
                            PdfGeneration.GeneratePdf((BitmapSource)parameter, null, Format.Tiff, Scanner.JpegQuality).Save(saveFileDialog.FileName);
                            return;

                        case 3:
                            File.WriteAllBytes(saveFileDialog.FileName, ((BitmapSource)parameter).ToTiffJpegByteArray(Format.Jpg));
                            return;

                        case 4:
                            File.WriteAllBytes(saveFileDialog.FileName, ((BitmapSource)parameter).ToTiffJpegByteArray(Format.Png));
                            break;
                    }
                }
            }, parameter => Scanner.CroppedImage is not null);

            SplitImage = new RelayCommand<object>(parameter =>
            {
                BitmapSource image = (BitmapSource)Scanner.CroppedImage;
                _ = Directory.CreateDirectory($@"{Settings.Default.AutoFolder}\Parçalanmış");
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
                            File.WriteAllBytes($@"{Settings.Default.AutoFolder}\Parçalanmış".SetUniqueFile("Parçalanmış", "jpg"), croppedBitmap.ToTiffJpegByteArray(Format.Jpg));
                        }
                    }
                }
                WebAdreseGit.Execute($@"{Settings.Default.AutoFolder}\Parçalanmış");
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
                if (pdfloadtask?.IsCompleted == false)
                {
                    _ = MessageBox.Show("İşlem Devam Ediyor. Bitmesini Bekleyin.");
                    return;
                }
                OpenFileDialog openFileDialog = new()
                {
                    Filter = "Tüm Dosyalar (*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.pdf)|*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.pdf|Resim Dosyası (*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle)|*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle|Pdf Dosyası (*.pdf)|*.pdf",
                    Multiselect = true
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    AddFiles(openFileDialog.FileNames, DecodeHeight);
                }
            }, parameter => true);

            PrintCroppedImage = new RelayCommand<object>(parameter => PdfViewer.PdfViewer.PrintImageSource(parameter as ImageSource), parameter => Scanner.CroppedImage is not null);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand ApplyColorChange { get; }

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

        public ICommand ExploreFile { get; }

        public ICommand FastScanImage { get; }

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
                    DecodeHeight = (int)(A4Height / 2.54 * ImgLoadResolution);
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

        public ICommand LoadImage { get; }

        public ICommand OcrPage { get; }

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

        public ICommand RemoveProfile { get; }

        public ICommand ResetCroppedImage { get; }

        public ICommand ResimSil { get; }

        public ICommand SaveImage { get; }

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

        public ICommand SetWatermark { get; }

        public string SourceColor
        {
            get => sourceColor;

            set
            {
                if (sourceColor != value)
                {
                    sourceColor = value;
                    OnPropertyChanged(nameof(SourceColor));
                }
            }
        }

        public ICommand SplitImage { get; }

        public string TargetColor
        {
            get => targetColor; set

            {
                if (targetColor != value)
                {
                    targetColor = value;
                    OnPropertyChanged(nameof(TargetColor));
                }
            }
        }

        public ICommand Tersiniİşaretle { get; }

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

        public void Dispose()
        {
            Dispose(true);
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

        private const double A4Height = 29.7;

        private ScanSettings _settings;

        private CroppedBitmap croppedOcrBitmap;

        private int decodeHeight;

        private bool disposedValue;

        private GridLength documentGridLength = new(5, GridUnitType.Star);

        private bool documentPreviewIsExpanded = true;

        private double height;

        private byte[] ımgData;

        private double ımgLoadResolution = 120;

        private bool isMouseDown;

        private bool isRightMouseDown;

        private double pdfLoadProgressValue;

        private Task pdfloadtask;

        private Scanner scanner;

        private ScannedImage seçiliResim;

        private string sourceColor = "Transparent";

        private double startupcoordx;

        private double startupcoordy;

        private string targetColor = "Transparent";

        private Twain twain;

        private GridLength twainGuiControlLength = new(3, GridUnitType.Star);

        private double width;

        private void AddFiles(string[] filenames, int decodeheight)
        {
            foreach (string item in filenames)
            {
                switch (Path.GetExtension(item.ToLower()))
                {
                    case ".pdf":
                        {
                            SynchronizationContext uiContext = SynchronizationContext.Current;
                            byte[] filedata = null;
                            pdfloadtask = Task.Run(async () =>
                            {
                                filedata = File.ReadAllBytes(item);
                                if (PdfGeneration.IsValidPdfFile(filedata.Take(4)))
                                {
                                    double totalpagecount = PdfViewer.PdfViewer.PdfPageCount(filedata);
                                    for (int i = 1; i <= totalpagecount; i++)
                                    {
                                        BitmapFrame bitmapFrame = BitmapMethods.GenerateImageDocumentBitmapFrame(decodeheight, await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, i, (int)ImgLoadResolution), Scanner.Deskew);
                                        bitmapFrame.Freeze();
                                        uiContext.Send(_ =>
                                        {
                                            Scanner?.Resimler.Add(new ScannedImage() { Resim = bitmapFrame });
                                            PdfLoadProgressValue = i / totalpagecount;
                                        }, null);
                                        bitmapFrame = null;
                                    }
                                }
                                filedata = null;
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
                    case ".tif":
                    case ".tıf":
                    case ".tiff":
                    case ".tıff":
                    case ".bmp":
                        {
                            BitmapFrame bitmapFrame = BitmapMethods.GenerateImageDocumentBitmapFrame(decodeheight, new Uri(item));
                            bitmapFrame.Freeze();
                            Scanner?.Resimler.Add(new ScannedImage() { Resim = bitmapFrame });
                            bitmapFrame = null;
                            break;
                        }
                }
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
            return new()
            {
                UseDocumentFeeder = Settings.Default.Adf,
                ShowTwainUi = Scanner.ShowUi,
                ShowProgressIndicatorUi = Scanner.ShowProgress,
                UseDuplex = Scanner.Duplex,
                ShouldTransferAllPages = true,
                Resolution = new ResolutionSettings()
            };
        }

        private BitmapSource EvrakOluştur(Bitmap bitmap)
        {
            int decodepixelheight = (int)(A4Height / 2.54 * Settings.Default.Çözünürlük);
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
            if (Scanner.ApplyOcr)
            {
                OnPropertyChanged(nameof(Scanner.ApplyOcr));
            }
            else
            {
                if ((ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
                {
                    PdfGeneration.GeneratePdf(Scanner.Resimler, Format.Tiff, Scanner.JpegQuality).Save(Scanner.PdfFilePath);
                }
                if ((ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale)
                {
                    PdfGeneration.GeneratePdf(Scanner.Resimler, Format.Jpg, Scanner.JpegQuality).Save(Scanner.PdfFilePath);
                }
                if (Settings.Default.ShowFile)
                {
                    ExploreFile.Execute(Scanner.PdfFilePath);
                }
                OnPropertyChanged(nameof(Scanner.Resimler));
            }

            twain.ScanningComplete -= Fastscan;
        }

        private void GridSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TwainGuiControlLength = new(3, GridUnitType.Star);
            DocumentGridLength = new(5, GridUnitType.Star);
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
                    SourceColor = System.Windows.Media.Color.FromRgb(pixels[2], pixels[1], pixels[0]).ToString();
                    if (e.RightButton == MouseButtonState.Released)
                    {
                        isRightMouseDown = false;
                        Cursor = Cursors.Arrow;
                    }
                }

                if (isMouseDown)
                {
                    Rectangle r = new()
                    {
                        Stroke = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#33FF0000")),
                        Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3300FF00")),
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
                        startupcoordx = startupcoordy = 0;
                        isMouseDown = false;
                        Cursor = Cursors.Arrow;
                        OnPropertyChanged(nameof(ImgData));
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
            string[] droppedfiles = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (droppedfiles?.Where(z => string.Equals(Path.GetExtension(z), ".pdf", StringComparison.OrdinalIgnoreCase)).Any() == true)
            {
                PdfGeneration.SavePdfFiles(droppedfiles);
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
            Scanner.CroppedImage = null;
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
                _ = DragDrop.DoDragDrop(run, run.DataContext, DragDropEffects.Move);
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
            if (e.PropertyName is "EnAdet")
            {
                LineGrid.ColumnDefinitions.Clear();
                for (int i = 0; i < Scanner.EnAdet; i++)
                {
                    LineGrid.ColumnDefinitions.Add(new ColumnDefinition());
                }
            }
            if (e.PropertyName is "BoyAdet")
            {
                LineGrid.RowDefinitions.Clear();
                for (int i = 0; i < Scanner.BoyAdet; i++)
                {
                    LineGrid.RowDefinitions.Add(new RowDefinition());
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
            if (e.PropertyName is "Threshold" && Scanner.CopyCroppedImage is not null)
            {
                System.Windows.Media.Color source = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(SourceColor);
                System.Windows.Media.Color target = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(TargetColor);
                using Bitmap bmp = ((BitmapSource)Scanner.CopyCroppedImage).BitmapSourceToBitmap();
                Scanner.CroppedImage = bmp.ReplaceColor(source, target, (int)Scanner.Threshold).ToBitmapImage(ImageFormat.Png);
            }
            if (e.PropertyName is "CroppedImageAngle" && Scanner.CopyCroppedImage is not null)
            {
                TransformedBitmap transformedBitmap = new((BitmapSource)Scanner.CopyCroppedImage, new RotateTransform(Scanner.CroppedImageAngle));
                transformedBitmap.Freeze();
                Scanner.CroppedImage = transformedBitmap;
            }
            if (e.PropertyName is "ApplyOcr" && Scanner.ApplyOcr)
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
                BitmapSource önizleme = evrak.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / 21 * 29.7);
                önizleme.Freeze();
                BitmapFrame bitmapFrame = BitmapFrame.Create(evrak, önizleme);
                bitmapFrame.Freeze();
                Scanner?.Resimler?.Add(new ScannedImage() { Resim = bitmapFrame });
                evrak = null;
                önizleme = null;
                bitmapFrame = null;
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