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

        public static readonly DependencyProperty ContextMenuVisibilityPropertyProperty = DependencyProperty.Register("ContextMenuVisibilityProperty", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Collapsed));

        public static readonly DependencyProperty DpiProperty = DependencyProperty.Register("Dpi", typeof(int), typeof(PdfViewer), new PropertyMetadata(200, DpiChanged));

        public static readonly DependencyProperty PdfFilePathProperty = DependencyProperty.Register("PdfFilePath", typeof(string), typeof(PdfViewer), new PropertyMetadata(null, async (o, e) => await PdfFilePathChangedAsync(o, e)));

        public static readonly DependencyProperty PdfFileStreamProperty = DependencyProperty.Register("PdfFileStream", typeof(byte[]), typeof(PdfViewer), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.NotDataBindable, async (o, e) => await PdfStreamChangedAsync(o, e)));

        public static readonly DependencyProperty ScrollBarVisibleProperty = DependencyProperty.Register("ScrollBarVisible", typeof(ScrollBarVisibility), typeof(PdfViewer), new PropertyMetadata(ScrollBarVisibility.Auto));

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

            Yazdır = new RelayCommand<object>(parameter => PrintPdfFile(PdfFileStream), parameter => PdfFileStream is not null);

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
            Loaded += PdfViewer_Loaded;
            Unloaded += PdfViewer_Unloaded;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public double Angle
        {
            get => (double)GetValue(AngleProperty);
            set => SetValue(AngleProperty, value);
        }

        public Visibility ContextMenuVisibilityProperty
        {
            get => (Visibility)GetValue(ContextMenuVisibilityPropertyProperty);
            set => SetValue(ContextMenuVisibilityPropertyProperty, value);
        }

        public RelayCommand<object> DosyaAç { get; }

        public int Dpi
        {
            get => (int)GetValue(DpiProperty);
            set => SetValue(DpiProperty, value);
        }

        public int[] DpiList { get; } = new int[] { 72, 96, 120, 150, 200, 300, 400, 500, 600 };

        public Visibility DpiListVisibility
        {
            get => dpiListVisibility;

            set
            {
                if (dpiListVisibility != value)
                {
                    dpiListVisibility = value;
                    OnPropertyChanged(nameof(DpiListVisibility));
                }
            }
        }

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

        public ScrollBarVisibility ScrollBarVisible
        {
            get => (ScrollBarVisibility)GetValue(ScrollBarVisibleProperty);
            set => SetValue(ScrollBarVisibleProperty, value);
        }

        public Visibility SliderZoomAngleVisibility
        {
            get => sliderZoomAngleVisibility;

            set
            {
                if (sliderZoomAngleVisibility != value)
                {
                    sliderZoomAngleVisibility = value;
                    OnPropertyChanged(nameof(SliderZoomAngleVisibility));
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

        public static BitmapImage BitmapSourceFromByteArray(byte[] buffer, bool fasterimage = false, int thumbdpi = 96)
        {
            if (buffer != null)
            {
                BitmapImage bitmap = new();
                MemoryStream stream = new(buffer);
                bitmap.BeginInit();
                if (fasterimage)
                {
                    bitmap.DecodePixelWidth = thumbdpi;
                }
                bitmap.CacheOption = BitmapCacheOption.None;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            return null;
        }

        public static async Task<BitmapImage> ConvertToImgAsync(byte[] stream, int page, int dpi, bool fasterimage = false)
        {
            return stream.Length > 0 ? await Task.Run(() => BitmapSourceFromByteArray(Pdf2Png.Convert(stream, page, dpi), fasterimage, dpi)) : null;
        }

        public static async Task<MemoryStream> ConvertToImgStreamAsync(byte[] stream, int page, int dpi)
        {
            return stream.Length > 0 ? await Task.Run(() => new MemoryStream(Pdf2Png.Convert(stream, page, dpi))) : null;
        }

        public static int PdfPageCount(byte[] stream)
        {
            return stream.Length > 0 ? Pdf2Png.ConvertAllPages(stream, 0).Count : 0;
        }

        public static void PrintImageSource(ImageSource Source, int Dpi = 300)
        {
            PrintDialog pd = new();
            DrawingVisual dv = new();
            if (pd.ShowDialog() == true)
            {
                using (DrawingContext dc = dv.RenderOpen())
                {
                    BitmapSource bs = Source.Width > Source.Height
                        ? ((BitmapSource)Source)?.Resize((int)pd.PrintableAreaHeight, (int)pd.PrintableAreaWidth, 90, Dpi, Dpi)
                        : ((BitmapSource)Source)?.Resize((int)pd.PrintableAreaWidth, (int)pd.PrintableAreaHeight, 0, Dpi, Dpi);
                    bs.Freeze();
                    dc.DrawImage(bs, new Rect(0, 0, pd.PrintableAreaWidth, pd.PrintableAreaHeight));
                }
                pd.PrintVisual(dv, "");
            }
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
                    PdfFilePath = null;
                }
                disposedValue = true;
            }
        }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool disposedValue;

        private Visibility dpiListVisibility = Visibility.Visible;

        private bool firstPageThumbnail;

        private FitImageOrientation fitImageOrientation;

        private Visibility openButtonVisibility = Visibility.Collapsed;

        private IEnumerable<int> pages;

        private Visibility printButtonVisibility = Visibility.Collapsed;

        private int sayfa = 1;

        private Visibility sliderZoomAngleVisibility = Visibility.Visible;

        private int thumbnailDpi = 96;

        private Visibility tifNavigasyonButtonEtkin = Visibility.Collapsed;

        private int toplamSayfa;

        private static async void DpiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfViewer pdfViewer && pdfViewer.PdfFileStream is not null)
            {
                pdfViewer.Source = await ConvertToImgAsync(pdfViewer.PdfFileStream, pdfViewer.Sayfa, (int)e.NewValue);
            }
        }

        private static async Task PdfFilePathChangedAsync(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfViewer pdfViewer)
            {
                if (e.NewValue is not null && File.Exists(e.NewValue as string) && string.Equals(Path.GetExtension(e.NewValue as string), ".pdf", StringComparison.OrdinalIgnoreCase))
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
                    pdfViewer.Source = pdfViewer.FirstPageThumbnail ? await ConvertToImgAsync(pdfdata, sayfa, thumbdpi, true) : await ConvertToImgAsync(pdfdata, sayfa, dpi);
                    pdfViewer.ToplamSayfa = PdfPageCount(pdfdata);
                    pdfViewer.TifNavigasyonButtonEtkin = pdfViewer.ToplamSayfa > 1 ? Visibility.Visible : Visibility.Collapsed;
                    pdfViewer.Pages = Enumerable.Range(1, pdfViewer.ToplamSayfa);
                    pdfdata = null;
                    GC.Collect();
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
                pdfViewer.Resize.Execute(null);
            }
        }

        private async void PdfViewer_Loaded(object sender, RoutedEventArgs e)
        {
            string path = PdfFilePath;
            if (path != null)
            {
                PdfFileStream = await Task.Run(() => File.ReadAllBytes(path));
            }
        }

        private async void PdfViewer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "Sayfa" && sender is PdfViewer pdfViewer && pdfViewer.PdfFileStream is not null)
            {
                Source = pdfViewer.FirstPageThumbnail ? await ConvertToImgAsync(pdfViewer.PdfFileStream, sayfa, pdfViewer.ThumbnailDpi, true) : await ConvertToImgAsync(pdfViewer.PdfFileStream, sayfa, pdfViewer.Dpi);
                GC.Collect();
            }
        }

        private void PdfViewer_Unloaded(object sender, RoutedEventArgs e)
        {
            PdfFileStream = null;
            Source = null;
            GC.Collect();
        }

        private async void PrintPdfFile(byte[] stream, int Dpi = 300)
        {
            int pagecount = PdfPageCount(stream);
            PrintDialog pd = new()
            {
                PageRangeSelection = PageRangeSelection.AllPages,
                UserPageRangeEnabled = true,
                MaxPage = (uint)pagecount,
                MinPage = 1
            };
            DrawingVisual dv = new();
            if (pd.ShowDialog() == true)
            {
                int başlangıç;
                int bitiş;
                if (pd.PageRangeSelection == PageRangeSelection.AllPages)
                {
                    başlangıç = 1;
                    bitiş = pagecount;
                }
                else
                {
                    başlangıç = pd.PageRange.PageFrom;
                    bitiş = pd.PageRange.PageTo;
                }

                for (int i = başlangıç; i <= bitiş; i++)
                {
                    using (DrawingContext dc = dv.RenderOpen())
                    {
                        BitmapImage bitmapimage = await ConvertToImgAsync(stream, i, Dpi);
                        BitmapSource bs = bitmapimage.Width > bitmapimage.Height
                        ? bitmapimage?.Resize((int)pd.PrintableAreaHeight, (int)pd.PrintableAreaWidth, 90, Dpi, Dpi)
                        : bitmapimage?.Resize((int)pd.PrintableAreaWidth, (int)pd.PrintableAreaHeight, 0, Dpi, Dpi);
                        bs.Freeze();
                        dc.DrawImage(bs, new Rect(0, 0, pd.PrintableAreaWidth, pd.PrintableAreaHeight));
                        bitmapimage = null;
                        bs = null;
                    }
                    pd.PrintVisual(dv, "");
                }
            }
        }
    }
}