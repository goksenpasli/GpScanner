using System;
using System.Collections.Generic;
using System.ComponentModel;
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

    [TemplatePart(Name = "ScrollVwr", Type = typeof(ScrollViewer))]
    public class PdfViewer : Control, INotifyPropertyChanged, IDisposable
    {
        public static readonly DependencyProperty AngleProperty = DependencyProperty.Register("Angle", typeof(double), typeof(PdfViewer), new PropertyMetadata(0.0));

        public static readonly DependencyProperty ContextMenuVisibilityProperty = DependencyProperty.Register("ContextMenuVisibility", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Collapsed));

        public static readonly DependencyProperty DpiProperty = DependencyProperty.Register("Dpi", typeof(int), typeof(PdfViewer), new PropertyMetadata(200, DpiChanged));

        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register("Orientation", typeof(FitImageOrientation), typeof(PdfViewer), new PropertyMetadata(FitImageOrientation.Width, Changed));

        public static readonly DependencyProperty PageScrollBarVisibilityProperty = DependencyProperty.Register("PageScrollBarVisibility", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Visible));

        public static readonly DependencyProperty PdfFilePathProperty = DependencyProperty.Register("PdfFilePath", typeof(string), typeof(PdfViewer), new PropertyMetadata(null, async (o, e) => await PdfFilePathChangedAsync(o, e)));

        public static readonly DependencyProperty ScrollBarVisibleProperty = DependencyProperty.Register("ScrollBarVisible", typeof(ScrollBarVisibility), typeof(PdfViewer), new PropertyMetadata(ScrollBarVisibility.Auto));

        public static readonly DependencyProperty SnapTickProperty = DependencyProperty.Register("SnapTick", typeof(bool), typeof(PdfViewer), new PropertyMetadata(false));

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(ImageSource), typeof(PdfViewer), new PropertyMetadata(null, SourceChanged));

        public static readonly DependencyProperty ThumbsVisibleProperty = DependencyProperty.Register("ThumbsVisible", typeof(bool), typeof(PdfViewer), new PropertyMetadata(true));

        public static readonly DependencyProperty ToolBarVisibilityProperty = DependencyProperty.Register("ToolBarVisibility", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Visible));

        public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register("Zoom", typeof(double), typeof(PdfViewer), new PropertyMetadata(1.0));

        static PdfViewer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PdfViewer), new FrameworkPropertyMetadata(typeof(PdfViewer)));
        }

        public PdfViewer()
        {
            PropertyChanged += PdfViewer_PropertyChanged;
            SizeChanged += PdfViewer_SizeChanged;
            DosyaAç = new RelayCommand<object>(parameter =>
            {
                OpenFileDialog openFileDialog = new() { Multiselect = false, Filter = "Pdf Dosyaları (*.pdf)|*.pdf" };
                if (openFileDialog.ShowDialog() == true)
                {
                    PdfFilePath = openFileDialog.FileName;
                }
            });

            Yazdır = new RelayCommand<object>(async parameter =>
            {
                string pdfFilePath = PdfFilePath;
                PrintPdfFile(await ReadAllFileAsync(pdfFilePath));
                GC.Collect();
            }, parameter => PdfFilePath is not null);

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
                if (Source is not null)
                {
                    Zoom = (Orientation != FitImageOrientation.Width) ? ActualHeight / Source.Height : ActualWidth / Source.Width;
                }
            }, (object parameter) => Source != null);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static int[] DpiList { get; } = new int[] { 12, 24, 36, 48, 72, 96, 120, 150, 200, 300, 400, 500, 600 };

        public double Angle {
            get => (double)GetValue(AngleProperty);
            set => SetValue(AngleProperty, value);
        }

        public bool AutoFitContent {
            get => autoFitContent;

            set {
                if (autoFitContent != value)
                {
                    autoFitContent = value;
                    OnPropertyChanged(nameof(AutoFitContent));
                }
            }
        }

        public Visibility ContextMenuVisibility {
            get => (Visibility)GetValue(ContextMenuVisibilityProperty);
            set => SetValue(ContextMenuVisibilityProperty, value);
        }

        public RelayCommand<object> DosyaAç { get; }

        public int Dpi {
            get => (int)GetValue(DpiProperty);
            set => SetValue(DpiProperty, value);
        }

        public Visibility DpiListVisibility {
            get => dpiListVisibility;

            set {
                if (dpiListVisibility != value)
                {
                    dpiListVisibility = value;
                    OnPropertyChanged(nameof(DpiListVisibility));
                }
            }
        }

        public Visibility OpenButtonVisibility {
            get => openButtonVisibility;

            set {
                if (openButtonVisibility != value)
                {
                    openButtonVisibility = value;
                    OnPropertyChanged(nameof(OpenButtonVisibility));
                }
            }
        }

        public FitImageOrientation Orientation {
            get => (FitImageOrientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        [Browsable(false)]
        public IEnumerable<int> Pages {
            get => pages;

            set {
                if (pages != value)
                {
                    pages = value;
                    OnPropertyChanged(nameof(Pages));
                }
            }
        }

        public Visibility PageScrollBarVisibility {
            get => (Visibility)GetValue(PageScrollBarVisibilityProperty);
            set => SetValue(PageScrollBarVisibilityProperty, value);
        }

        public string PdfFilePath {
            get => (string)GetValue(PdfFilePathProperty);
            set => SetValue(PdfFilePathProperty, value);
        }

        public Visibility PrintButtonVisibility {
            get => printButtonVisibility;

            set {
                if (printButtonVisibility != value)
                {
                    printButtonVisibility = value;
                    OnPropertyChanged(nameof(PrintButtonVisibility));
                }
            }
        }

        public RelayCommand<object> Resize { get; }

        public ICommand SaveImage { get; }

        public int Sayfa {
            get => sayfa;

            set {
                if (sayfa != value)
                {
                    sayfa = value;
                    OnPropertyChanged(nameof(Sayfa));
                }
            }
        }

        public ScrollBarVisibility ScrollBarVisible {
            get => (ScrollBarVisibility)GetValue(ScrollBarVisibleProperty);
            set => SetValue(ScrollBarVisibleProperty, value);
        }

        public Visibility SliderZoomAngleVisibility {
            get => sliderZoomAngleVisibility;

            set {
                if (sliderZoomAngleVisibility != value)
                {
                    sliderZoomAngleVisibility = value;
                    OnPropertyChanged(nameof(SliderZoomAngleVisibility));
                }
            }
        }

        public bool SnapTick {
            get => (bool)GetValue(SnapTickProperty);
            set => SetValue(SnapTickProperty, value);
        }

        public ImageSource Source {
            get => (ImageSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public bool ThumbsVisible {
            get { return (bool)GetValue(ThumbsVisibleProperty); }
            set { SetValue(ThumbsVisibleProperty, value); }
        }

        public Visibility TifNavigasyonButtonEtkin {
            get => tifNavigasyonButtonEtkin;

            set {
                if (tifNavigasyonButtonEtkin != value)
                {
                    tifNavigasyonButtonEtkin = value;
                    OnPropertyChanged(nameof(TifNavigasyonButtonEtkin));
                }
            }
        }

        public Visibility ToolBarVisibility {
            get => (Visibility)GetValue(ToolBarVisibilityProperty);
            set => SetValue(ToolBarVisibilityProperty, value);
        }

        [Browsable(false)]
        public int ToplamSayfa {
            get => toplamSayfa;

            set {
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

        public double Zoom {
            get => (double)GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }

        public static async Task<BitmapImage> ConvertToImgAsync(byte[] pdffilestream, int page, int dpi, int decodepixel = 0)
        {
            try
            {
                if (pdffilestream?.Length > 0)
                {
                    return await Task.Run(() =>
                    {
                        byte[] imagearray = Pdf2Png.Convert(pdffilestream, page, dpi);
                        if (imagearray is not null)
                        {
                            MemoryStream ms = new(imagearray);
                            BitmapImage bitmap = new();
                            bitmap.BeginInit();
                            bitmap.DecodePixelHeight = decodepixel;
                            bitmap.CacheOption = BitmapCacheOption.None;
                            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.DelayCreation;
                            bitmap.StreamSource = ms;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            pdffilestream = null;
                            imagearray = null;
                            ms = null;
                            GC.Collect();
                            return bitmap;
                        }
                        return null;
                    });
                }
            }
            catch (Exception)
            {
                pdffilestream = null;
            }
            return null;
        }

        public static async Task<MemoryStream> ConvertToImgStreamAsync(byte[] stream, int page, int dpi)
        {
            try
            {
                if (stream?.Length > 0)
                {
                    return await Task.Run(() =>
                    {
                        byte[] buffer = Pdf2Png.Convert(stream, page, dpi);
                        if (buffer != null)
                        {
                            MemoryStream ms = new(buffer);
                            buffer = null;
                            stream = null;
                            GC.Collect();
                            return ms;
                        }
                        return null;
                    });
                }
            }
            catch (Exception)
            {
                stream = null;
            }
            return null;
        }

        public static async Task<int> PdfPageCountAsync(byte[] stream)
        {
            try
            {
                if (stream?.Length > 0)
                {
                    return await Task.Run(() =>
                    {
                        int pagecount = Pdf2Png.ConvertAllPages(stream, 0).Count;
                        stream = null;
                        GC.Collect();
                        return pagecount;
                    });
                }
            }
            catch (Exception)
            {
                stream = null;
            }
            return 0;
        }

        public static void PrintImageSource(ImageSource Source, int Dpi = 300, bool resize = true)
        {
            PrintDialog pd = new();
            DrawingVisual dv = new();
            if (pd.ShowDialog() == true)
            {
                using (DrawingContext dc = dv.RenderOpen())
                {
                    BitmapSource bs;
                    if (resize)
                    {
                        bs = Source.Width > Source.Height ? ((BitmapSource)Source)?.Resize((int)pd.PrintableAreaHeight, (int)pd.PrintableAreaWidth, 90, Dpi, Dpi) : ((BitmapSource)Source)?.Resize((int)pd.PrintableAreaWidth, (int)pd.PrintableAreaHeight, 0, Dpi, Dpi);
                        bs.Freeze();
                        dc.DrawImage(bs, new Rect(0, 0, pd.PrintableAreaWidth, pd.PrintableAreaHeight));
                    }
                    else
                    {
                        bs = (BitmapSource)Source;
                        bs.Freeze();
                        dc.DrawImage(bs, new Rect(0, 0, pd.PrintableAreaWidth, pd.PrintableAreaHeight));
                    }
                }
                pd.PrintVisual(dv, "");
            }
        }

        public static async Task<byte[]> ReadAllFileAsync(string filename)
        {
            try
            {
                using FileStream file = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                byte[] buffer = new byte[file.Length];
                _ = await file.ReadAsync(buffer, 0, (int)file.Length);
                return buffer;
            }
            catch (Exception)
            {
            }
            return null;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _scrollvwr = GetTemplateChild("ScrollVwr") as ScrollViewer;
            if (_scrollvwr != null)
            {
                _scrollvwr.Drop -= _scrollvwr_Drop;
                _scrollvwr.Drop += _scrollvwr_Drop;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Source = null;
                }
                disposedValue = true;
            }
        }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ScrollViewer _scrollvwr;

        private bool autoFitContent;

        private bool disposedValue;

        private Visibility dpiListVisibility = Visibility.Visible;

        private Visibility openButtonVisibility = Visibility.Collapsed;

        private IEnumerable<int> pages;

        private Visibility printButtonVisibility = Visibility.Collapsed;

        private int sayfa = 1;

        private Visibility sliderZoomAngleVisibility = Visibility.Visible;

        private Visibility tifNavigasyonButtonEtkin = Visibility.Visible;

        private int toplamSayfa;

        private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfViewer pdfViewer && pdfViewer.Source is not null && !DesignerProperties.GetIsInDesignMode(pdfViewer))
            {
                pdfViewer.Resize.Execute(null);
            }
        }

        private static async void DpiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfViewer pdfViewer && pdfViewer.PdfFilePath is not null)
            {
                string pdfFilePath = pdfViewer.PdfFilePath;
                pdfViewer.Source = await ConvertToImgAsync(await ReadAllFileAsync(pdfFilePath), pdfViewer.Sayfa, (int)e.NewValue);
                GC.Collect();
            }
        }

        private static async Task PdfFilePathChangedAsync(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfViewer pdfViewer)
            {
                if (e.NewValue is not null && File.Exists(e.NewValue as string) && string.Equals(Path.GetExtension(e.NewValue as string), ".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        byte[] data = await ReadAllFileAsync(e.NewValue as string);
                        int dpi = pdfViewer.Dpi;
                        pdfViewer.Source = await ConvertToImgAsync(data, pdfViewer.Sayfa, dpi);
                        pdfViewer.ToplamSayfa = await PdfPageCountAsync(data);
                        pdfViewer.Pages = Enumerable.Range(1, pdfViewer.ToplamSayfa);
                        pdfViewer.Resize.Execute(null);
                        data = null;
                        GC.Collect();
                    }
                    catch (Exception)
                    {
                    }
                    return;
                }
                pdfViewer.Source = null;
            }
        }

        private static void SourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfViewer pdfViewer && e.NewValue is not null)
            {
                pdfViewer.Resize.Execute(null);
            }
        }

        private void _scrollvwr_Drop(object sender, DragEventArgs e)
        {
            string[] droppedfiles = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (droppedfiles?.Length > 0)
            {
                PdfFilePath = droppedfiles[0];
            }
        }

        private async void PdfViewer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "Sayfa" && sender is PdfViewer pdfViewer && pdfViewer.PdfFilePath is not null)
            {
                string pdfFilePath = pdfViewer.PdfFilePath;
                Source = await ConvertToImgAsync(await ReadAllFileAsync(pdfFilePath), sayfa, pdfViewer.Dpi);
                GC.Collect();
            }
        }

        private void PdfViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (AutoFitContent)
            {
                Resize.Execute(null);
            }
        }

        private async void PrintPdfFile(byte[] stream, int Dpi = 300)
        {
            int pagecount = await PdfPageCountAsync(stream);
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