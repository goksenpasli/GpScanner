using Extensions;
using Microsoft.Win32;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        public TwainCtrl()
        {
            InitializeComponent();
            DataContext = this;
            Scanner = new Scanner();

            ScanImage = new RelayCommand<object>(parameter =>
            {
                ScanCommonSettings();
                if (Scanner.Tarayıcılar.Count > 0)
                {
                    twain.SelectSource(Scanner.SeçiliTarayıcı);
                    twain.StartScanning(_settings);
                }
            }, parameter => !Environment.Is64BitProcess);

            FastScanImage = new RelayCommand<object>(parameter =>
            {
                ScanCommonSettings();
                if (Scanner.Tarayıcılar.Count > 0)
                {
                    Scanner.Resimler = new ObservableCollection<ScannedImage>();
                    twain.SelectSource(Scanner.SeçiliTarayıcı);
                    twain.StartScanning(_settings);
                }
                twain.ScanningComplete += Fastscan;
            }, parameter => !Environment.Is64BitProcess && Scanner.AutoSave && Scanner?.FileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);

            ResimSil = new RelayCommand<object>(parameter =>
            {
                _ = (Scanner.Resimler?.Remove(parameter as ScannedImage));
                ResetCropMargin();
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
                        switch (saveFileDialog.FilterIndex)
                        {
                            case 1:
                                if ((ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
                                {
                                    File.WriteAllBytes(saveFileDialog.FileName, scannedImage.Resim.ToTiffJpegByteArray(Format.Tiff));
                                    return;
                                }
                                if ((ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale)
                                {
                                    File.WriteAllBytes(saveFileDialog.FileName, scannedImage.Resim.ToTiffJpegByteArray(Format.TiffRenkli));
                                }
                                return;

                            case 2:
                                File.WriteAllBytes(saveFileDialog.FileName, scannedImage.Resim.ToTiffJpegByteArray(Format.Jpg));
                                return;

                            case 3:
                                if (Scanner.RotateAngle is not 0 or 360)
                                {
                                    if (MessageBox.Show("Döndürme Uygulanarak Kaydedilsin mi?", Application.Current?.MainWindow?.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                                    {
                                        PdfKaydet(scannedImage.Resim, saveFileDialog.FileName, Format.Jpg, true);
                                        return;
                                    }
                                    PdfKaydet(scannedImage.Resim, saveFileDialog.FileName, Format.Jpg);
                                    return;
                                }
                                PdfKaydet(scannedImage.Resim, saveFileDialog.FileName, Format.Jpg);
                                return;

                            case 4:
                                if (Scanner.RotateAngle is not 0 or 360)
                                {
                                    if (MessageBox.Show("Döndürme Uygulanarak Kaydedilsin mi?", Application.Current?.MainWindow?.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                                    {
                                        PdfKaydet(scannedImage.Resim, saveFileDialog.FileName, Format.Tiff, true);
                                        return;
                                    }
                                    PdfKaydet(scannedImage.Resim, saveFileDialog.FileName, Format.Tiff);
                                    return;
                                }
                                PdfKaydet(scannedImage.Resim, saveFileDialog.FileName, Format.Tiff);
                                return;
                        }
                    }
                }
            }, parameter => Scanner?.FileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);

            Tümünüİşaretle = new RelayCommand<object>(parameter =>
            {
                foreach (ScannedImage item in Scanner.Resimler.ToList())
                {
                    item.Seçili = true;
                }
            }, parameter => Scanner.Resimler?.Count > 0);

            TümününİşaretiniKaldır = new RelayCommand<object>(parameter =>
            {
                foreach (ScannedImage item in Scanner.Resimler.ToList())
                {
                    item.Seçili = false;
                }
            }, parameter => Scanner.Resimler?.Count > 0);

            Tersiniİşaretle = new RelayCommand<object>(parameter =>
            {
                foreach (ScannedImage item in Scanner.Resimler.ToList())
                {
                    item.Seçili = !item.Seçili;
                }
            }, parameter => Scanner.Resimler?.Count > 0);

            KayıtYoluBelirle = new RelayCommand<object>(parameter =>
            {
                System.Windows.Forms.FolderBrowserDialog dialog = new()
                {
                    Description = "Otomatik Kayıt Klasörünü Belirtin.",
                    SelectedPath = Settings.Default.AutoFolder
                };
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    Settings.Default.AutoFolder = dialog.SelectedPath;
                    Scanner.LocalizedPath = GetDisplayName(dialog.SelectedPath);
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
                    switch (saveFileDialog.FilterIndex)
                    {
                        case 1:
                            {
                                PdfKaydet(Scanner.Resimler.Where(z => z.Seçili).ToArray(), saveFileDialog.FileName, Format.Jpg);
                                return;
                            }

                        case 2:
                            {
                                PdfKaydet(Scanner.Resimler.Where(z => z.Seçili).ToArray(), saveFileDialog.FileName, Format.Tiff);
                                return;
                            }

                        case 3:
                            {
                                string dosyayolu = $"{Path.GetTempPath()}{Guid.NewGuid()}.pdf";
                                PdfKaydet(Scanner.Resimler.Where(z => z.Seçili).ToArray(), dosyayolu, Format.Jpg);
                                using ZipArchive archive = ZipFile.Open(saveFileDialog.FileName, ZipArchiveMode.Create);
                                _ = archive.CreateEntryFromFile(dosyayolu, $"{Scanner.SaveFileName}.pdf", CompressionLevel.Optimal);
                                File.Delete(dosyayolu);
                                return;
                            }
                    }
                }
            }, parameter =>
            {
                Scanner.SeçiliResimSayısı = Scanner.Resimler.Count(z => z.Seçili);
                return Scanner.SeçiliResimSayısı > 0 && Scanner?.FileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
            });

            ListeTemizle = new RelayCommand<object>(parameter =>
            {
                if (MessageBox.Show("Tüm Taranan Evrak Silinecek Devam Etmek İstiyor Musun?", Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    Scanner.Resimler?.Clear();
                    ResetCropMargin();
                }
            }, parameter => Scanner.Resimler?.Count > 0);

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
                    .Append(true)//Settings.Default.DateGroupFolder
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
                Settings.Default.Save();
                Settings.Default.Reload();
            }, parameter => true);

            InsertFileNamePlaceHolder = new RelayCommand<object>(parameter =>
            {
                string placeholder = parameter as string;
                Scanner.FileName = $"{Scanner.FileName.Substring(0, Scanner.CaretPosition)}{placeholder}{Scanner.FileName.Substring(Scanner.CaretPosition, Scanner.FileName.Length - Scanner.CaretPosition)}";
            }, parameter => true);

            SaveCroppedImage = new RelayCommand<object>(parameter =>
            {
                SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Pdf Dosyası(*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası(*.pdf)|*.pdf|Jpg Dosyası(*.jpg)|*.jpg",
                    FileName = Scanner.FileName
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    switch (saveFileDialog.FilterIndex)
                    {
                        case 1:
                            PdfKaydet((BitmapSource)Scanner.CroppedImage, saveFileDialog.FileName, Format.Jpg);
                            return;

                        case 2:
                            PdfKaydet((BitmapSource)Scanner.CroppedImage, saveFileDialog.FileName, Format.Tiff);
                            return;

                        case 3:
                            File.WriteAllBytes(saveFileDialog.FileName, Scanner.CroppedImage.ToTiffJpegByteArray(Format.Jpg));
                            return;
                    }
                }
            }, parameter => Scanner.CroppedImage is not null && (Scanner.CropRight != 0 || Scanner.CropTop != 0 || Scanner.CropBottom != 0 || Scanner.CropLeft != 0));

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

            DeskewImage = new RelayCommand<object>(parameter =>
            {
                Deskew sk = new((BitmapSource)Scanner.CroppedImage);
                double skewAngle = -1 * sk.GetSkewAngle(true);
                Scanner.CroppedImage = RotateImage(Scanner.CroppedImage, skewAngle);
            }, parameter => Scanner.CroppedImage is not null);

            SaveWatermarkedPdf = new RelayCommand<object>(parameter => SaveCroppedImage.Execute(null), parameter => Scanner.CroppedImage is not null && !string.IsNullOrWhiteSpace(Scanner?.Watermark));

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
                        SaveFileDialog saveFileDialog = new()
                        {
                            Filter = "Pdf Dosyası(*.pdf)|*.pdf",
                            FileName = "Birleştirilmiş"
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
                }
            }, parameter => true);

            Scanner.PropertyChanged += Scanner_PropertyChanged;

            Settings.Default.PropertyChanged += Default_PropertyChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

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

        public ICommand InsertFileNamePlaceHolder { get; }

        public ICommand Kaydet { get; }

        public ICommand KayıtYoluBelirle { get; }

        public ICommand ListeTemizle { get; }

        public ICommand PdfBirleştir { get; }

        public ICommand RemoveProfile { get; }

        public ICommand ResetCroppedImage { get; }

        public ICommand ResimSil { get; }

        public ICommand SaveCroppedImage { get; }

        public ICommand SaveProfile { get; }

        public ICommand SaveWatermarkedPdf { get; }

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

        public ICommand SetWatermark { get; }

        public ICommand SplitImage { get; }

        public ICommand Tersiniİşaretle { get; }

        public ICommand Tümünüİşaretle { get; }

        public ICommand TümününİşaretiniKaldır { get; }

        public ICommand WebAdreseGit { get; }

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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Scanner.Resimler = null;
                    twain = null;
                }

                disposedValue = true;
            }
        }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ScanSettings _settings;

        private CroppedBitmap croppedOcrBitmap;

        private bool disposedValue;

        private double height;

        private byte[] ımgData;

        private bool isMouseDown;

        private Scanner scanner;

        private Twain twain;

        private double width;

        private double x;

        private double y;

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

        private void ButtonedTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Scanner.CaretPosition = (sender as ButtonedTextBox)?.CaretIndex ?? 0;
        }

        private byte[] CaptureScreen(double x, double y, double width, double height)
        {
            try
            {
                double widthmultiply = Scanner.SeçiliResim.Resim.PixelWidth / ImgViewer.RenderSize.Width;
                double heightmultiply = Scanner.SeçiliResim.Resim.PixelHeight / ImgViewer.RenderSize.Height;
                Int32Rect ınt32Rect = new((int)(x * widthmultiply), (int)(y * heightmultiply), (int)(width * widthmultiply), (int)(height * heightmultiply));
                CroppedOcrBitmap = new CroppedBitmap(Scanner.SeçiliResim.Resim, ınt32Rect);
                return CroppedOcrBitmap.ToTiffJpegByteArray(Format.Png);
            }
            catch (Exception)
            {
                CroppedOcrBitmap = null;
                return null;
            }
        }

        private bool CheckAutoSave()
        {
            return Scanner.SeperateSave && Scanner.AutoSave;
        }

        private Int32Rect CropImageRect(ImageSource ımageSource)
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

        private void DefaultPdfCompression(PdfDocument doc)
        {
            doc.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
            doc.Options.CompressContentStreams = true;
            doc.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Automatic;
            doc.Options.NoCompression = false;
            doc.Options.EnableCcittCompressionForBilevelImages = true;
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
            const float mmpi = 25.4f;
            double dpi = Settings.Default.Çözünürlük;
            return (ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite
                ? bitmap.ConvertBlackAndWhite(Scanner.Eşik).ToBitmapImage(ImageFormat.Tiff, SystemParameters.PrimaryScreenHeight).Resize(210 / mmpi * dpi, 297 / mmpi * dpi)
                : (ColourSetting)Settings.Default.Mode == ColourSetting.GreyScale
                ? bitmap.ConvertBlackAndWhite(Scanner.Eşik, true).ToBitmapImage(ImageFormat.Jpeg, SystemParameters.PrimaryScreenHeight).Resize(210 / mmpi * dpi, 297 / mmpi * dpi)
                : (ColourSetting)Settings.Default.Mode == ColourSetting.Colour
                ? bitmap.ToBitmapImage(ImageFormat.Jpeg, SystemParameters.PrimaryScreenHeight).Resize(210 / mmpi * dpi, 297 / mmpi * dpi)
                : null;
        }

        private void Fastscan(object sender, ScanningCompleteEventArgs e)
        {
            string pdffilepath = GetPdfScanPath();

            if ((ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
            {
                PdfKaydet(Scanner.Resimler, pdffilepath, Format.Tiff);
            }
            if ((ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale)
            {
                PdfKaydet(Scanner.Resimler, pdffilepath, Format.Jpg);
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

        private void ImgViewer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.Image)
            {
                isMouseDown = true;
                Cursor = Cursors.Cross;
                x = e.GetPosition(ImgViewer).X;
                y = e.GetPosition(ImgViewer).Y;
            }
        }

        private void ImgViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (isMouseDown && e.OriginalSource is System.Windows.Controls.Image)
            {
                double curx = e.GetPosition(ImgViewer).X;
                double cury = e.GetPosition(ImgViewer).Y;

                Rectangle r = new()
                {
                    Stroke = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#33FF0000")),
                    Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3300FF00")),
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection(new double[] { 4, 2 }),
                    Width = Math.Abs(curx - x),
                    Height = Math.Abs(cury - y)
                };
                cnv.Children.Clear();
                _ = cnv.Children.Add(r);
                if (x < curx && y < cury)
                {
                    Canvas.SetLeft(r, x);
                    Canvas.SetTop(r, y);
                }
                else
                {
                    Canvas.SetLeft(r, curx);
                    Canvas.SetTop(r, cury);
                }

                if (e.LeftButton == MouseButtonState.Released)
                {
                    cnv.Children.Clear();
                    width = Math.Abs(e.GetPosition(ImgViewer).X - x);
                    height = Math.Abs(e.GetPosition(ImgViewer).Y - y);
                    ImgData = (x < curx && y < cury) ? CaptureScreen(x, y, width, height) : CaptureScreen(curx, cury, width, height);
                    x = y = 0;
                    isMouseDown = false;
                    Cursor = Cursors.Arrow;
                    OnPropertyChanged(nameof(ImgData));
                }
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            Scanner.PdfPassword = ((PasswordBox)sender).SecurePassword;
        }

        private void PdfKaydet(BitmapSource bitmapframe, string dosyayolu, Format format, bool rotate = false)
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
            document.Save(dosyayolu);
        }

        private void PdfKaydet(IList<ScannedImage> bitmapFrames, string dosyayolu, Format format, bool rotate = false)
        {
            using PdfDocument document = new();
            for (int i = 0; i < bitmapFrames.Count; i++)
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
                using MemoryStream ms = new(bitmapFrames[i].Resim.ToTiffJpegByteArray(format));
                using XImage xImage = XImage.FromStream(ms);
                XSize size = PageSizeConverter.ToSize(PageSize.A4);
                gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
            }
            DefaultPdfCompression(document);
            document.Save(dosyayolu);
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
            if (e.PropertyName is "SeperateSave")
            {
                Scanner.AutoSave = CheckAutoSave();
            }
            if (e.PropertyName is "CropLeft" or "CropTop" or "CropRight" or "CropBottom" && Scanner.SeçiliResim != null)
            {
                Int32Rect sourceRect = CropImageRect(Scanner.SeçiliResim.Resim);
                if (sourceRect.HasArea)
                {
                    Scanner.CroppedImage = new CroppedBitmap(Scanner.SeçiliResim.Resim, sourceRect);
                    Scanner.CroppedImage.Freeze();
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
            if (e.PropertyName is "SelectedProfile" && Scanner.SelectedProfile is not null)
            {
                string[] selectedprofile = Scanner.SelectedProfile.Split('|');
                Settings.Default.Çözünürlük = double.Parse(selectedprofile[1]);
                Settings.Default.Adf = bool.Parse(selectedprofile[2]);
                Settings.Default.Mode = int.Parse(selectedprofile[3]);
                Scanner.Duplex = bool.Parse(selectedprofile[4]);
                Scanner.ShowUi = bool.Parse(selectedprofile[5]);
                Scanner.SeperateSave = bool.Parse(selectedprofile[6]);
                Settings.Default.ShowFile = bool.Parse(selectedprofile[7]);
                Settings.Default.DateGroupFolder = true;//bool.Parse(selectedprofile[8])
                Scanner.FileName = selectedprofile[9];
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                twain = new Twain(new WindowMessageHook(Window.GetWindow(Parent)));
                Scanner.Tarayıcılar = twain.SourceNames;
                if (twain.SourceNames?.Count > 0)
                {
                    Scanner.SeçiliTarayıcı = twain.SourceNames[0];
                }

                twain.TransferImage += (s, args) =>
                {
                    if (args.Image != null)
                    {
                        using Bitmap bitmap = args.Image;
                        BitmapSource evrak = EvrakOluştur(bitmap);
                        evrak.Freeze();
                        BitmapSource önizleme = evrak.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / 21 * 29.7);
                        önizleme.Freeze();
                        BitmapFrame bitmapFrame = BitmapFrame.Create(evrak, önizleme);
                        bitmapFrame.Freeze();
                        Scanner.Resimler.Add(new ScannedImage() { Resim = bitmapFrame });
                        if (Scanner.SeperateSave && (ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
                        {
                            File.WriteAllBytes(GetSaveFolder().SetUniqueFile(Scanner.SaveFileName, "tif"), bitmapFrame.ToTiffJpegByteArray(Format.Tiff));
                            OnPropertyChanged(nameof(Scanner.Tarandı));
                        }

                        if (Scanner.SeperateSave && ((ColourSetting)Settings.Default.Mode == ColourSetting.GreyScale || (ColourSetting)Settings.Default.Mode == ColourSetting.Colour))
                        {
                            File.WriteAllBytes(GetSaveFolder().SetUniqueFile(Scanner.SaveFileName, "jpg"), bitmapFrame.ToTiffJpegByteArray(Format.Jpg));
                            OnPropertyChanged(nameof(Scanner.Tarandı));
                        }

                        evrak = null;
                        bitmapFrame = null;
                        önizleme = null;
                    }
                };
                twain.ScanningComplete += delegate
                {
                    Scanner.ArayüzEtkin = true;
                };
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