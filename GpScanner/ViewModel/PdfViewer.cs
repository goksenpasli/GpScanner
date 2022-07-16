using Extensions;
using Freeware;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TwainControl;
using TwainControl.Properties;

namespace GpScanner.ViewModel
{
    public class PdfViewer : ImageViewer, IDisposable
    {
        public static readonly DependencyProperty DpiProperty = DependencyProperty.Register("Dpi", typeof(int), typeof(PdfViewer), new PropertyMetadata(150, DpiChanged));

        public static readonly DependencyProperty PdfFilePathProperty = DependencyProperty.Register("PdfFilePath", typeof(string), typeof(PdfViewer), new PropertyMetadata(null, PdfFilePathChanged));

        public static readonly DependencyProperty PdfFileStreamProperty = DependencyProperty.Register("PdfFileStream", typeof(byte[]), typeof(PdfViewer), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.NotDataBindable, async (o, e) => await PdfStreamChangedAsync(o, e)));

        public static new readonly DependencyProperty ZoomProperty = DependencyProperty.Register("Zoom", typeof(double), typeof(PdfViewer), new PropertyMetadata(1.0));

        public PdfViewer()
        {
            DosyaAç = new RelayCommand<object>(parameter =>
            {
                OpenFileDialog openFileDialog = new() { Multiselect = false, Filter = "Pdf Dosyaları (*.pdf)|*.pdf" };
                if (openFileDialog.ShowDialog() == true)
                {
                    PdfFilePath = openFileDialog.FileName;
                }
            });

            ViewerBack = new RelayCommand<object>(parameter =>
            {
                Sayfa--;
                Source = BitmapSourceFromByteArray(Pdf2Png.Convert(PdfFileStream, Sayfa, Dpi));
            }, parameter => Source is not null && Sayfa > 1 && Sayfa <= ToplamSayfa);

            ViewerNext = new RelayCommand<object>(parameter =>
            {
                Sayfa++;
                Source = BitmapSourceFromByteArray(Pdf2Png.Convert(PdfFileStream, Sayfa, Dpi));
            }, parameter => Source is not null && Sayfa >= 1 && Sayfa < ToplamSayfa);

            SaveImage = new RelayCommand<object>(parameter =>
            {
                SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Jpg Dosyası(*.jpg)|*.jpg",
                    FileName = "Resim"
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllBytes(saveFileDialog.FileName, Source.ToTiffJpegByteArray(ExtensionMethods.Format.Jpg));
                }
            }, parameter => Source is not null);

            TransferImage = new RelayCommand<object>(parameter =>
            {
                if (parameter is MainWindow mainWindow)
                {
                    BitmapSource thumbnail = ((BitmapSource)Source).Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / 21 * 29.7);
                    ScannedImage scannedImage = new() { Seçili = true, Resim = BitmapFrame.Create((BitmapSource)Source, thumbnail) };
                    mainWindow.TwainCtrl.Scanner.Resimler.Add(scannedImage);
                }
            }, parameter => Source is not null);

            Resize = new RelayCommand<object>(delegate
            {
                Zoom = (FitImageOrientation != 0) ? (double.IsNaN(Height) ? ((ActualHeight == 0.0) ? 1.0 : (ActualHeight / Source.Height)) : ((Height == 0.0) ? 1.0 : (Height / Source.Height))) : (double.IsNaN(Width) ? ((ActualWidth == 0.0) ? 1.0 : (ActualWidth / Source.Width)) : ((Width == 0.0) ? 1.0 : (Width / Source.Width)));
            }, (object parameter) => Source != null);

            OrijinalPdfDosyaAç = new RelayCommand<object>(parameter => _ = Process.Start(parameter as string), parameter => !DesignerProperties.GetIsInDesignMode(new DependencyObject()) && File.Exists(parameter as string));

            PropertyChanged += PdfViewer_PropertyChanged;
        }

        public new ICommand DosyaAç { get; }

        public int Dpi
        {
            get => (int)GetValue(DpiProperty);
            set => SetValue(DpiProperty, value);
        }

        public int[] DpiList { get; } = new int[] { 96, 150, 225, 300, 600 };

        public bool FirstPageThumbnail
        {
            get => firstPageThumbnail;

            set
            {
                if (firstPageThumbnail != value)
                {
                    firstPageThumbnail = value;
                    OnPropertyChanged(nameof(FirstPageThumbnail));
                }
            }
        }

        public ICommand OrijinalPdfDosyaAç { get; }

        public string PdfFilePath
        {
            get => (string)GetValue(PdfFilePathProperty);
            set => SetValue(PdfFilePathProperty, value);
        }

        public byte[] PdfFileStream
        {
            get => (byte[])GetValue(PdfFileStreamProperty);
            set => SetValue(PdfFileStreamProperty, value);
        }

        public new ICommand Resize { get; }

        public ICommand SaveImage { get; }

        public int ToplamSayfa
        {
            get => toplamSayfa;

            set
            {
                if (toplamSayfa != value)
                {
                    toplamSayfa = value;
                    OnPropertyChanged(nameof(ToplamSayfa));
                }
            }
        }

        public ICommand TransferImage { get; }

        public new ICommand ViewerBack { get; }

        public new ICommand ViewerNext { get; }

        public new double Zoom
        {
            get => (double)GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }

        public static BitmapImage BitmapSourceFromByteArray(byte[] buffer, bool fasterimage = false)
        {
            if (buffer != null)
            {
                BitmapImage bitmap = new();
                using MemoryStream stream = new(buffer);
                bitmap.BeginInit();
                if (fasterimage)
                {
                    bitmap.DecodePixelWidth = 72;
                }
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            return null;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    PdfFileStream = null;
                }
                disposedValue = true;
            }
        }

        private bool disposedValue;

        private bool firstPageThumbnail;

        private int toplamSayfa;

        private static void DpiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfViewer pdfViewer && pdfViewer.PdfFileStream is not null)
            {
                pdfViewer.Source = BitmapSourceFromByteArray(Pdf2Png.Convert(pdfViewer.PdfFileStream, pdfViewer.Sayfa, (int)e.NewValue));
            }
        }

        private static void PdfFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfViewer pdfViewer && File.Exists(e.NewValue as string) && string.Equals(Path.GetExtension(e.NewValue as string), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                if (e.NewValue is not null)
                {
                    pdfViewer.PdfFileStream = File.ReadAllBytes(e.NewValue as string);
                    pdfViewer.Sayfa = 1;
                }
                else
                {
                    pdfViewer.Source = null;
                }
            }
        }

        private static async Task PdfStreamChangedAsync(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfViewer pdfViewer)
            {
                try
                {
                    byte[] pdfdata = e.NewValue as byte[];
                    int sayfa = pdfViewer.Sayfa;
                    int dpi = pdfViewer.Dpi;
                    if (pdfViewer.FirstPageThumbnail)
                    {
                        pdfViewer.Source = await Task.Run(() => BitmapSourceFromByteArray(Pdf2Png.Convert(pdfdata, 1, 108), true));
                    }
                    else
                    {
                        pdfViewer.ToplamSayfa = Pdf2Png.ConvertAllPages(pdfdata, 0).Count;
                        pdfViewer.Source = await Task.Run(() => BitmapSourceFromByteArray(Pdf2Png.Convert(pdfdata, sayfa, dpi)));
                        pdfViewer.TifNavigasyonButtonEtkin = pdfViewer.ToplamSayfa > 1 ? Visibility.Visible : Visibility.Collapsed;
                        pdfViewer.Pages = Enumerable.Range(1, pdfViewer.ToplamSayfa);
                    }
                    pdfdata = null;
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message);
                }
            }
        }

        private void PdfViewer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "Sayfa" && sender is PdfViewer pdfViewer && pdfViewer.PdfFileStream is not null)
            {
                Source = BitmapSourceFromByteArray(Pdf2Png.Convert(pdfViewer.PdfFileStream, Sayfa, pdfViewer.Dpi));
            }
        }
    }
}