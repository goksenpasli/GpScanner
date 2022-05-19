using Extensions;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections;
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

        private bool arayüzetkin = true;

        private bool autoRotate;

        private bool autoSave = Directory.Exists(Settings.Default.AutoFolder);

        private bool borderDetect;

        private int boyAdet = 1;

        private bool? bw = false;

        private double cropBottom;

        private double cropLeft;

        private ImageSource croppedImage;

        private double cropRight;

        private double cropTop;

        private bool deskew;

        private bool disposedValue;

        private bool duplex;

        private int enAdet = 1;

        private int eşik = 160;

        private ObservableCollection<BitmapFrame> resimler = new();

        private ImageSource seçiliResim;

        private IList seçiliresimler = new ObservableCollection<BitmapFrame>();

        private string seçiliTarayıcı;

        private bool seperateSave;

        private bool showProgress;

        private bool showUi;

        private bool tarandı;

        private IList<string> tarayıcılar;

        private Twain twain;

        public TwainCtrl()
        {
            InitializeComponent();
            DataContext = this;

            ScanImage = new RelayCommand<object>(parameter =>
            {
                ArayüzEtkin = false;
                _settings = DefaultScanSettings();
                _settings.Resolution.ColourSetting = Bw ?? false ? ColourSetting.BlackAndWhite : ColourSetting.Colour;
                _settings.Resolution.Dpi = (int)Settings.Default.Çözünürlük;
                _settings.Rotation = new RotationSettings { AutomaticDeskew = Deskew, AutomaticRotate = AutoRotate, AutomaticBorderDetection = BorderDetect };
                if (Tarayıcılar.Count > 0)
                {
                    twain.SelectSource(SeçiliTarayıcı);
                    twain.StartScanning(_settings);
                }
            }, parameter => !Environment.Is64BitProcess);

            FastScanImage = new RelayCommand<object>(parameter =>
            {
                ArayüzEtkin = false;
                _settings = DefaultScanSettings();
                _settings.Resolution.ColourSetting = Settings.Default.DefaultScanFormat is 2
                    ? ColourSetting.BlackAndWhite
                    : ColourSetting.Colour;
                _settings.Resolution.Dpi = (int)Settings.Default.Çözünürlük;
                _settings.Rotation = new RotationSettings { AutomaticDeskew = Deskew, AutomaticRotate = AutoRotate, AutomaticBorderDetection = BorderDetect };
                if (Tarayıcılar.Count > 0)
                {
                    Resimler = new ObservableCollection<BitmapFrame>();
                    twain.SelectSource(SeçiliTarayıcı);
                    twain.StartScanning(_settings);
                }
                twain.ScanningComplete += Fastscan;
            }, parameter => !Environment.Is64BitProcess && Settings.Default.DefaultScanFormat != -1 && AutoSave);

            ResimSil = new RelayCommand<object>(parameter => Resimler?.Remove(parameter as BitmapFrame), parameter => true);

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
                                switch (Bw)
                                {
                                    case true:
                                        File.WriteAllBytes(saveFileDialog.FileName, resim.ToTiffJpegByteArray(Format.Tiff));
                                        break;

                                    case null:
                                    case false:
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
                        PdfKaydet(Resimler, saveFileDialog.FileName, Format.Tiff);
                    }
                    else
                    {
                        PdfKaydet(Resimler, saveFileDialog.FileName, Format.Jpg);
                    }
                }
            }, parameter => Resimler?.Count > 0);

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
                        PdfKaydet(SeçiliResimler as IList<BitmapFrame>, saveFileDialog.FileName, Format.Tiff);
                    }
                    else
                    {
                        PdfKaydet(SeçiliResimler as IList<BitmapFrame>, saveFileDialog.FileName, Format.Jpg);
                    }
                }
            }, parameter => SeçiliResimler?.Count > 0);

            ListeTemizle = new RelayCommand<object>(parameter =>
            {
                if (MessageBox.Show("Tüm Taranan Evrak Silinecek Devam Etmek İstiyor Musun?", Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    Resimler?.Clear();
                }
            }, parameter => Resimler?.Count > 0);

            SaveCroppedImage = new RelayCommand<object>(parameter =>
            {
                Microsoft.Win32.SaveFileDialog saveFileDialog = new() { Filter = "Pdf Dosyası(*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası(*.pdf)|*.pdf|Jpg Dosyası(*.jpg)|*.jpg" };
                if (saveFileDialog.ShowDialog() == true)
                {
                    if (saveFileDialog.FilterIndex == 2)
                    {
                        PdfKaydet(CroppedImage, saveFileDialog.FileName, Format.Tiff);
                    }
                    if (saveFileDialog.FilterIndex == 1)
                    {
                        PdfKaydet(CroppedImage, saveFileDialog.FileName, Format.Jpg);
                    }
                    if (saveFileDialog.FilterIndex == 3)
                    {
                        File.WriteAllBytes(saveFileDialog.FileName, CroppedImage.ToTiffJpegByteArray(Format.Jpg));
                    }
                }
            }, parameter => CroppedImage is not null);

            SplitImage = new RelayCommand<object>(parameter =>
            {
                BitmapSource image = (BitmapSource)CroppedImage;
                for (int i = 0; i < EnAdet; i++)
                {
                    for (int j = 0; j < BoyAdet; j++)
                    {
                        int x = i * image.PixelWidth / EnAdet;
                        int y = j * image.PixelHeight / BoyAdet;
                        int width = image.PixelWidth / EnAdet;
                        int height = image.PixelHeight / BoyAdet;
                        Int32Rect sourceRect = new(x, y, width, height);
                        if (sourceRect.HasArea)
                        {
                            CroppedBitmap croppedBitmap = new(image, sourceRect);
                            File.WriteAllBytes(Settings.Default.AutoFolder.SetUniqueFile($"{DateTime.Now.ToShortDateString()}Parçalanmış", "jpg"), croppedBitmap.ToTiffJpegByteArray(Format.Jpg));
                        }
                    }
                }
            }, parameter => CroppedImage is not null && EnAdet > 0 && BoyAdet > 0);

            ResetCroppedImage = new RelayCommand<object>(parameter => ResetCropMargin(), parameter => CroppedImage is not null);

            WebAdreseGit = new RelayCommand<object>(parameter => Process.Start(parameter as string), parameter => true);

            PropertyChanged += TwainCtrl_PropertyChanged;
            Settings.Default.PropertyChanged += Default_PropertyChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public bool ArayüzEtkin
        {
            get => arayüzetkin;

            set
            {
                if (arayüzetkin != value)
                {
                    arayüzetkin = value;
                    OnPropertyChanged(nameof(ArayüzEtkin));
                }
            }
        }

        public bool AutoRotate
        {
            get => autoRotate;

            set
            {
                if (autoRotate != value)
                {
                    autoRotate = value;
                    OnPropertyChanged(nameof(AutoRotate));
                }
            }
        }

        public bool AutoSave
        {
            get => autoSave;

            set
            {
                if (autoSave != value)
                {
                    autoSave = value;
                    OnPropertyChanged(nameof(AutoSave));
                }
            }
        }

        public bool BorderDetect
        {
            get => borderDetect;

            set
            {
                if (borderDetect != value)
                {
                    borderDetect = value;
                    OnPropertyChanged(nameof(BorderDetect));
                }
            }
        }

        public int BoyAdet
        {
            get => boyAdet;

            set

            {
                if (boyAdet != value)
                {
                    boyAdet = value;
                    OnPropertyChanged(nameof(BoyAdet));
                }
            }
        }

        public bool? Bw
        {
            get => bw;

            set
            {
                if (bw != value)
                {
                    bw = value;
                    OnPropertyChanged(nameof(Bw));
                }
            }
        }

        public double CropBottom
        {
            get => cropBottom; set

            {
                if (cropBottom != value)
                {
                    cropBottom = value;
                    OnPropertyChanged(nameof(CropBottom));
                }
            }
        }

        public double CropLeft
        {
            get => cropLeft; set

            {
                if (cropLeft != value)
                {
                    cropLeft = value;
                    OnPropertyChanged(nameof(CropLeft));
                }
            }
        }

        public ImageSource CroppedImage
        {
            get => croppedImage;

            set
            {
                if (croppedImage != value)
                {
                    croppedImage = value;
                    OnPropertyChanged(nameof(CroppedImage));
                }
            }
        }

        public double CropRight
        {
            get => cropRight; set

            {
                if (cropRight != value)
                {
                    cropRight = value;
                    OnPropertyChanged(nameof(CropRight));
                }
            }
        }

        public double CropTop
        {
            get => cropTop;

            set
            {
                if (cropTop != value)
                {
                    cropTop = value;
                    OnPropertyChanged(nameof(CropTop));
                }
            }
        }

        public bool Deskew
        {
            get => deskew;

            set
            {
                if (deskew != value)
                {
                    deskew = value;
                    OnPropertyChanged(nameof(Deskew));
                }
            }
        }

        public bool Duplex
        {
            get => duplex;

            set
            {
                if (duplex != value)
                {
                    duplex = value;
                    OnPropertyChanged(nameof(Duplex));
                }
            }
        }

        public int EnAdet
        {
            get => enAdet;

            set
            {
                if (enAdet != value)
                {
                    enAdet = value;
                    OnPropertyChanged(nameof(EnAdet));
                }
            }
        }

        public int Eşik
        {
            get => eşik;

            set
            {
                if (eşik != value)
                {
                    eşik = value;
                    OnPropertyChanged(nameof(Eşik));
                }
            }
        }

        public ICommand ExploreFile { get; }

        public ICommand FastScanImage { get; }

        public ICommand Kaydet { get; }

        public ICommand KayıtYoluBelirle { get; }

        public ICommand ListeTemizle { get; }

        public ICommand ResetCroppedImage { get; }

        public ObservableCollection<BitmapFrame> Resimler
        {
            get => resimler;

            set
            {
                if (resimler != value)
                {
                    resimler = value;
                    OnPropertyChanged(nameof(Resimler));
                }
            }
        }

        public ICommand ResimSil { get; }

        public ICommand SaveCroppedImage { get; }

        public ICommand ScanImage { get; }

        public ICommand Seçilikaydet { get; }

        public ImageSource SeçiliResim
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

        public IList SeçiliResimler
        {
            get => seçiliresimler;

            set
            {
                if (seçiliresimler != value)
                {
                    seçiliresimler = value;
                    OnPropertyChanged(nameof(SeçiliResimler));
                }
            }
        }

        public string SeçiliTarayıcı
        {
            get => seçiliTarayıcı;

            set
            {
                if (seçiliTarayıcı != value)
                {
                    seçiliTarayıcı = value;
                    OnPropertyChanged(nameof(SeçiliTarayıcı));
                }
            }
        }

        public bool SeperateSave
        {
            get => seperateSave;

            set
            {
                if (seperateSave != value)
                {
                    seperateSave = value;
                    OnPropertyChanged(nameof(SeperateSave));
                }
            }
        }

        public bool ShowProgress
        {
            get => showProgress;

            set
            {
                if (showProgress != value)
                {
                    showProgress = value;
                    OnPropertyChanged(nameof(ShowProgress));
                }
            }
        }

        public bool ShowUi
        {
            get => showUi;

            set
            {
                if (showUi != value)
                {
                    showUi = value;
                    OnPropertyChanged(nameof(ShowUi));
                }
            }
        }

        public ICommand SplitImage { get; }

        public bool Tarandı
        {
            get => tarandı;

            set
            {
                if (tarandı != value)
                {
                    tarandı = value;
                    OnPropertyChanged(nameof(Tarandı));
                }
            }
        }

        public IList<string> Tarayıcılar
        {
            get => tarayıcılar;

            set
            {
                if (tarayıcılar != value)
                {
                    tarayıcılar = value;
                    OnPropertyChanged(nameof(Tarayıcılar));
                }
            }
        }

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
                    Resimler = null;
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
                AutoSave = Directory.Exists(Settings.Default.AutoFolder);
            }
            if (e.PropertyName is "DefaultScanFormat" && Settings.Default.DefaultScanFormat == 0)
            {
                Settings.Default.ShowFile = false;
            }
            Settings.Default.Save();
        }

        private ScanSettings DefaultScanSettings()
        {
            return new()
            {
                UseDocumentFeeder = Settings.Default.Adf,
                ShowTwainUi = ShowUi,
                ShowProgressIndicatorUi = ShowProgress,
                UseDuplex = Duplex,
                ShouldTransferAllPages = true,
                Resolution = new ResolutionSettings()
            };
        }

        private void Fastscan(object sender, ScanningCompleteEventArgs e)
        {
            string pdffile = Settings.Default.AutoFolder.SetUniqueFile($"{DateTime.Now.ToShortDateString()}Tarama", "pdf");
            if (Settings.Default.DefaultScanFormat == 2)
            {
                PdfKaydet(Resimler, pdffile, Format.Tiff);
            }
            if (Settings.Default.DefaultScanFormat == 0)
            {
                File.WriteAllBytes(Settings.Default.AutoFolder.SetUniqueFile($"{DateTime.Now.ToShortDateString()}Tarama", "jpg"), Resimler[Resimler.Count - 1].ToTiffJpegByteArray(Format.Jpg));
            }
            if (Settings.Default.DefaultScanFormat == 1)
            {
                PdfKaydet(Resimler, pdffile, Format.Jpg);
            }
            if (Settings.Default.ShowFile)
            {
                ExploreFile.Execute(pdffile);
            }
            twain.ScanningComplete -= Fastscan;
            OnPropertyChanged(nameof(Tarandı));
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
            CropBottom = 0;
            CropLeft = 0;
            CropTop = 0;
            CropRight = 0;
            EnAdet = 1;
            BoyAdet = 1;
        }

        private void TwainCtrl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "SeperateSave")
            {
                if (SeperateSave && AutoSave)
                {
                    AutoSave = true;
                }
                if (SeperateSave && !AutoSave)
                {
                    AutoSave = false;
                }
            }
            if (e.PropertyName is "SeçiliResim" or "CropLeft" or "CropTop" or "CropRight" or "CropBottom" && SeçiliResim != null)
            {
                int height = Math.Abs(((BitmapSource)SeçiliResim).PixelHeight - (int)CropBottom - (int)CropTop);
                int width = Math.Abs(((BitmapSource)SeçiliResim).PixelWidth - (int)CropRight - (int)CropLeft);
                Int32Rect sourceRect = new((int)CropLeft, (int)CropTop, width, height);
                if (sourceRect.HasArea)
                {
                    CroppedImage = new CroppedBitmap((BitmapSource)SeçiliResim, sourceRect);
                    CroppedImage.Freeze();
                }
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                twain = new Twain(new WindowMessageHook(Window.GetWindow(Parent)));
                Tarayıcılar = twain.SourceNames;
                if (twain.SourceNames?.Count > 0)
                {
                    SeçiliTarayıcı = twain.SourceNames[0];
                }

                twain.TransferImage += (s, args) =>
                {
                    if (args.Image != null)
                    {
                        using System.Drawing.Bitmap bmp = args.Image;
                        BitmapSource evrak = null;
                        const float mmpi = 25.4f;
                        double dpi = Settings.Default.Çözünürlük;
                        switch (Bw)
                        {
                            case true:
                                evrak = bmp.ConvertBlackAndWhite(Eşik).ToBitmapImage(ImageFormat.Tiff, SystemParameters.PrimaryScreenHeight);
                                break;

                            case null:
                                evrak = bmp.ConvertBlackAndWhite(Eşik, true).ToBitmapImage(ImageFormat.Jpeg, SystemParameters.PrimaryScreenHeight).Resize(210 / mmpi * dpi, 297 / mmpi * dpi);
                                break;

                            case false:
                                evrak = bmp.ToBitmapImage(ImageFormat.Jpeg, SystemParameters.PrimaryScreenHeight).Resize(210 / mmpi * dpi, 297 / mmpi * dpi);
                                break;
                        }

                        evrak.Freeze();
                        BitmapSource önizleme = evrak.Resize(63, 89);
                        önizleme.Freeze();
                        BitmapFrame bitmapFrame = BitmapFrame.Create(evrak, önizleme);
                        bitmapFrame.Freeze();
                        Resimler.Add(bitmapFrame);
                        if (SeperateSave && Bw == true)
                        {
                            File.WriteAllBytes(Settings.Default.AutoFolder.SetUniqueFile($"{DateTime.Now.ToShortDateString()}Tarama", "tif"), bitmapFrame.ToTiffJpegByteArray(Format.Tiff));
                            OnPropertyChanged(nameof(Tarandı));
                        }

                        if (SeperateSave && (Bw == false || Bw == null))
                        {
                            File.WriteAllBytes(Settings.Default.AutoFolder.SetUniqueFile($"{DateTime.Now.ToShortDateString()}Tarama", "jpg"), bitmapFrame.ToTiffJpegByteArray(Format.Jpg));
                            OnPropertyChanged(nameof(Tarandı));
                        }

                        evrak = null;
                        bitmapFrame = null;
                        önizleme = null;
                    }
                };
                twain.ScanningComplete += delegate
                {
                    ArayüzEtkin = true;
                };
            }
            catch (Exception)
            {
                ArayüzEtkin = false;
            }
        }
    }
}