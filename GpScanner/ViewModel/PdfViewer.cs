using Extensions;
using Freeware;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace GpScanner.ViewModel
{
    public class PdfViewer : ImageViewer, IDisposable
    {
        public static readonly DependencyProperty DpiProperty = DependencyProperty.Register("Dpi", typeof(int), typeof(PdfViewer), new PropertyMetadata(150, DpiChanged));

        public static readonly DependencyProperty PdfFilePathProperty = DependencyProperty.Register("PdfFilePath", typeof(string), typeof(PdfViewer), new PropertyMetadata(null, PdfFilePathChanged));

        public static readonly DependencyProperty PdfFileStreamProperty = DependencyProperty.Register("PdfFileStream", typeof(FileStream), typeof(PdfViewer), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.NotDataBindable, PdfStreamChanged));

        public new static readonly DependencyProperty ZoomProperty = DependencyProperty.Register("Zoom", typeof(double), typeof(PdfViewer), new PropertyMetadata(1.0));

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

        public int[] DpiList { get; set; } = new int[] { 96, 150, 225, 300, 600 };

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

        public FileStream PdfFileStream
        {
            get => (FileStream)GetValue(PdfFileStreamProperty);
            set => SetValue(PdfFileStreamProperty, value);
        }

        public new ICommand Resize { get; }

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
            if (d is PdfViewer pdfViewer)
            {
                if (e.NewValue is not null)
                {
                    pdfViewer.PdfFileStream = new FileStream(e.NewValue as string, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    pdfViewer.Sayfa = 1;
                }
                else
                {
                    pdfViewer.Source = null;
                }
            }
        }

        private static void PdfStreamChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfViewer pdfViewer && string.Equals(Path.GetExtension(pdfViewer.DataContext as string), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                pdfViewer.ToplamSayfa = Pdf2Png.ConvertAllPages(pdfViewer.PdfFileStream, 0).Count;
                pdfViewer.TifNavigasyonButtonEtkin = pdfViewer.ToplamSayfa > 1 ? Visibility.Visible : Visibility.Collapsed;
                pdfViewer.Pages = Enumerable.Range(1, pdfViewer.ToplamSayfa);
                pdfViewer.Source = pdfViewer.FirstPageThumbnail
                    ? BitmapSourceFromByteArray(Pdf2Png.Convert(pdfViewer.PdfFileStream, 1, 108), true)
                    : BitmapSourceFromByteArray(Pdf2Png.Convert(pdfViewer.PdfFileStream, pdfViewer.Sayfa, pdfViewer.Dpi));
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