using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Extensions;
using Microsoft.Win32;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;
using TwainControl.Properties;
using TwainWpf;
using TwainWpf.Wpf;
using static Extensions.ExtensionMethods;
using Path = System.IO.Path;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace TwainControl
{
    public partial class TwainCtrl : UserControl, INotifyPropertyChanged, IDisposable
    {
        public TwainCtrl()
        {
            InitializeComponent();
            DataContext = this;
            Scanner = new Scanner();
            ScanImage = new RelayCommand<object>(parameter =>
            {
                ScanCommonSettings();
                twain.SelectSource(Scanner.SeçiliTarayıcı);
                twain.StartScanning(_settings);
            }, parameter => !Environment.Is64BitProcess && Scanner?.Tarayıcılar?.Count > 0);

            FastScanImage = new RelayCommand<object>(parameter =>
            {
                ScanCommonSettings();
                Scanner.Resimler = new ObservableCollection<ScannedImage>();
                twain.SelectSource(Scanner.SeçiliTarayıcı);
                twain.StartScanning(_settings);

                twain.ScanningComplete += Fastscan;
            }, parameter => !Environment.Is64BitProcess && Scanner.AutoSave && !Scanner.SeperateSave && Scanner?.FileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && Scanner?.Tarayıcılar?.Count > 0);

            ResimSil = new RelayCommand<object>(parameter =>
            {
                _ = (Scanner.Resimler?.Remove(parameter as ScannedImage));
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
                        Filter = "Tif Resmi (*.tif)|*.tif|Jpg Resmi(*.jpg)|*.jpg|Pdf Dosyası(*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası(*.pdf)|*.pdf",
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
                            if (Scanner.RotateAngle is not 0 or 360)
                            {
                                if (MessageBox.Show(Translation.GetResStringValue("ROTSAVE"), Application.Current?.MainWindow?.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                                {
                                    GeneratePdf(scannedImage.Resim, Format.Jpg, true).Save(saveFileDialog.FileName);
                                    return;
                                }
                                GeneratePdf(scannedImage.Resim, Format.Jpg).Save(saveFileDialog.FileName);
                                return;
                            }
                            GeneratePdf(scannedImage.Resim, Format.Jpg).Save(saveFileDialog.FileName);
                            return;
                        }
                        if (saveFileDialog.FilterIndex == 4)
                        {
                            if (Scanner.RotateAngle is not 0 or 360)
                            {
                                if (MessageBox.Show(Translation.GetResStringValue("ROTSAVE"), Application.Current?.MainWindow?.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                                {
                                    GeneratePdf(scannedImage.Resim, Format.Tiff, true).Save(saveFileDialog.FileName);
                                    return;
                                }
                                GeneratePdf(scannedImage.Resim, Format.Tiff).Save(saveFileDialog.FileName);
                                return;
                            }
                            GeneratePdf(scannedImage.Resim, Format.Tiff).Save(saveFileDialog.FileName);
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
                SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Pdf Dosyası(*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası(*.pdf)|*.pdf|Zip Dosyası(*.zip)|*.zip",
                    FileName = Scanner.SaveFileName
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    if (saveFileDialog.FilterIndex == 1)
                    {
                        GeneratePdf(Scanner.Resimler.Where(z => z.Seçili).ToList(), Format.Jpg).Save(saveFileDialog.FileName);
                    }
                    if (saveFileDialog.FilterIndex == 2)
                    {
                        GeneratePdf(Scanner.Resimler.Where(z => z.Seçili).ToList(), Format.Tiff).Save(saveFileDialog.FileName);
                    }
                    if (saveFileDialog.FilterIndex == 3)
                    {
                        string dosyayolu = $"{Path.GetTempPath()}{Guid.NewGuid()}.pdf";
                        GeneratePdf(Scanner.Resimler.Where(z => z.Seçili).ToList(), Format.Jpg).Save(dosyayolu);
                        using ZipArchive archive = ZipFile.Open(saveFileDialog.FileName, ZipArchiveMode.Create);
                        _ = archive.CreateEntryFromFile(dosyayolu, $"{Scanner.SaveFileName}.pdf", CompressionLevel.Optimal);
                        File.Delete(dosyayolu);
                    }
                    if (Path.GetDirectoryName(saveFileDialog.FileName).Contains(Settings.Default.AutoFolder))
                    {
                        OnPropertyChanged(nameof(Scanner.Tarandı));
                    }
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
                    .Append(Scanner.SeperateSave)
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
                    Filter = "Pdf Dosyası(*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası(*.pdf)|*.pdf|Jpg Dosyası(*.jpg)|*.jpg|Png Dosyası(*.png)|*.png",
                    FileName = Scanner.FileName
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    if (saveFileDialog.FilterIndex == 1)
                    {
                        GeneratePdf((BitmapSource)parameter, Format.Jpg).Save(saveFileDialog.FileName);
                        return;
                    }
                    if (saveFileDialog.FilterIndex == 2)
                    {
                        GeneratePdf((BitmapSource)parameter, Format.Tiff).Save(saveFileDialog.FileName);
                        return;
                    }
                    if (saveFileDialog.FilterIndex == 3)
                    {
                        File.WriteAllBytes(saveFileDialog.FileName, ((BitmapSource)parameter).ToTiffJpegByteArray(Format.Jpg));
                    }
                    if (saveFileDialog.FilterIndex == 4)
                    {
                        File.WriteAllBytes(saveFileDialog.FileName, ((BitmapSource)parameter).ToTiffJpegByteArray(Format.Png));
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
                try
                {
                    _ = Process.Start(parameter as string);
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message);
                }
            }, parameter => true);

            SetWatermark = new RelayCommand<object>(parameter => Scanner.CroppedImage = ÜstüneResimÇiz(Scanner.CroppedImage, new System.Windows.Point(Scanner.CroppedImage.Width / 2, Scanner.CroppedImage.Height / 2), System.Windows.Media.Brushes.Red, Scanner.WatermarkTextSize, Scanner.Watermark, Scanner.WatermarkAngle, Scanner.WatermarkFont), parameter => Scanner.CroppedImage is not null && !string.IsNullOrWhiteSpace(Scanner?.Watermark));

            ApplyColorChange = new RelayCommand<object>(parameter => Scanner.CopyCroppedImage = Scanner.CroppedImage, parameter => Scanner.CroppedImage is not null);

            DeskewImage = new RelayCommand<object>(parameter =>
            {
                Deskew sk = new((BitmapSource)Scanner.CroppedImage);
                double skewAngle = -1 * sk.GetSkewAngle(true);
                Scanner.CroppedImage = RotateImage(Scanner.CroppedImage, skewAngle);
            }, parameter => Scanner.CroppedImage is not null);

            OcrPage = new RelayCommand<object>(parameter =>
            {
                ImgData = Scanner.CroppedImage.ToTiffJpegByteArray(Format.Png);
                OnPropertyChanged(nameof(ImgData));
            }, parameter => Scanner.CroppedImage is not null);

            PdfBirleştir = new RelayCommand<object>(parameter =>
            {
                OpenFileDialog openFileDialog = new()
                {
                    Filter = "Pdf Dosyası(*.pdf)|*.pdf",
                    Multiselect = true
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    string[] files = openFileDialog.FileNames;
                    if (files.Length > 0)
                    {
                        SavePdfFiles(files);
                    }
                }
            }, parameter => true);

            LoadImage = new RelayCommand<object>(parameter =>
            {
                OpenFileDialog openFileDialog = new()
                {
                    Filter = "Resim Dosyası(*.jpg;*.jpeg;*.png;*.gif;*.tif;*.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.tif;*.bmp",
                    Multiselect = true
                };
                int dpimultiplier = (int)PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11;
                if (openFileDialog.ShowDialog() == true)
                {
                    Scanner?.Resimler.Clear();
                    GC.Collect();
                    foreach (string item in openFileDialog.FileNames)
                    {
                        BitmapImage image = new();
                        image.BeginInit();
                        image.DecodePixelHeight = (int)(A4Height / 2.54 * ImgLoadResolution);
                        image.CacheOption = BitmapCacheOption.None;
                        image.UriSource = new Uri(item);
                        image.EndInit();
                        image.Freeze();

                        BitmapImage thumbimage = new();
                        thumbimage.BeginInit();
                        thumbimage.DecodePixelHeight = 96 * dpimultiplier;
                        thumbimage.CacheOption = BitmapCacheOption.None;
                        thumbimage.UriSource = new Uri(item);
                        thumbimage.EndInit();
                        thumbimage.Freeze();

                        BitmapFrame bitmapFrame = BitmapFrame.Create(image, thumbimage);
                        bitmapFrame.Freeze();
                        Scanner?.Resimler.Add(new ScannedImage() { Seçili = true, Resim = bitmapFrame });
                    }
                }
            }, parameter => true);

            Scanner.PropertyChanged += Scanner_PropertyChanged;

            Settings.Default.PropertyChanged += Default_PropertyChanged;

            Scanner.SelectedProfile = Settings.Default.DefaultProfile;
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
                    OnPropertyChanged(nameof(ImgLoadResolution));
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

        public static void DefaultPdfCompression(PdfDocument doc)
        {
            doc.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
            doc.Options.CompressContentStreams = true;
            doc.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Automatic;
            doc.Options.NoCompression = false;
            doc.Options.EnableCcittCompressionForBilevelImages = true;
        }

        public static PdfDocument MergePdf(string[] pdffiles)
        {
            using PdfDocument outputDocument = new();
            foreach (string file in pdffiles)
            {
                PdfDocument inputDocument = PdfReader.Open(file, PdfDocumentOpenMode.Import);
                int count = inputDocument.PageCount;
                for (int idx = 0; idx < count; idx++)
                {
                    PdfPage page = inputDocument.Pages[idx];
                    _ = outputDocument.AddPage(page);
                }
            }
            return outputDocument;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public PdfDocument GeneratePdf(BitmapSource bitmapframe, Format format, bool rotate = false)
        {
            try
            {
                using PdfDocument document = new();
                PdfPage page = document.AddPage();
                if (rotate)
                {
                    page.Rotate = (int)Scanner.RotateAngle;
                }
                if (Scanner.PasswordProtect)
                {
                    ApplyPdfSecurity(document);
                }
                using XGraphics gfx = XGraphics.FromPdfPage(page);
                using MemoryStream ms = new(bitmapframe.ToTiffJpegByteArray(format));
                using XImage xImage = XImage.FromStream(ms);
                XSize size = PageSizeConverter.ToSize(PageSize.A4);
                gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                DefaultPdfCompression(document);
                return document;
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message);
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

        private const double A4Height = 29.7;

        private ScanSettings _settings;

        private CroppedBitmap croppedOcrBitmap;

        private bool disposedValue;

        private GridLength documentGridLength = new(5, GridUnitType.Star);

        private bool documentPreviewIsExpanded = true;

        private double height;

        private byte[] ımgData;

        private double ımgLoadResolution = 72;

        private bool isMouseDown;

        private bool isRightMouseDown;

        private Scanner scanner;

        private ScannedImage seçiliResim;

        private string sourceColor = "Transparent";

        private double startupcoordx;

        private double startupcoordy;

        private string targetColor = "Transparent";

        private Twain twain;

        private GridLength twainGuiControlLength = new(3, GridUnitType.Star);

        private double width;

        private void ApplyPdfSecurity(PdfDocument document)
        {
            PdfSecuritySettings securitySettings = document.SecuritySettings;
            if (Scanner.PdfPassword is not null)
            {
                securitySettings.OwnerPassword = Scanner.PdfPassword.ToString();
                securitySettings.PermitModifyDocument = Scanner.AllowEdit;
                securitySettings.PermitPrint = Scanner.AllowPrint;
                securitySettings.PermitExtractContent = Scanner.AllowCopy;
            }
        }

        private Bitmap BitmapSourceToBitmap(BitmapSource bitmapsource)
        {
            FormatConvertedBitmap src = new();
            src.BeginInit();
            src.Source = bitmapsource;
            src.DestinationFormat = PixelFormats.Bgra32;
            src.EndInit();
            Bitmap bitmap = new(src.PixelWidth, src.PixelHeight, PixelFormat.Format32bppArgb);
            BitmapData data = bitmap.LockBits(new System.Drawing.Rectangle(System.Drawing.Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            src.CopyPixels(Int32Rect.Empty, data.Scan0, data.Height * data.Stride, data.Stride);
            bitmap.UnlockBits(data);

            return bitmap;
        }

        private void ButtonedTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Scanner.CaretPosition = (sender as ButtonedTextBox)?.CaretIndex ?? 0;
        }

        private byte[] CaptureScreen(double coordx, double coordy, double selectionwidth, double selectionheight, ScrollViewer scrollviewer)
        {
            try
            {
                coordx += scrollviewer.HorizontalOffset;
                coordy += scrollviewer.VerticalOffset;

                double widthmultiply = SeçiliResim.Resim.PixelWidth / (double)((scrollviewer.ExtentWidth < scrollviewer.ViewportWidth) ? scrollviewer.ViewportWidth : scrollviewer.ExtentWidth);
                double heightmultiply = SeçiliResim.Resim.PixelHeight / (double)((scrollviewer.ExtentHeight < scrollviewer.ViewportHeight) ? scrollviewer.ViewportHeight : scrollviewer.ExtentHeight);

                Int32Rect ınt32Rect = new((int)(coordx * widthmultiply), (int)(coordy * heightmultiply), (int)(selectionwidth * widthmultiply), (int)(selectionheight * heightmultiply));
                CroppedOcrBitmap = new CroppedBitmap(SeçiliResim.Resim, ınt32Rect);
                CroppedOcrBitmap.Freeze();
                return CroppedOcrBitmap.ToTiffJpegByteArray(Format.Png);
            }
            catch (Exception)
            {
                CroppedOcrBitmap = null;
                return null;
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
            return ((ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite) ? bitmap.ConvertBlackAndWhite(Scanner.Eşik).ToBitmapImage(ImageFormat.Tiff, decodepixelheight) : ((ColourSetting)Settings.Default.Mode == ColourSetting.GreyScale) ? bitmap.ConvertBlackAndWhite(Scanner.Eşik, true).ToBitmapImage(ImageFormat.Jpeg, decodepixelheight) : (ColourSetting)Settings.Default.Mode == ColourSetting.Colour
                ? bitmap.ToBitmapImage(ImageFormat.Jpeg, decodepixelheight)
                : (BitmapSource)null;
        }

        private void Fastscan(object sender, ScanningCompleteEventArgs e)
        {
            string pdffilepath = GetPdfScanPath();

            if ((ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
            {
                GeneratePdf(Scanner.Resimler, Format.Tiff).Save(pdffilepath);
            }
            if ((ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale)
            {
                GeneratePdf(Scanner.Resimler, Format.Jpg).Save(pdffilepath);
            }
            if (Settings.Default.ShowFile)
            {
                ExploreFile.Execute(pdffilepath);
            }
            switch (Scanner.ShutDownMode)
            {
                case 1:
                    Shutdown.DoExitWin(Shutdown.EWX_SHUTDOWN);
                    break;

                case 2:
                    Shutdown.DoExitWin(Shutdown.EWX_REBOOT);
                    break;
            }
            twain.ScanningComplete -= Fastscan;
            OnPropertyChanged(nameof(Scanner.Tarandı));
        }

        private PdfDocument GeneratePdf(IList<ScannedImage> bitmapFrames, Format format, bool rotate = false)
        {
            using PdfDocument document = new();
            try
            {
                foreach (ScannedImage scannedimage in bitmapFrames)
                {
                    PdfPage page = document.AddPage();
                    if (rotate)
                    {
                        page.Rotate = (int)Scanner.RotateAngle;
                    }
                    if (Scanner.PasswordProtect)
                    {
                        ApplyPdfSecurity(document);
                    }
                    using XGraphics gfx = XGraphics.FromPdfPage(page);
                    using MemoryStream ms = new(scannedimage.Resim.ToTiffJpegByteArray(format));
                    using XImage xImage = XImage.FromStream(ms);
                    XSize size = PageSizeConverter.ToSize(PageSize.A4);
                    gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                }
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message);
            }
            DefaultPdfCompression(document);
            return document;
        }

        private string GetPdfScanPath()
        {
            return GetSaveFolder().SetUniqueFile(Scanner.SaveFileName, "pdf");
        }

        private string GetSaveFolder()
        {
            string datefolder = $@"{Settings.Default.AutoFolder}\{DateTime.Today.ToShortDateString()}";
            if (!Directory.Exists(datefolder))
            {
                _ = Directory.CreateDirectory(datefolder);
            }
            return datefolder;
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
                            ImgData = CaptureScreen(startupcoordx, startupcoordy, width, height, scrollviewer);
                        }
                        if (startupcoordx > mousemovecoordx && startupcoordy > mousemovecoordy)
                        {
                            ImgData = CaptureScreen(mousemovecoordx, mousemovecoordy, width, height, scrollviewer);
                        }
                        if (startupcoordx < mousemovecoordx && startupcoordy > mousemovecoordy)
                        {
                            ImgData = CaptureScreen(startupcoordx, mousemovecoordy, width, height, scrollviewer);
                        }
                        if (startupcoordx > mousemovecoordx && startupcoordy < mousemovecoordy)
                        {
                            ImgData = CaptureScreen(mousemovecoordx, startupcoordy, width, height, scrollviewer);
                        }
                        startupcoordx = startupcoordy = 0;
                        isMouseDown = false;
                        Cursor = Cursors.Arrow;
                        OnPropertyChanged(nameof(ImgData));
                    }
                }
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            Scanner.PdfPassword = ((PasswordBox)sender).SecurePassword;
        }

        private unsafe Bitmap ReplaceColor(Bitmap source, System.Windows.Media.Color toReplace, System.Windows.Media.Color replacement, int threshold)
        {
            const int pixelSize = 4; // 32 bits per pixel

            Bitmap target = new(source.Width, source.Height, PixelFormat.Format32bppArgb);

            BitmapData sourceData = null, targetData = null;

            try
            {
                sourceData = source.LockBits(new System.Drawing.Rectangle(0, 0, source.Width, source.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                targetData = target.LockBits(new System.Drawing.Rectangle(0, 0, target.Width, target.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                for (int y = 0; y < source.Height; ++y)
                {
                    byte* sourceRow = (byte*)sourceData.Scan0 + (y * sourceData.Stride);
                    byte* targetRow = (byte*)targetData.Scan0 + (y * targetData.Stride);

                    _ = Parallel.For(0, source.Width, x =>
                    {
                        byte b = sourceRow[(x * pixelSize) + 0];
                        byte g = sourceRow[(x * pixelSize) + 1];
                        byte r = sourceRow[(x * pixelSize) + 2];
                        byte a = sourceRow[(x * pixelSize) + 3];

                        if (toReplace.R + threshold >= r && toReplace.R - threshold <= r && toReplace.G + threshold >= g && toReplace.G - threshold <= g && toReplace.B + threshold >= b && toReplace.B - threshold <= b)
                        {
                            r = replacement.R;
                            g = replacement.G;
                            b = replacement.B;
                        }

                        targetRow[(x * pixelSize) + 0] = b;
                        targetRow[(x * pixelSize) + 1] = g;
                        targetRow[(x * pixelSize) + 2] = r;
                        targetRow[(x * pixelSize) + 3] = a;
                    });
                }
            }
            finally
            {
                if (sourceData != null)
                {
                    source.UnlockBits(sourceData);
                }

                if (targetData != null)
                {
                    target.UnlockBits(targetData);
                }
            }

            return target;
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

        private RenderTargetBitmap RotateImage(ImageSource Source, double angle)
        {
            DrawingVisual dv = new();
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.PushTransform(new RotateTransform(angle));
                dc.DrawImage(Source, new Rect(0, 0, ((BitmapSource)Source).PixelWidth, ((BitmapSource)Source).PixelHeight));
            }
            RenderTargetBitmap rtb = new(((BitmapSource)Source).PixelWidth, ((BitmapSource)Source).PixelHeight, 96, 96, PixelFormats.Default);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        private void SavePdfFiles(string[] files)
        {
            SaveFileDialog saveFileDialog = new()
            {
                Filter = "Pdf Dosyası(*.pdf)|*.pdf",
                FileName = Translation.GetResStringValue("MERGE")
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    MergePdf(files).Save(saveFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message);
                }
            }
        }

        private void ScanCommonSettings()
        {
            Scanner.ArayüzEtkin = false;
            _settings = DefaultScanSettings();
            _settings.Resolution.ColourSetting = (ColourSetting)Settings.Default.Mode;
            _settings.Resolution.Dpi = (int)Settings.Default.Çözünürlük;
            _settings.Rotation = new RotationSettings { AutomaticDeskew = Scanner.Deskew, AutomaticRotate = Scanner.AutoRotate, AutomaticBorderDetection = Scanner.BorderDetect };
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
                Scanner.SeperateSave = bool.Parse(selectedprofile[6]);
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
                using Bitmap bmp = BitmapSourceToBitmap((BitmapSource)Scanner.CopyCroppedImage);
                Scanner.CroppedImage = ReplaceColor(bmp, source, target, (int)Scanner.Threshold).ToBitmapImage(ImageFormat.Png);
            }
            if (e.PropertyName is "CroppedImageAngle" && Scanner.CroppedImageAngle != 0)
            {
                TransformedBitmap transformedBitmap = new((BitmapSource)Scanner.CroppedImage, new RotateTransform(Scanner.CroppedImageAngle * 90));
                transformedBitmap.Freeze();
                Scanner.CroppedImage = transformedBitmap;
                Scanner.CroppedImageAngle = 0;
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
                BitmapSource evrak = EvrakOluştur(bitmap);
                evrak.Freeze();
                BitmapSource önizleme = evrak.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / 21 * 29.7);
                önizleme.Freeze();
                BitmapFrame bitmapFrame = BitmapFrame.Create(evrak, önizleme);
                bitmapFrame.Freeze();
                Scanner.Resimler.Add(new ScannedImage() { Resim = bitmapFrame });
                if (Scanner.SeperateSave && (ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
                {
                    GeneratePdf(bitmapFrame, Format.Tiff).Save(GetSaveFolder().SetUniqueFile(Scanner.SaveFileName, "pdf"));
                    OnPropertyChanged(nameof(Scanner.Tarandı));
                }

                if (Scanner.SeperateSave && ((ColourSetting)Settings.Default.Mode == ColourSetting.GreyScale || (ColourSetting)Settings.Default.Mode == ColourSetting.Colour))
                {
                    GeneratePdf(bitmapFrame, Format.Jpg).Save(GetSaveFolder().SetUniqueFile(Scanner.SaveFileName, "pdf"));
                    OnPropertyChanged(nameof(Scanner.Tarandı));
                }

                evrak = null;
                bitmapFrame = null;
                önizleme = null;
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

        private RenderTargetBitmap ÜstüneResimÇiz(ImageSource Source, System.Windows.Point konum, System.Windows.Media.Brush brushes, double emSize = 64, string metin = null, double angle = 315, string font = "Arial")
        {
            FormattedText formattedText = new(metin, CultureInfo.GetCultureInfo("tr-TR"), FlowDirection.LeftToRight, new Typeface(font), emSize, brushes) { TextAlignment = TextAlignment.Center };
            DrawingVisual dv = new();
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawImage(Source, new Rect(0, 0, ((BitmapSource)Source).Width, ((BitmapSource)Source).Height));
                dc.PushTransform(new RotateTransform(angle, konum.X, konum.Y));
                dc.DrawText(formattedText, new System.Windows.Point(konum.X, konum.Y - (formattedText.Height / 2)));
            }

            RenderTargetBitmap rtb = new((int)((BitmapSource)Source).Width, (int)((BitmapSource)Source).Height, 96, 96, PixelFormats.Default);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }
    }
}