using Extensions;
using Microsoft.Win32;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
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
using System.Runtime.InteropServices;
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
            }, parameter => !Environment.Is64BitProcess && Scanner.AutoSave);

            ResimSil = new RelayCommand<object>(parameter => Scanner.Resimler?.Remove(parameter as ScannedImage), parameter => true);

            ExploreFile = new RelayCommand<object>(parameter => OpenFolderAndSelectItem(Path.GetDirectoryName(parameter as string), Path.GetFileName(parameter as string)), parameter => true);

            Kaydet = new RelayCommand<object>(parameter =>
            {
                if (parameter is ScannedImage scannedImage)
                {
                    SaveFileDialog saveFileDialog = new()
                    {
                        Filter = "Tif Resmi (*.tif)|*.tif|Jpg Resmi(*.jpg)|*.jpg|Pdf Dosyası(*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası(*.pdf)|*.pdf",
                        FileName = Scanner.FileName
                    };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        if (saveFileDialog.FilterIndex == 1)
                        {
                            if ((ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
                            {
                                File.WriteAllBytes(saveFileDialog.FileName, scannedImage.Resim.ToTiffJpegByteArray(Format.Tiff));
                            }
                            else if ((ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale)
                            {
                                File.WriteAllBytes(saveFileDialog.FileName, scannedImage.Resim.ToTiffJpegByteArray(Format.TiffRenkli));
                            }
                        }
                        else if (saveFileDialog.FilterIndex == 2)
                        {
                            File.WriteAllBytes(saveFileDialog.FileName, scannedImage.Resim.ToTiffJpegByteArray(Format.Jpg));
                        }
                        else if (saveFileDialog.FilterIndex == 3)
                        {
                            if (Viewer is { Angle: not 0, Angle: not 360 })
                            {
                                if (MessageBox.Show("Döndürme Uygulanarak Kaydedilsin mi?", Application.Current?.MainWindow?.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                                {
                                    PdfKaydet(scannedImage.Resim, saveFileDialog.FileName, Format.Jpg, true);
                                    return;
                                }
                                PdfKaydet(scannedImage.Resim, saveFileDialog.FileName, Format.Jpg);
                            }
                            PdfKaydet(scannedImage.Resim, saveFileDialog.FileName, Format.Jpg);
                        }
                        else if (saveFileDialog.FilterIndex == 4)
                        {
                            if (Viewer is { Angle: not 0, Angle: not 360 })
                            {
                                if (MessageBox.Show("Döndürme Uygulanarak Kaydedilsin mi?", Application.Current?.MainWindow?.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                                {
                                    PdfKaydet(scannedImage.Resim, saveFileDialog.FileName, Format.Tiff, true);
                                    return;
                                }
                                PdfKaydet(scannedImage.Resim, saveFileDialog.FileName, Format.Tiff);
                            }
                            PdfKaydet(scannedImage.Resim, saveFileDialog.FileName, Format.Tiff);
                        }
                    }
                }
            }, parameter => true);

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
                    FileName = Scanner.FileName
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    if (saveFileDialog.FilterIndex == 1)
                    {
                        PdfKaydet(Scanner.Resimler.Where(z => z.Seçili).ToArray(), saveFileDialog.FileName, Format.Jpg);
                    }
                    if (saveFileDialog.FilterIndex == 2)
                    {
                        PdfKaydet(Scanner.Resimler.Where(z => z.Seçili).ToArray(), saveFileDialog.FileName, Format.Tiff);
                    }
                    if (saveFileDialog.FilterIndex == 3)
                    {
                        string dosyayolu = $"{Path.GetTempPath()}{Guid.NewGuid()}.pdf";
                        PdfKaydet(Scanner.Resimler.Where(z => z.Seçili).ToArray(), dosyayolu, Format.Jpg);
                        using ZipArchive archive = ZipFile.Open(saveFileDialog.FileName, ZipArchiveMode.Create);
                        _ = archive.CreateEntryFromFile(dosyayolu, $"{Scanner.FileName}.pdf", CompressionLevel.Optimal);
                        File.Delete(dosyayolu);
                    }
                }
            }, parameter =>
            {
                Scanner.SeçiliResimSayısı = Scanner.Resimler.Count(z => z.Seçili);
                return Scanner.SeçiliResimSayısı > 0;
            });

            ListeTemizle = new RelayCommand<object>(parameter =>
            {
                if (MessageBox.Show("Tüm Taranan Evrak Silinecek Devam Etmek İstiyor Musun?", Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    Scanner.Resimler?.Clear();
                }
            }, parameter => Scanner.Resimler?.Count > 0);

            SaveProfile = new RelayCommand<object>(parameter =>
            {
                StringBuilder sb = new();
                string profile = sb
                    .Append(Scanner.ProfileName)
                    .Append(",")
                    .Append(Settings.Default.Çözünürlük)
                    .Append(",")
                    .Append(Settings.Default.Adf)
                    .Append(",")
                    .Append(Settings.Default.Mode)
                    .Append(",")
                    .Append(Scanner.Duplex)
                    .Append(",")
                    .Append(Scanner.ShowUi)
                    .Append(",")
                    .Append(Scanner.SeperateSave)
                    .Append(",")
                    .Append(Settings.Default.ShowFile)
                    .Append(",")
                    .Append(Settings.Default.DateGroupFolder)
                    .Append(",")
                    .Append(Scanner.FileName)
                    .ToString();
                _ = Settings.Default.Profile.Add(profile);
                Settings.Default.Save();
                Settings.Default.Reload();
            }, parameter => !string.IsNullOrWhiteSpace(Scanner?.ProfileName));

            SaveCroppedImage = new RelayCommand<object>(parameter =>
            {
                SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Pdf Dosyası(*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası(*.pdf)|*.pdf|Jpg Dosyası(*.jpg)|*.jpg",
                    FileName = Scanner.FileName
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    if (saveFileDialog.FilterIndex == 1)
                    {
                        PdfKaydet((BitmapSource)Scanner.CroppedImage, saveFileDialog.FileName, Format.Jpg);
                    }
                    if (saveFileDialog.FilterIndex == 2)
                    {
                        PdfKaydet((BitmapSource)Scanner.CroppedImage, saveFileDialog.FileName, Format.Tiff);
                    }
                    if (saveFileDialog.FilterIndex == 3)
                    {
                        File.WriteAllBytes(saveFileDialog.FileName, Scanner.CroppedImage.ToTiffJpegByteArray(Format.Jpg));
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

        public ICommand ExploreFile { get; }

        public ICommand FastScanImage { get; }

        public ICommand Kaydet { get; }

        public ICommand KayıtYoluBelirle { get; }

        public ICommand ListeTemizle { get; }

        public ICommand PdfBirleştir { get; }

        public ICommand ResetCroppedImage { get; }

        public ICommand ResimSil { get; }

        public ICommand SaveCroppedImage { get; }

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

        public ICommand SplitImage { get; }

        public ICommand Tersiniİşaretle { get; }

        public ICommand Tümünüİşaretle { get; }

        public ICommand TümününİşaretiniKaldır { get; }

        public ICommand WebAdreseGit { get; }

        public static string GetDisplayName(string path)
        {
            _ = new SHFILEINFO();
            return (SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, out SHFILEINFO shfi, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), SHGFI_DISPLAYNAME) != 0) ? shfi.szDisplayName : null;
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

        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

        private const uint SHGFI_DISPLAYNAME = 0x000000200;

        private ScanSettings _settings;

        private bool disposedValue;

        private Scanner scanner;

        private Twain twain;

        [DllImport("shell32")]
        private static extern int SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint flags);

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
            twain.ScanningComplete -= Fastscan;
            OnPropertyChanged(nameof(Scanner.Tarandı));
        }

        private string GetPdfScanPath()
        {
            string today = DateTime.Today.ToShortDateString();
            if (Settings.Default.DateGroupFolder)
            {
                string datefolder = $@"{Settings.Default.AutoFolder}\{today}";
                if (!Directory.Exists(datefolder))
                {
                    _ = Directory.CreateDirectory(datefolder);
                }
                return datefolder.SetUniqueFile($"{today}{Scanner.FileName}", "pdf");
            }
            return Settings.Default.AutoFolder.SetUniqueFile($"{today}{Scanner.FileName}", "pdf");
        }

        private void PdfKaydet(BitmapSource bitmapframe, string dosyayolu, Format format, bool rotate = false)
        {
            using PdfDocument document = new();
            PdfPage page = document.AddPage();
            if (rotate)
            {
                page.Rotate = (int)Viewer.Angle;
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
                    page.Rotate = (int)Viewer.Angle;
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
                if (Scanner.SeperateSave && Scanner.AutoSave)
                {
                    Scanner.AutoSave = true;
                }
                if (Scanner.SeperateSave && !Scanner.AutoSave)
                {
                    Scanner.AutoSave = false;
                }
            }
            if (e.PropertyName is "SeçiliResim" or "CropLeft" or "CropTop" or "CropRight" or "CropBottom" && Scanner.SeçiliResim != null)
            {
                Int32Rect sourceRect = CropImageRect(Scanner.SeçiliResim.Resim);
                if (sourceRect.HasArea)
                {
                    Scanner.CroppedImage = new CroppedBitmap(Scanner.SeçiliResim.Resim, sourceRect);
                    Scanner.CroppedImage.Freeze();
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
            if (e.PropertyName is "SelectedProfile")
            {
                string[] selectedprofile = Scanner.SelectedProfile.Split(',');
                Settings.Default.Çözünürlük = double.Parse(selectedprofile[1]);
                Settings.Default.Adf = bool.Parse(selectedprofile[2]);
                Settings.Default.Mode = int.Parse(selectedprofile[3]);
                Scanner.Duplex = bool.Parse(selectedprofile[4]);
                Scanner.ShowUi = bool.Parse(selectedprofile[5]);
                Scanner.SeperateSave = bool.Parse(selectedprofile[6]);
                Settings.Default.ShowFile = bool.Parse(selectedprofile[7]);
                Scanner.FileName = selectedprofile[8];
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
                            File.WriteAllBytes(Settings.Default.AutoFolder.SetUniqueFile($"{DateTime.Now.ToShortDateString()}{Scanner.FileName}", "tif"), bitmapFrame.ToTiffJpegByteArray(Format.Tiff));
                            OnPropertyChanged(nameof(Scanner.Tarandı));
                        }

                        if (Scanner.SeperateSave && ((ColourSetting)Settings.Default.Mode == ColourSetting.GreyScale || (ColourSetting)Settings.Default.Mode == ColourSetting.Colour))
                        {
                            File.WriteAllBytes(Settings.Default.AutoFolder.SetUniqueFile($"{DateTime.Now.ToShortDateString()}{Scanner.FileName}", "jpg"), bitmapFrame.ToTiffJpegByteArray(Format.Jpg));
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

        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;

            public IntPtr iIcon;

            public uint dwAttributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };
    }
}