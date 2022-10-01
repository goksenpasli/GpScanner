using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Extensions;
using Freeware;
using Microsoft.Win32;
using static Extensions.ExtensionMethods;

namespace PdfViewer
{
    public enum FitImageOrientation
    {
        Width = 0,

        Height = 1
    }

    public class PdfViewer : Control, INotifyPropertyChanged, IDisposable
    {
        public static readonly DependencyProperty AngleProperty = DependencyProperty.Register("Angle", typeof(double), typeof(PdfViewer), new PropertyMetadata(0.0));

        public static readonly DependencyProperty DpiProperty = DependencyProperty.Register("Dpi", typeof(int), typeof(PdfViewer), new PropertyMetadata(200, DpiChanged));

        public static readonly DependencyProperty PdfFilePathProperty = DependencyProperty.Register("PdfFilePath", typeof(string), typeof(PdfViewer), new PropertyMetadata(null, async (o, e) => await PdfFilePathChangedAsync(o, e)));

        public static readonly DependencyProperty PdfFileStreamProperty = DependencyProperty.Register("PdfFileStream", typeof(byte[]), typeof(PdfViewer), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.NotDataBindable, async (o, e) => await PdfStreamChangedAsync(o, e)));

        public static readonly DependencyProperty SnapTickProperty = DependencyProperty.Register("SnapTick", typeof(bool), typeof(PdfViewer), new PropertyMetadata(false));

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(ImageSource), typeof(PdfViewer), new PropertyMetadata(null, SourceChanged));

        public static readonly DependencyProperty ToolBarVisibilityProperty = DependencyProperty.Register("ToolBarVisibility", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Visible));

        public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register("Zoom", typeof(double), typeof(PdfViewer), new PropertyMetadata(1.0));

        static PdfViewer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PdfViewer), new FrameworkPropertyMetadata(typeof(PdfViewer)));
        }

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

            Yazdır = new RelayCommand<object>(parameter =>
            {
                PrintDialog pd = new();
                DrawingVisual dv = new();
                if (Decoder == null)
                {
                    if (pd.ShowDialog() == true)
                    {
                        using (DrawingContext dc = dv.RenderOpen())
                        {
                            BitmapSource imagesource = Source.Width > Source.Height
                                ? ((BitmapSource)Source)?.Resize((int)pd.PrintableAreaHeight, (int)pd.PrintableAreaWidth, 90, 300, 300)
                                : ((BitmapSource)Source)?.Resize((int)pd.PrintableAreaWidth, (int)pd.PrintableAreaHeight, 0, 300, 300);
                            imagesource.Freeze();
                            dc.DrawImage(imagesource, new Rect(0, 0, pd.PrintableAreaWidth, pd.PrintableAreaHeight));
                        }

                        pd.PrintVisual(dv, "");
                    }
                }
                else
                {
                    pd.PageRangeSelection = PageRangeSelection.AllPages;
                    pd.UserPageRangeEnabled = true;
                    pd.MaxPage = (uint)Decoder.Frames.Count;
                    pd.MinPage = 1;
                    if (pd.ShowDialog() == true)
                    {
                        int başlangıç;
                        int bitiş;
                        if (pd.PageRangeSelection == PageRangeSelection.AllPages)
                        {
                            başlangıç = 0;
                            bitiş = Decoder.Frames.Count - 1;
                        }
                        else
                        {
                            başlangıç = pd.PageRange.PageFrom - 1;
                            bitiş = pd.PageRange.PageTo - 1;
                        }

                        for (int i = başlangıç; i <= bitiş; i++)
                        {
                            using (DrawingContext dc = dv.RenderOpen())
                            {
                                BitmapSource imagesource = Source.Width > Source.Height
                                    ? Decoder.Frames[i]?.Resize((int)pd.PrintableAreaHeight, (int)pd.PrintableAreaWidth, 90, 300, 300)
                                    : Decoder.Frames[i]?.Resize((int)pd.PrintableAreaWidth, (int)pd.PrintableAreaHeight, 0, 300, 300);
                                imagesource.Freeze();
                                dc.DrawImage(imagesource, new Rect(0, 0, pd.PrintableAreaWidth, pd.PrintableAreaHeight));
                            }

                            pd.PrintVisual(dv, "");
                        }
                    }
                }
            }, parameter => Source is not null);

            ViewerBack = new RelayCommand<object>(parameter => Sayfa--, parameter => Source is not null && Sayfa > 1 && Sayfa <= ToplamSayfa);

            ViewerNext = new RelayCommand<object>(parameter => Sayfa++, parameter => Source is not null && Sayfa >= 1 && Sayfa < ToplamSayfa);

            SaveImage = new RelayCommand<object>(parameter =>
            {
                SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Jpg Dosyası(*.jpg)|*.jpg",
                    FileName = "Resim"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        File.WriteAllBytes(saveFileDialog.FileName, Source.ToTiffJpegByteArray(Format.Jpg));
                    }
                    catch (Exception ex)
                    {
                        _ = MessageBox.Show(ex.Message);
                    }
                }
            }, parameter => Source is not null);

            Resize = new RelayCommand<object>(delegate
            {
                Zoom = (FitImageOrientation.Width != 0) ? (double.IsNaN(Height) ? ((ActualHeight == 0.0) ? 1.0 : (ActualHeight / Source.Height)) : ((Height == 0.0) ? 1.0 : (Height / Source.Height))) : (double.IsNaN(Width) ? ((ActualWidth == 0.0) ? 1.0 : (ActualWidth / Source.Width)) : ((Width == 0.0) ? 1.0 : (Width / Source.Width)));
            }, (object parameter) => Source != null);

            OrijinalDosyaAç = new RelayCommand<object>(parameter => _ = Process.Start(parameter as string), parameter => !DesignerProperties.GetIsInDesignMode(new DependencyObject()) && File.Exists(parameter as string));

            PropertyChanged += PdfViewer_PropertyChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public double Angle
        {
            get => (double)GetValue(AngleProperty);
            set => SetValue(AngleProperty, value);
        }

        [Browsable(false)]
        public TiffBitmapDecoder Decoder
        {
            get => decoder;

            set
            {
                if (decoder != value)
                {
                    decoder = value;
                    OnPropertyChanged(nameof(Decoder));
                }
            }
        }

        public RelayCommand<object> DosyaAç { get; }

        public int Dpi
        {
            get => (int)GetValue(DpiProperty);
            set => SetValue(DpiProperty, value);
        }

        public int[] DpiList { get; } = new int[] { 72, 96, 120, 150, 200, 300, 400, 500, 600 };

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

        public FitImageOrientation FitImageOrientation
        {
            get => fitImageOrientation;

            set
            {
                if (fitImageOrientation != value)
                {
                    fitImageOrientation = value;
                    OnPropertyChanged(nameof(FitImageOrientation));
                }
            }
        }

        public Visibility OpenButtonVisibility
        {
            get => openButtonVisibility;

            set
            {
                if (openButtonVisibility != value)
                {
                    openButtonVisibility = value;
                    OnPropertyChanged(nameof(OpenButtonVisibility));
                }
            }
        }

        public RelayCommand<object> OrijinalDosyaAç { get; }

        [Browsable(false)]
        public IEnumerable<int> Pages
        {
            get => pages;

            set
            {
                if (pages != value)
                {
                    pages = value;
                    OnPropertyChanged(nameof(Pages));
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

        public Visibility PrintButtonVisibility
        {
            get => printButtonVisibility;

            set
            {
                if (printButtonVisibility != value)
                {
                    printButtonVisibility = value;
                    OnPropertyChanged(nameof(PrintButtonVisibility));
                }
            }
        }

        public RelayCommand<object> Resize { get; }

        public ICommand SaveImage { get; }

        public int Sayfa
        {
            get => sayfa;

            set
            {
                if (sayfa != value)
                {
                    sayfa = value;
                    OnPropertyChanged(nameof(Sayfa));
                }
            }
        }

        public bool SnapTick
        {
            get => (bool)GetValue(SnapTickProperty);
            set => SetValue(SnapTickProperty, value);
        }

        public ImageSource Source
        {
            get => (ImageSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

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

        public Visibility TifNavigasyonButtonEtkin
        {
            get => tifNavigasyonButtonEtkin;

            set
            {
                if (tifNavigasyonButtonEtkin != value)
                {
                    tifNavigasyonButtonEtkin = value;
                    OnPropertyChanged(nameof(TifNavigasyonButtonEtkin));
                }
            }
        }

        public Visibility ToolBarVisibility
        {
            get => (Visibility)GetValue(ToolBarVisibilityProperty);
            set => SetValue(ToolBarVisibilityProperty, value);
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

        public RelayCommand<object> ViewerBack { get; }

        public RelayCommand<object> ViewerNext { get; }

        public ICommand Yazdır { get; }

        public double Zoom
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
                buffer = null;
                return bitmap;
            }
            return null;
        }

        public static async Task<BitmapImage> ConvertToImg(byte[] stream, int page, int dpi, bool fasterimage = false)
        {
            return stream.Length > 0 ? await Task.Run(() => BitmapSourceFromByteArray(Pdf2Png.Convert(stream, page, dpi), false, dpi)) : null;
        }

        public static async Task<MemoryStream> ConvertToImgStreamAsync(byte[] stream, int page, int dpi)
        {
            return stream.Length > 0 ? await Task.Run(() => new MemoryStream(Pdf2Png.Convert(stream, page, dpi))) : null;
        }

        public static int PdfPageCount(byte[] stream)
        {
            return stream.Length > 0 ? Pdf2Png.ConvertAllPages(stream, 0).Count : 0;
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
                    Source = null;
                }
                disposedValue = true;
            }
        }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private TiffBitmapDecoder decoder;

        private bool disposedValue;

        private bool firstPageThumbnail;

        private FitImageOrientation fitImageOrientation;

        private Visibility openButtonVisibility = Visibility.Collapsed;

        private IEnumerable<int> pages;

        private Visibility printButtonVisibility = Visibility.Collapsed;

        private int sayfa = 1;

        private int thumbnailDpi = 120;

        private Visibility tifNavigasyonButtonEtkin = Visibility.Collapsed;

        private int toplamSayfa;

        private static async void DpiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfViewer pdfViewer && pdfViewer.PdfFileStream is not null)
            {
                pdfViewer.Source = await ConvertToImg(pdfViewer.PdfFileStream, pdfViewer.Sayfa, (int)e.NewValue);
            }
        }

        private static async Task PdfFilePathChangedAsync(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfViewer pdfViewer && File.Exists(e.NewValue as string) && string.Equals(Path.GetExtension(e.NewValue as string), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                if (e.NewValue is not null)
                {
                    pdfViewer.PdfFileStream = await Task.Run(() => File.ReadAllBytes(e.NewValue as string));
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
                        pdfViewer.Source = await ConvertToImg(pdfdata, 1, 96, true);
                    }
                    else
                    {
                        pdfViewer.ToplamSayfa = PdfPageCount(pdfdata);
                        pdfViewer.Source = await ConvertToImg(pdfdata, sayfa, dpi);
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

        private static void SourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfViewer pdfViewer && e.NewValue is not null)
            {
                switch (pdfViewer.FitImageOrientation)
                {
                    case FitImageOrientation.Width:
                        {
                            if (!double.IsNaN(pdfViewer.Width))
                            {
                                pdfViewer.Zoom = pdfViewer.Width == 0 ? 1 : pdfViewer.Width / pdfViewer.Source.Width;
                                return;
                            }
                            if (pdfViewer.ActualWidth == 0)
                            {
                                pdfViewer.Zoom = 1;
                                return;
                            }
                            pdfViewer.Zoom = Math.Round(pdfViewer.ActualWidth / pdfViewer.Source.Width, 2);
                            return;
                        }

                    default:
                        if (!double.IsNaN(pdfViewer.Height))
                        {
                            pdfViewer.Zoom = pdfViewer.Height == 0 ? 1 : pdfViewer.Height / pdfViewer.Source.Height;
                            return;
                        }
                        if (pdfViewer.ActualHeight == 0)
                        {
                            pdfViewer.Zoom = 1;
                            return;
                        }
                        pdfViewer.Zoom = Math.Round(pdfViewer.ActualHeight / pdfViewer.Source.Height, 2);
                        return;
                }
            }
        }

        private async void PdfViewer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "Sayfa" && sender is PdfViewer pdfViewer && pdfViewer.PdfFileStream is not null)
            {
                Source = await ConvertToImg(pdfViewer.PdfFileStream, Sayfa, pdfViewer.Dpi);
            }
        }
    }
}