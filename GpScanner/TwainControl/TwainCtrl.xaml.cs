using Extensions;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TwainControl.Properties;
using TwainWpf;
using TwainWpf.Wpf;
using static Extensions.ExtensionMethods;

namespace TwainControl
{
    public partial class TwainCtrl : System.Windows.Controls.UserControl, INotifyPropertyChanged, IDisposable
    {
        private ScanSettings _settings;

        private bool disposedValue;

        private Scanner scanner;

        private Twain twain;

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
                    Scanner.Resimler = new ObservableCollection<BitmapFrame>();
                    twain.SelectSource(Scanner.SeçiliTarayıcı);
                    twain.StartScanning(_settings);
                }
                twain.ScanningComplete += Fastscan;
            }, parameter => !Environment.Is64BitProcess && Scanner.AutoSave);

            ResimSil = new RelayCommand<object>(parameter => Scanner.Resimler?.Remove(parameter as BitmapFrame), parameter => true);

            ExploreFile = new RelayCommand<object>(parameter => OpenFolderAndSelectItem(Path.GetDirectoryName(parameter as string), Path.GetFileName(parameter as string)), parameter => true);

            Kaydet = new RelayCommand<object>(parameter =>
            {
                if (parameter is BitmapFrame resim)
                {
                    Microsoft.Win32.SaveFileDialog saveFileDialog = new() { Filter = "Tif Resmi (*.tif)|*.tif|Jpg Resmi(*.jpg)|*.jpg|Pdf Dosyası(*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası(*.pdf)|*.pdf" };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        switch (saveFileDialog.FilterIndex)
                        {
                            case 1:
                                switch ((ColourSetting)Settings.Default.Mode)
                                {
                                    case ColourSetting.BlackAndWhite:
                                        File.WriteAllBytes(saveFileDialog.FileName, resim.ToTiffJpegByteArray(Format.Tiff));
                                        break;

                                    case ColourSetting.Colour:
                                    case ColourSetting.GreyScale:
                                        File.WriteAllBytes(saveFileDialog.FileName, resim.ToTiffJpegByteArray(Format.TiffRenkli));
                                        break;
                                }
                                break;

                            case 2:
                                File.WriteAllBytes(saveFileDialog.FileName, resim.ToTiffJpegByteArray(Format.Jpg));
                                break;

                            case 3:
                                PdfKaydet(resim, saveFileDialog.FileName, Format.Jpg);
                                break;

                            case 4:
                                PdfKaydet(resim, saveFileDialog.FileName, Format.Tiff);
                                break;
                        }
                    }
                }
            }, parameter => true);

            Tümünükaydet = new RelayCommand<object>(parameter =>
            {
                Microsoft.Win32.SaveFileDialog saveFileDialog = new() { Filter = "Pdf Dosyası(*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası(*.pdf)|*.pdf" };
                if (saveFileDialog.ShowDialog() == true)
                {
                    if (saveFileDialog.FilterIndex == 2)
                    {
                        PdfKaydet(Scanner.Resimler, saveFileDialog.FileName, Format.Tiff);
                    }
                    else
                    {
                        PdfKaydet(Scanner.Resimler, saveFileDialog.FileName, Format.Jpg);
                    }
                }
            }, parameter => Scanner.Resimler?.Count > 0);

            KayıtYoluBelirle = new RelayCommand<object>(parameter =>
            {
                System.Windows.Forms.FolderBrowserDialog dialog = new()
                {
                    Description = "Otomatik Kayıt Klasörünü Belirtin."
                };
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    Settings.Default.AutoFolder = dialog.SelectedPath;
                }
            }, parameter => true);

            Seçilikaydet = new RelayCommand<object>(parameter =>
            {
                Microsoft.Win32.SaveFileDialog saveFileDialog = new() { Filter = "Pdf Dosyası(*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası(*.pdf)|*.pdf" };
                if (saveFileDialog.ShowDialog() == true)
                {
                    if (saveFileDialog.FilterIndex == 2)
                    {
                        PdfKaydet(Scanner.SeçiliResimler as IList<BitmapFrame>, saveFileDialog.FileName, Format.Tiff);
                    }
                    else
                    {
                        PdfKaydet(Scanner.SeçiliResimler as IList<BitmapFrame>, saveFileDialog.FileName, Format.Jpg);
                    }
                }
            }, parameter => Scanner.SeçiliResimler?.Count > 0);

            ListeTemizle = new RelayCommand<object>(parameter =>
            {
                if (MessageBox.Show("Tüm Taranan Evrak Silinecek Devam Etmek İstiyor Musun?", Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    Scanner.Resimler?.Clear();
                }
            }, parameter => Scanner.Resimler?.Count > 0);

            SaveCroppedImage = new RelayCommand<object>(parameter =>
            {
                Microsoft.Win32.SaveFileDialog saveFileDialog = new() { Filter = "Pdf Dosyası(*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası(*.pdf)|*.pdf|Jpg Dosyası(*.jpg)|*.jpg" };
                if (saveFileDialog.ShowDialog() == true)
                {
                    if (saveFileDialog.FilterIndex == 2)
                    {
                        PdfKaydet(Scanner.CroppedImage, saveFileDialog.FileName, Format.Tiff);
                    }
                    if (saveFileDialog.FilterIndex == 1)
                    {
                        PdfKaydet(Scanner.CroppedImage, saveFileDialog.FileName, Format.Jpg);
                    }
                    if (saveFileDialog.FilterIndex == 3)
                    {
                        File.WriteAllBytes(saveFileDialog.FileName, Scanner.CroppedImage.ToTiffJpegByteArray(Format.Jpg));
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
                WebAdreseGit.Execute(Settings.Default.AutoFolder);
            }, parameter => Scanner.AutoSave && Scanner.CroppedImage is not null && Scanner.EnAdet > 0 && Scanner.BoyAdet > 0);

            ResetCroppedImage = new RelayCommand<object>(parameter => ResetCropMargin(), parameter => Scanner.CroppedImage is not null);

            WebAdreseGit = new RelayCommand<object>(parameter => Process.Start(parameter as string), parameter => true);

            Scanner.PropertyChanged += Scanner_PropertyChanged;

            Settings.Default.PropertyChanged += Default_PropertyChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand ExploreFile { get; }

        public ICommand FastScanImage { get; }

        public ICommand Kaydet { get; }

        public ICommand KayıtYoluBelirle { get; }

        public ICommand ListeTemizle { get; }

        public ICommand ResetCroppedImage { get; }

        public ICommand ResimSil { get; }

        public ICommand SaveCroppedImage { get; }

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

        public ICommand Tümünükaydet { get; }

        public ICommand WebAdreseGit { get; }

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

        private void Fastscan(object sender, ScanningCompleteEventArgs e)
        {
            string pdffile = Settings.Default.AutoFolder.SetUniqueFile($"{DateTime.Now.ToShortDateString()}Tarama", "pdf");
            if ((ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
            {
                PdfKaydet(Scanner.Resimler, pdffile, Format.Tiff);
            }
            if ((ColourSetting)Settings.Default.Mode == ColourSetting.Colour || (ColourSetting)Settings.Default.Mode == ColourSetting.GreyScale)
            {
                PdfKaydet(Scanner.Resimler, pdffile, Format.Jpg);
            }
            if (Settings.Default.ShowFile)
            {
                ExploreFile.Execute(pdffile);
            }
            twain.ScanningComplete -= Fastscan;
            OnPropertyChanged(nameof(Scanner.Tarandı));
        }

        private void PdfKaydet(ImageSource bitmapframe, string dosyayolu, Format format)
        {
            Document doc = new(new Rectangle(((BitmapSource)bitmapframe).PixelWidth, ((BitmapSource)bitmapframe).PixelHeight), 0, 0, 0, 0);
            using FileStream fs = new(dosyayolu, FileMode.Create);
            using PdfWriter writer = PdfWriter.GetInstance(doc, fs);
            writer.SetPdfVersion(PdfWriter.PDF_VERSION_1_5);
            writer.CompressionLevel = PdfStream.BEST_COMPRESSION;
            writer.SetFullCompression();
            doc.Open();
            Image pdfImage = Image.GetInstance(((BitmapSource)bitmapframe).ToTiffJpegByteArray(format));
            _ = doc.Add(pdfImage);
            doc.Close();
            doc.Dispose();
        }

        private void PdfKaydet(IList<BitmapFrame> bitmapFrames, string dosyayolu, Format format)
        {
            Document doc = new(new Rectangle((float)bitmapFrames.FirstOrDefault()?.PixelWidth, (float)bitmapFrames.FirstOrDefault()?.PixelHeight), 0, 0, 0, 0);
            using FileStream fs = new(dosyayolu, FileMode.Create);
            using PdfWriter writer = PdfWriter.GetInstance(doc, fs);
            writer.SetPdfVersion(PdfWriter.PDF_VERSION_1_5);
            writer.CompressionLevel = PdfStream.BEST_COMPRESSION;
            writer.SetFullCompression();
            doc.Open();
            for (int i = 0; i < bitmapFrames.Count; i++)
            {
                _ = doc.Add(Image.GetInstance(bitmapFrames[i].ToTiffJpegByteArray(format)));
            }

            doc.Close();
            doc.Dispose();
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
                int height = Math.Abs(((BitmapSource)Scanner.SeçiliResim).PixelHeight - (int)Scanner.CropBottom - (int)Scanner.CropTop);
                int width = Math.Abs(((BitmapSource)Scanner.SeçiliResim).PixelWidth - (int)Scanner.CropRight - (int)Scanner.CropLeft);
                Int32Rect sourceRect = new((int)Scanner.CropLeft, (int)Scanner.CropTop, width, height);
                if (sourceRect.HasArea)
                {
                    Scanner.CroppedImage = new CroppedBitmap((BitmapSource)Scanner.SeçiliResim, sourceRect);
                    Scanner.CroppedImage.Freeze();
                }
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
                        using System.Drawing.Bitmap bmp = args.Image;
                        BitmapSource evrak = null;
                        const float mmpi = 25.4f;
                        double dpi = Settings.Default.Çözünürlük;
                        switch ((ColourSetting)Settings.Default.Mode)
                        {
                            case ColourSetting.BlackAndWhite:
                                evrak = bmp.ConvertBlackAndWhite(Scanner.Eşik).ToBitmapImage(ImageFormat.Tiff, SystemParameters.PrimaryScreenHeight).Resize(210 / mmpi * dpi, 297 / mmpi * dpi);
                                break;

                            case ColourSetting.GreyScale:
                                evrak = bmp.ConvertBlackAndWhite(Scanner.Eşik, true).ToBitmapImage(ImageFormat.Jpeg, SystemParameters.PrimaryScreenHeight).Resize(210 / mmpi * dpi, 297 / mmpi * dpi);
                                break;

                            case ColourSetting.Colour:
                                evrak = bmp.ToBitmapImage(ImageFormat.Jpeg, SystemParameters.PrimaryScreenHeight).Resize(210 / mmpi * dpi, 297 / mmpi * dpi);
                                break;
                        }

                        evrak.Freeze();
                        BitmapSource önizleme = evrak.Resize(63, 89);
                        önizleme.Freeze();
                        BitmapFrame bitmapFrame = BitmapFrame.Create(evrak, önizleme);
                        bitmapFrame.Freeze();
                        Scanner.Resimler.Add(bitmapFrame);
                        if (Scanner.SeperateSave && (ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
                        {
                            File.WriteAllBytes(Settings.Default.AutoFolder.SetUniqueFile($"{DateTime.Now.ToShortDateString()}Tarama", "tif"), bitmapFrame.ToTiffJpegByteArray(Format.Tiff));
                            OnPropertyChanged(nameof(Scanner.Tarandı));
                        }

                        if (Scanner.SeperateSave && ((ColourSetting)Settings.Default.Mode == ColourSetting.GreyScale || (ColourSetting)Settings.Default.Mode == ColourSetting.Colour))
                        {
                            File.WriteAllBytes(Settings.Default.AutoFolder.SetUniqueFile($"{DateTime.Now.ToShortDateString()}Tarama", "jpg"), bitmapFrame.ToTiffJpegByteArray(Format.Jpg));
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
    }
}