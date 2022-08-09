using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Extensions;
using Freeware;
using Microsoft.Win32;
using TwainControl;
using TwainControl.Properties;
using static Extensions.ExtensionMethods;

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

            ViewerBack = new RelayCommand<object>(parameter => Sayfa--, parameter => Source is not null && Sayfa > 1 && Sayfa <= ToplamSayfa);

            ViewerNext = new RelayCommand<object>(parameter => Sayfa++, parameter => Source is not null && Sayfa >= 1 && Sayfa < ToplamSayfa);

            SaveImage = new RelayCommand<object>(parameter =>
            {
                SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Jpg Dosyası(*.jpg)|*.jpg|Pdf Dosyası(*.pdf)|*.pdf",
                    FileName = "Resim"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    switch (saveFileDialog.FilterIndex)
                    {
                        case 1:
                            {
                                File.WriteAllBytes(saveFileDialog.FileName, Source.ToTiffJpegByteArray(Format.Jpg));
                                return;
                            }

                        case 2:
                            {
                                if (parameter is TwainCtrl twainCtrl)
                                {
                                    twainCtrl.GeneratePdf((BitmapSource)Source, Format.Jpg).Save(saveFileDialog.FileName);
                                }
                                return;
                            }
                    }
                }
            }, parameter => Source is not null);

            TransferImage = new RelayCommand<object>(parameter =>
            {
                if (parameter is Scanner scanner)
                {
                    BitmapSource thumbnail = ((BitmapSource)Source).Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / 21 * 29.7);
                    ScannedImage scannedImage = new() { Seçili = true, Resim = BitmapFrame.Create((BitmapSource)Source, thumbnail) };
                    scanner.Resimler.Add(scannedImage);
                }
            }, parameter => Source is not null);

            Resize = new RelayCommand<object>(delegate
            {
                Zoom = (FitImageOrientation != 0) ? (double.IsNaN(Height) ? ((ActualHeight == 0.0) ? 1.0 : (ActualHeight / Source.Height)) : ((Height == 0.0) ? 1.0 : (Height / Source.Height))) : (double.IsNaN(Width) ? ((ActualWidth == 0.0) ? 1.0 : (ActualWidth / Source.Width)) : ((Width == 0.0) ? 1.0 : (Width / Source.Width)));
            }, (object parameter) => Source != null);

            OrijinalDosyaAç = new RelayCommand<object>(parameter => _ = Process.Start(parameter as string), parameter => !DesignerProperties.GetIsInDesignMode(new DependencyObject()) && File.Exists(parameter as string));

            PropertyChanged += PdfViewer_PropertyChanged;
        }

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

        public ICommand SaveImage { get; }

        public int ThumbnailDpi
        {
            get => thumbnailDpi;

            set
            {
                if (thumbnailDpi != value)
                {
                    thumbnailDpi = value;
                    OnPropertyChanged(nameof(ThumbnailDpi));
                }
            }
        }

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

        public new double Zoom
        {
            get => (double)GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }

        public static BitmapImage BitmapSourceFromByteArray(byte[] buffer, bool fasterimage = false, int thumbdpi = 120)
        {
            if (buffer != null)
            {
                BitmapImage bitmap = new();
                using MemoryStream stream = new(buffer);
                bitmap.BeginInit();
                if (fasterimage)
                {
                    bitmap.DecodePixelWidth = thumbdpi;
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

        private int thumbnailDpi = 120;

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
            if (d is PdfViewer pdfViewer && e.NewValue is byte[] pdfdata && pdfdata.Length > 0)
            {
                try
                {
                    int sayfa = pdfViewer.Sayfa;
                    int dpi = pdfViewer.Dpi;
                    int thumbdpi = pdfViewer.ThumbnailDpi;
                    if (pdfViewer.FirstPageThumbnail)
                    {
                        pdfViewer.Source = await Task.Run(() => BitmapSourceFromByteArray(Pdf2Png.Convert(pdfdata, 1, thumbdpi), true, thumbdpi));
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
                    _ = MessageBox.Show(ex.StackTrace, ex.Message);
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