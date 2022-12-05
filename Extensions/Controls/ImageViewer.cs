using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Extensions.Controls;
using Microsoft.Win32;

namespace Extensions
{
    public enum FitImageOrientation
    {
        Width = 0,

        Height = 1
    }

    [TemplatePart(Name = "PanoramaViewPort", Type = typeof(Viewport3D))]
    [TemplatePart(Name = "panoramaBrush", Type = typeof(DiffuseMaterial))]
    public class ImageViewer : Control, INotifyPropertyChanged, IDisposable
    {
        public static readonly DependencyProperty AngleProperty = DependencyProperty.Register("Angle", typeof(double), typeof(ImageViewer), new PropertyMetadata(0.0));

        public static readonly DependencyProperty DecodeHeightProperty = DependencyProperty.Register("DecodeHeight", typeof(int), typeof(ImageViewer), new PropertyMetadata(300, DecodeHeightChanged));

        public static readonly DependencyProperty FovProperty = DependencyProperty.Register("Fov", typeof(double), typeof(ImageViewer), new PropertyMetadata(95d, FovChanged));

        public static readonly DependencyProperty ImageFilePathProperty = DependencyProperty.Register("ImageFilePath", typeof(string), typeof(ImageViewer), new PropertyMetadata(null, ImageFilePathChanged));

        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register("Orientation", typeof(FitImageOrientation), typeof(ImageViewer), new PropertyMetadata(FitImageOrientation.Width, Changed));

        public static readonly DependencyProperty PanoramaModeProperty = DependencyProperty.Register("PanoramaMode", typeof(bool), typeof(ImageViewer), new PropertyMetadata(PanoramaModeChanged));

        public static readonly DependencyProperty RotateXProperty = DependencyProperty.Register("RotateX", typeof(double), typeof(ImageViewer), new PropertyMetadata(0.0));

        public static readonly DependencyProperty RotateYProperty = DependencyProperty.Register("RotateY", typeof(double), typeof(ImageViewer), new PropertyMetadata(0.0));

        public static readonly DependencyProperty SnapTickProperty = DependencyProperty.Register("SnapTick", typeof(bool), typeof(ImageViewer), new PropertyMetadata(false));

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(ImageSource), typeof(ImageViewer), new PropertyMetadata(null, SourceChanged));

        public static readonly DependencyProperty ToolBarVisibilityProperty = DependencyProperty.Register("ToolBarVisibility", typeof(Visibility), typeof(ImageViewer), new PropertyMetadata(Visibility.Visible));

        public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register("Zoom", typeof(double), typeof(ImageViewer), new PropertyMetadata(1.0), ZoomValidateCallBack);

        static ImageViewer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ImageViewer), new FrameworkPropertyMetadata(typeof(ImageViewer)));
        }

        public ImageViewer()
        {
            DosyaAç = new RelayCommand<object>(parameter =>
            {
                OpenFileDialog openFileDialog = new() { Multiselect = false, Filter = "Resim Dosyaları (*.jpg;*.jpeg;*.tif;*.tiff;*.png)|*.jpg;*.jpeg;*.tif;*.tiff;*.png" };
                if (openFileDialog.ShowDialog() == true)
                {
                    ImageFilePath = openFileDialog.FileName;
                    PanoramaMode = false;
                }
            });

            ViewerBack = new RelayCommand<object>(parameter => Sayfa--, parameter => Decoder != null && Sayfa > 1 && Sayfa <= Decoder.Frames.Count);

            ViewerNext = new RelayCommand<object>(parameter => Sayfa++, parameter => Decoder != null && Sayfa >= 1 && Sayfa < Decoder.Frames.Count);

            Resize = new RelayCommand<object>(parameter =>
            {
                if (Source is not null)
                {
                    Zoom = (Orientation != FitImageOrientation.Width) ? ActualHeight / Source.Height : ActualWidth / Source.Width;
                }
            }, parameter => Source is not null);

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

            PropertyChanged += ImageViewer_PropertyChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public double Angle
        {
            get => (double)GetValue(AngleProperty);
            set => SetValue(AngleProperty, value);
        }

        public int DecodeHeight
        {
            get => (int)GetValue(DecodeHeightProperty);
            set => SetValue(DecodeHeightProperty, value);
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

        public virtual ICommand DosyaAç { get; set; }

        public double Fov { get => (double)GetValue(FovProperty); set => SetValue(FovProperty, value); }

        public string ImageFilePath
        {
            get => (string)GetValue(ImageFilePathProperty);
            set => SetValue(ImageFilePathProperty, value);
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

        public FitImageOrientation Orientation
        {
            get { return (FitImageOrientation)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }

        public Visibility OrijinalResimDosyaAçButtonVisibility
        {
            get => orijinalResimDosyaAçButtonVisibility;

            set
            {
                if (orijinalResimDosyaAçButtonVisibility != value)
                {
                    orijinalResimDosyaAçButtonVisibility = value;
                    OnPropertyChanged(nameof(OrijinalResimDosyaAçButtonVisibility));
                }
            }
        }

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

        public Visibility PanoramaButtonVisibility
        {
            get => panoramaButtonVisibility;

            set
            {
                if (panoramaButtonVisibility != value)
                {
                    panoramaButtonVisibility = value;
                    OnPropertyChanged(nameof(PanoramaButtonVisibility));
                }
            }
        }

        public bool PanoramaMode
        {
            get => (bool)GetValue(PanoramaModeProperty);
            set => SetValue(PanoramaModeProperty, value);
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

        public virtual ICommand Resize { get; set; }

        public double RotateX { get => (double)GetValue(RotateXProperty); set => SetValue(RotateXProperty, value); }

        public double RotateY { get => (double)GetValue(RotateYProperty); set => SetValue(RotateYProperty, value); }

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

        public Geometry3D SphereModel { get; set; } = MediaViewer.CreateGeometry();

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

        public bool ToolBarIsEnabled
        {
            get => toolBarIsEnabled;

            set
            {
                if (toolBarIsEnabled != value)
                {
                    toolBarIsEnabled = value;
                    OnPropertyChanged(nameof(ToolBarIsEnabled));
                }
            }
        }

        public Visibility ToolBarVisibility
        {
            get => (Visibility)GetValue(ToolBarVisibilityProperty);
            set => SetValue(ToolBarVisibilityProperty, value);
        }

        public virtual ICommand ViewerBack { get; set; }

        public virtual ICommand ViewerNext { get; set; }

        public ICommand Yazdır { get; }

        public double Zoom
        {
            get => (double)GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _viewport = GetTemplateChild("PanoramaViewPort") as Viewport3D;
            _panoramaBrush = GetTemplateChild("panoramaBrush") as DiffuseMaterial;
            if (_viewport != null)
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

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool _isOnDrag;

        private DiffuseMaterial _panoramaBrush;

        private Point _startPoint;

        private double _startRotateX;

        private double _startRotateY;

        private Viewport3D _viewport;

        private TiffBitmapDecoder decoder;

        private bool disposedValue;

        private Visibility openButtonVisibility = Visibility.Collapsed;

        private Visibility orijinalResimDosyaAçButtonVisibility;

        private IEnumerable<int> pages;

        private Visibility panoramaButtonVisibility;

        private Visibility printButtonVisibility = Visibility.Collapsed;

        private int sayfa = 1;

        private Visibility tifNavigasyonButtonEtkin = Visibility.Collapsed;

        private bool toolBarIsEnabled = true;

        private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageViewer imageViewer)
            {
                imageViewer.Resize.Execute(null);
            }
        }

        private static void DecodeHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageViewer imageViewer)
            {
                string path = imageViewer.ImageFilePath;
                imageViewer.DecodeHeight = (int)e.NewValue;
                LoadImage(path, imageViewer);
            }
        }

        private static void FovChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageViewer viewer && e.NewValue != null)
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

        private static void ImageFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageViewer imageViewer)
            {
                if (e.NewValue is string filepath)
                {
                    LoadImage(filepath, imageViewer);
                }
                else
                {
                    imageViewer.Source = null;
                }
            }
        }

        private static void LoadImage(string filepath, ImageViewer imageViewer)
        {
            if (filepath is not null && File.Exists(filepath))
            {
                switch (Path.GetExtension(filepath).ToLower())
                {
                    case ".tiff" or ".tif":
                        imageViewer.Sayfa = 1;
                        imageViewer.Decoder = new TiffBitmapDecoder(new Uri(filepath), BitmapCreateOptions.None, BitmapCacheOption.None);
                        imageViewer.TifNavigasyonButtonEtkin = Visibility.Visible;
                        imageViewer.Source = imageViewer.Decoder.Frames[0];
                        imageViewer.Pages = Enumerable.Range(1, imageViewer.Decoder.Frames.Count);
                        return;

                    case ".png" or ".jpg" or ".jpeg" or ".bmp":
                        {
                            imageViewer.TifNavigasyonButtonEtkin = Visibility.Collapsed;
                            BitmapImage image = new();
                            image.BeginInit();
                            image.DecodePixelHeight = imageViewer.DecodeHeight;
                            image.CacheOption = BitmapCacheOption.None;
                            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache | BitmapCreateOptions.DelayCreation;
                            image.UriSource = new Uri(filepath);
                            image.EndInit();
                            if (!image.IsFrozen && image.CanFreeze)
                            {
                                image.Freeze();
                            }
                            imageViewer.Source = image;
                            return;
                        }
                }
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
                }
                else
                {
                    viewer._viewport.Visibility = Visibility.Collapsed;
                    viewer._panoramaBrush.Brush = null;
                }
            }
        }

        private static void SourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageViewer imageViewer && e.NewValue is not null)
            {
                imageViewer.Resize.Execute(null);
                if (e.NewValue is BitmapFrame bitmapFrame)
                {
                    if (bitmapFrame.PixelHeight < bitmapFrame.PixelWidth)
                    {
                        imageViewer.Orientation = FitImageOrientation.Width;
                    }
                    if (bitmapFrame.PixelHeight * 2 == bitmapFrame.PixelWidth)
                    {
                        imageViewer.PanoramaButtonVisibility = Visibility.Visible;
                    }
                }
            }
        }

        private static bool ZoomValidateCallBack(object value)
        {
            double zoom = (double)value;
            return zoom >= 0.0;
        }

        private void ImageViewer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "Sayfa" && Decoder is not null)
            {
                Source = Decoder.Frames[Sayfa - 1];
            }
        }

        private void Viewport3D_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isOnDrag = true;
            _startPoint = e.GetPosition(this);
            _startRotateX = RotateX;
            _startRotateY = RotateY;
        }

        private void Viewport3D_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isOnDrag = false;
        }

        private void Viewport3D_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isOnDrag && e.LeftButton == MouseButtonState.Pressed)
            {
                Vector delta = _startPoint - e.GetPosition(this);
                RotateX = _startRotateX + (delta.X / ActualWidth * 360);
                RotateY = _startRotateY + (delta.Y / ActualHeight * 360);
            }
        }

        private void Viewport3D_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Fov -= e.Delta / 100;
        }
    }
}