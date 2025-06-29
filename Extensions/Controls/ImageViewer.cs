using Extensions.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Printing;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Xps;

namespace Extensions;

[TemplatePart(Name = "PanoramaViewPort", Type = typeof(Viewport3D))]
[TemplatePart(Name = "panoramaBrush", Type = typeof(DiffuseMaterial))]
public class ImageViewer : Control, INotifyPropertyChanged, IDisposable
{
    public static readonly DependencyProperty AngleButtonVisibilityProperty = DependencyProperty.Register("AngleButtonVisibility", typeof(Visibility), typeof(ImageViewer), new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty AngleProperty = DependencyProperty.Register("Angle", typeof(double), typeof(ImageViewer), new PropertyMetadata(0.0));
    public static readonly DependencyProperty DecodeHeightProperty = DependencyProperty.Register("DecodeHeight", typeof(int), typeof(ImageViewer), new PropertyMetadata(300, DecodeHeightChangedAsync));
    public static readonly DependencyProperty FovProperty = DependencyProperty.Register("Fov", typeof(double), typeof(ImageViewer), new PropertyMetadata(95d, FovChanged));
    public static readonly DependencyProperty ImageFilePathProperty = DependencyProperty.Register("ImageFilePath", typeof(string), typeof(ImageViewer), new PropertyMetadata(null, ImageFilePathChangedAsync));
    public static readonly DependencyProperty MaxZoomProperty = DependencyProperty.Register("MaxZoom", typeof(double), typeof(ImageViewer), new PropertyMetadata(10d));
    public static readonly DependencyProperty MinZoomProperty = DependencyProperty.Register("MinZoom", typeof(double), typeof(ImageViewer), new PropertyMetadata(0.1d));
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register("Orientation", typeof(FitImageOrientation), typeof(ImageViewer), new PropertyMetadata(FitImageOrientation.None, OrientationChanged));
    public static readonly DependencyProperty OriginalPixelHeightProperty = DependencyProperty.Register("OriginalPixelHeight", typeof(int), typeof(ImageViewer), new PropertyMetadata(0));
    public static readonly DependencyProperty OriginalPixelWidthProperty = DependencyProperty.Register("OriginalPixelWidth", typeof(int), typeof(ImageViewer), new PropertyMetadata(0));
    public static readonly DependencyProperty PanoramaModeProperty = DependencyProperty.Register("PanoramaMode", typeof(bool), typeof(ImageViewer), new PropertyMetadata(PanoramaModeChanged));
    public static readonly DependencyProperty PrintDpiProperty = DependencyProperty.Register("PrintDpi", typeof(int), typeof(ImageViewer), new PropertyMetadata(300));
    public static readonly DependencyProperty PrintDpiSettingsListEnabledProperty = DependencyProperty.Register("PrintDpiSettingsListEnabled", typeof(bool), typeof(ImageViewer), new PropertyMetadata(true));
    public static readonly DependencyProperty RotateXProperty = DependencyProperty.Register("RotateX", typeof(double), typeof(ImageViewer), new PropertyMetadata(0.0));
    public static readonly DependencyProperty RotateYProperty = DependencyProperty.Register("RotateY", typeof(double), typeof(ImageViewer), new PropertyMetadata(0.0));
    public static readonly DependencyProperty SnapTickProperty = DependencyProperty.Register("SnapTick", typeof(bool), typeof(ImageViewer), new PropertyMetadata(false));
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(ImageSource), typeof(ImageViewer), new PropertyMetadata(null, SourceChanged));
    public static readonly DependencyProperty ToolBarVisibilityProperty = DependencyProperty.Register("ToolBarVisibility", typeof(Visibility), typeof(ImageViewer), new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty ZoomButtonVisibilityProperty = DependencyProperty.Register("ZoomButtonVisibility", typeof(Visibility), typeof(ImageViewer), new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register("Zoom", typeof(double), typeof(ImageViewer), new PropertyMetadata(1.0), ZoomValidateCallBack);
    private bool _isOnDrag;
    private DiffuseMaterial _panoramaBrush;
    private Point _startPoint;
    private double _startRotateX;
    private double _startRotateY;
    private Viewport3D _viewport;
    private bool disposedValue;

    static ImageViewer() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ImageViewer), new FrameworkPropertyMetadata(typeof(ImageViewer))); }

    public ImageViewer()
    {
        DosyaAç = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new() { Multiselect = false, Filter = "Resim Dosyaları (*.jpg;*.jpeg;*.tif;*.tiff;*.png)|*.jpg;*.jpeg;*.tif;*.tiff;*.png" };
                if (openFileDialog.ShowDialog() == true)
                {
                    ImageFilePath = openFileDialog.FileName;
                    PanoramaMode = false;
                }
            });

        ViewerBack = new RelayCommand<object>(parameter => Sayfa--, parameter => TiffDecoder is not null && Sayfa > 1 && Sayfa <= TiffDecoder.Frames.Count);

        ViewerNext = new RelayCommand<object>(parameter => Sayfa++, parameter => TiffDecoder is not null && Sayfa >= 1 && Sayfa < TiffDecoder.Frames.Count);

        Resize = new RelayCommand<object>(
            parameter =>
            {
                if (Source is not null)
                {
                    if (Orientation == FitImageOrientation.Width)
                    {
                        Zoom = ActualHeight / Source.Height;
                    }
                    if (Orientation == FitImageOrientation.Height)
                    {
                        Zoom = ActualWidth / Source.Width;
                    }
                    if (Zoom == 0 || Orientation == FitImageOrientation.None)
                    {
                        Zoom = 1;
                    }
                }
            },
            parameter => Source is not null);

        Yazdır = new RelayCommand<object>(
            async parameter =>
            {
                PrintDialog printDialog = new() { PageRangeSelection = PageRangeSelection.AllPages, UserPageRangeEnabled = true, MaxPage = (uint)(TiffDecoder?.Frames?.Count ?? 1), MinPage = 1 };

                if (printDialog.ShowDialog() != true)
                {
                    return;
                }
                TiffBitmapDecoder decoder = TiffDecoder;
                ReadOnlyCollection<BitmapFrame> frames = decoder?.Frames;
                int printdpi = PrintDpi;
                await Task.Run(
                    () =>
                    {
                        if (decoder is null)
                        {
                            Application.Current.Dispatcher
                            .Invoke(
                                () =>
                                {
                                    DrawingVisual visual = new();
                                    using (DrawingContext dc = visual.RenderOpen())
                                    {
                                        BitmapSource resizedImage = Source.Width > Source.Height
                                                                    ? ((BitmapSource)Source)?.Resize((int)printDialog.PrintableAreaHeight, (int)printDialog.PrintableAreaWidth, 90, PrintDpi, PrintDpi)
                                                                    : ((BitmapSource)Source)?.Resize((int)printDialog.PrintableAreaWidth, (int)printDialog.PrintableAreaHeight, 0, PrintDpi, PrintDpi);
                                        resizedImage.Freeze();
                                        dc.DrawImage(resizedImage, new Rect(0, 0, printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight));
                                    }

                                    printDialog.PrintVisual(visual, "Image Print");
                                });
                            return;
                        }

                        int startPage = printDialog.PageRangeSelection == PageRangeSelection.AllPages ? 0 : printDialog.PageRange.PageFrom - 1;
                        int endPage = printDialog.PageRangeSelection == PageRangeSelection.AllPages ? (frames?.Count ?? 1) - 1 : printDialog.PageRange.PageTo - 1;

                        Application.Current.Dispatcher
                        .Invoke(
                            () =>
                            {
                                FixedDocument document = PrintMultipleFixedDocumentPages(printDialog, startPage, endPage, frames, printdpi);
                                XpsDocumentWriter xpsWriter = PrintQueue.CreateXpsDocumentWriter(printDialog.PrintQueue);
                                xpsWriter.Write(document, printDialog.PrintTicket);
                            });
                    });
            },
            parameter => Source is not null);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public double Angle { get => (double)GetValue(AngleProperty); set => SetValue(AngleProperty, value); }

    public Visibility AngleButtonVisibility { get => (Visibility)GetValue(AngleButtonVisibilityProperty); set => SetValue(AngleButtonVisibilityProperty, value); }

    public int DecodeHeight { get => (int)GetValue(DecodeHeightProperty); set => SetValue(DecodeHeightProperty, value); }

    public ICommand DosyaAç { get; set; }

    public int[] DpiList { get; } = [12, 24, 36, 48, 72, 96, 120, 150, 200, 300, 400, 500, 600, 1200];

    public double Fov { get => (double)GetValue(FovProperty); set => SetValue(FovProperty, value); }

    public string ImageFilePath { get => (string)GetValue(ImageFilePathProperty); set => SetValue(ImageFilePathProperty, value); }

    public double MaxZoom { get => (double)GetValue(MaxZoomProperty); set => SetValue(MaxZoomProperty, value); }

    public double MinZoom { get => (double)GetValue(MinZoomProperty); set => SetValue(MinZoomProperty, value); }

    public Visibility OpenButtonVisibility
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(OpenButtonVisibility));
            }
        }
    } = Visibility.Collapsed;

    public FitImageOrientation Orientation { get => (FitImageOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    public int OriginalPixelHeight { get => (int)GetValue(OriginalPixelHeightProperty); set => SetValue(OriginalPixelHeightProperty, value); }

    public int OriginalPixelWidth { get => (int)GetValue(OriginalPixelWidthProperty); set => SetValue(OriginalPixelWidthProperty, value); }

    [Browsable(false)]
    public IEnumerable<int> Pages
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Pages));
            }
        }
    }

    public Visibility PanoramaButtonVisibility
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PanoramaButtonVisibility));
            }
        }
    }

    public bool PanoramaMode { get => (bool)GetValue(PanoramaModeProperty); set => SetValue(PanoramaModeProperty, value); }

    public Visibility PrintButtonVisibility
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PrintButtonVisibility));
            }
        }
    } = Visibility.Collapsed;

    public int PrintDpi { get => (int)GetValue(PrintDpiProperty); set => SetValue(PrintDpiProperty, value); }

    public bool PrintDpiSettingsListEnabled { get => (bool)GetValue(PrintDpiSettingsListEnabledProperty); set => SetValue(PrintDpiSettingsListEnabledProperty, value); }

    public ICommand Resize { get; set; }

    public double RotateX { get => (double)GetValue(RotateXProperty); set => SetValue(RotateXProperty, value); }

    public double RotateY { get => (double)GetValue(RotateYProperty); set => SetValue(RotateYProperty, value); }

    public int Sayfa
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Sayfa));
            }
        }
    } = 1;

    public bool SnapTick { get => (bool)GetValue(SnapTickProperty); set => SetValue(SnapTickProperty, value); }

    public ImageSource Source { get => (ImageSource)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    public Geometry3D SphereModel { get; set; } = MediaViewer.CreateGeometry();

    [Browsable(false)]
    public TiffBitmapDecoder TiffDecoder
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(TiffDecoder));
            }
        }
    }

    public Visibility TifNavigasyonButtonEtkin
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(TifNavigasyonButtonEtkin));
            }
        }
    } = Visibility.Collapsed;

    public bool ToolBarIsEnabled
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ToolBarIsEnabled));
            }
        }
    } = true;

    public Visibility ToolBarVisibility { get => (Visibility)GetValue(ToolBarVisibilityProperty); set => SetValue(ToolBarVisibilityProperty, value); }

    public virtual RelayCommand<object> ViewerBack { get; set; }

    public virtual RelayCommand<object> ViewerNext { get; set; }

    public ICommand Yazdır { get; }

    public double Zoom { get => (double)GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }

    public Visibility ZoomButtonVisibility { get => (Visibility)GetValue(ZoomButtonVisibilityProperty); set => SetValue(ZoomButtonVisibilityProperty, value); }

    public static async Task<BitmapImage> LoadImageAsync(string imagePath, int decodepixelheight = 0)
    {
        return await Task.Run(
            () =>
            {
                BitmapImage image = new();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.None;
                image.DecodePixelHeight = decodepixelheight;
                image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache | BitmapCreateOptions.DelayCreation;
                image.UriSource = new Uri(imagePath);
                image.EndInit();
                if (!image.IsFrozen && image.CanFreeze)
                {
                    image.Freeze();
                }

                return image;
            });
    }

    public static FixedDocument PrintMultipleFixedDocumentPages(PrintDialog printdialog, int başlangıç, int bitiş, IEnumerable<BitmapFrame> bitmappages, int dpi = 300)
    {
        FixedDocument fixedDocument = new();
        for (int i = başlangıç; i <= bitiş; i++)
        {
            BitmapSource imagesource = bitmappages.ElementAtOrDefault(i);
            if (imagesource.Width > imagesource.Height)
            {
                imagesource = imagesource.Resize(printdialog.PrintableAreaHeight, printdialog.PrintableAreaWidth, -90, dpi, dpi);
            }
            imagesource.Freeze();
            FixedPage fixedPage = new() { Width = printdialog.PrintableAreaWidth, Height = printdialog.PrintableAreaHeight };
            Image image = new() { Source = imagesource, Width = printdialog.PrintableAreaWidth, Height = printdialog.PrintableAreaHeight };
            _ = fixedPage.Children.Add(image);
            PageContent pageContent = new();
            ((IAddChild)pageContent).AddChild(fixedPage);
            _ = fixedDocument.Pages.Add(pageContent);
            GC.Collect();
        }

        return fixedDocument;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _viewport = GetTemplateChild("PanoramaViewPort") as Viewport3D;
        _panoramaBrush = GetTemplateChild("panoramaBrush") as DiffuseMaterial;
        if (_viewport is not null)
        {
            _viewport.MouseLeftButtonDown -= Viewport3D_MouseLeftButtonDown;
            _viewport.MouseLeftButtonDown += Viewport3D_MouseLeftButtonDown;
            _viewport.MouseLeftButtonUp -= Viewport3D_MouseLeftButtonUp;
            _viewport.MouseLeftButtonUp += Viewport3D_MouseLeftButtonUp;
            _viewport.MouseMove -= Viewport3D_MouseMove;
            _viewport.MouseMove += Viewport3D_MouseMove;
            _viewport.MouseWheel -= Viewport3D_MouseWheel;
            _viewport.MouseWheel += Viewport3D_MouseWheel;
        }
    }

    internal static Point3D GetPosition(double t, double y)
    {
        double r = Math.Sqrt(1 - (y * y));
        double x = r * Math.Cos(t);
        double z = r * Math.Sin(t);
        return new Point3D(x, y, z);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                ImageFilePath = null;
            }

            disposedValue = true;
        }
    }

    protected virtual async Task LoadImageAsync(string filepath, ImageViewer imageViewer)
    {
        if (filepath is not null && File.Exists(filepath))
        {
            int?[] size;
            switch (Path.GetExtension(filepath).ToLowerInvariant())
            {
                case ".tiff" or ".tif":
                    size = await GetImagePixelSizeAsync(filepath);
                    imageViewer.OriginalPixelHeight = size[0] ?? 0;
                    imageViewer.OriginalPixelWidth = size[1] ?? 0;
                    imageViewer.Sayfa = 1;
                    imageViewer.TiffDecoder = new TiffBitmapDecoder(new Uri(filepath), BitmapCreateOptions.None, BitmapCacheOption.None);
                    imageViewer.TifNavigasyonButtonEtkin = Visibility.Visible;
                    imageViewer.Source = imageViewer.TiffDecoder.Frames[0];
                    imageViewer.Pages = Enumerable.Range(1, imageViewer.TiffDecoder.Frames.Count);
                    return;

                case ".png" or ".jpg" or ".jpeg" or ".bmp":
                    size = await GetImagePixelSizeAsync(filepath);
                    imageViewer.OriginalPixelHeight = size[0] ?? 0;
                    imageViewer.OriginalPixelWidth = size[1] ?? 0;
                    imageViewer.TifNavigasyonButtonEtkin = Visibility.Collapsed;
                    imageViewer.Source = await LoadImageAsync(filepath, imageViewer.DecodeHeight);
                    return;
            }
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        else
        {
            Dispatcher.Invoke(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }

    private static async void DecodeHeightChangedAsync(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageViewer imageViewer)
        {
            string path = imageViewer.ImageFilePath;
            imageViewer.DecodeHeight = (int)e.NewValue;
            await imageViewer.LoadImageAsync(path, imageViewer);
        }
    }

    private static void FovChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageViewer viewer && e.NewValue is not null)
        {
            if ((double)e.NewValue < 1)
            {
                viewer.Fov = 1;
            }

            if ((double)e.NewValue > 140)
            {
                viewer.Fov = 140;
            }
        }
    }

    private static async Task<int?[]> GetImagePixelSizeAsync(string filepath)
    {
        return await Task.Run(
            () =>
            {
                BitmapDecoder bitmapframe = BitmapDecoder.Create(new Uri(filepath), BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreImageCache | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                return new[] { bitmapframe.Frames[0]?.PixelHeight, bitmapframe.Frames[0]?.PixelWidth };
            });
    }

    private static async void ImageFilePathChangedAsync(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageViewer imageViewer)
        {
            if (e.NewValue is string filepath && File.Exists(filepath))
            {
                await imageViewer.LoadImageAsync(filepath, imageViewer);
                return;
            }
            imageViewer.Source = null;
        }
    }

    private static void OrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageViewer imageViewer)
        {
            imageViewer?.Resize?.Execute(null);
        }
    }

    private static void PanoramaModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()) && d is ImageViewer viewer)
        {
            if ((bool)e.NewValue)
            {
                viewer._viewport.Visibility = Visibility.Visible;
                viewer._panoramaBrush.Brush = null;
                viewer._panoramaBrush.Brush = new ImageBrush(viewer.Source);
                viewer._panoramaBrush.Brush.Freeze();
                return;
            }

            viewer._viewport.Visibility = Visibility.Collapsed;
            viewer._panoramaBrush.Brush = null;
        }
    }

    private static void SourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageViewer imageViewer && e.NewValue is not null && e.NewValue is BitmapFrame bitmapFrame)
        {
            if (bitmapFrame.PixelHeight * 2 == bitmapFrame.PixelWidth)
            {
                imageViewer.PanoramaButtonVisibility = Visibility.Visible;
            }
            if (imageViewer.Orientation != FitImageOrientation.None)
            {
                imageViewer.Resize?.Execute(null);
            }
        }
    }

    private static bool ZoomValidateCallBack(object value)
    {
        double zoom = (double)value;
        return zoom >= 0.0;
    }

    private void Viewport3D_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isOnDrag = true;
        _startPoint = e.GetPosition(this);
        _startRotateX = RotateX;
        _startRotateY = RotateY;
    }

    private void Viewport3D_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => _isOnDrag = false;

    private void Viewport3D_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isOnDrag && e.LeftButton == MouseButtonState.Pressed)
        {
            Vector delta = _startPoint - e.GetPosition(this);
            RotateX = _startRotateX + (delta.X / ActualWidth * 360);
            RotateY = _startRotateY + (delta.Y / ActualHeight * 360);
        }
    }

    private void Viewport3D_MouseWheel(object sender, MouseWheelEventArgs e) => Fov -= e.Delta / 100d;
}
