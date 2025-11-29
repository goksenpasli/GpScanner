using Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using Size = System.Windows.Size;

namespace TwainControl;

public partial class DrawControl : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty TemporaryImageProperty = DependencyProperty.Register("TemporaryImage", typeof(ImageSource), typeof(DrawControl), new PropertyMetadata(null));
    private readonly List<Thumb> _corners = [];
    private readonly Line[] _quadLines = [ new(), new(), new(), new() ];
    private double stylusWidth = 3d;

    public DrawControl()
    {
        InitializeComponent();
        PropertyChanged += DrawControl_PropertyChanged;
        DependencyPropertyDescriptor.FromProperty(ZoomableInkCanvas.CurrentZoomProperty, typeof(ZoomableInkCanvas))?.AddValueChanged(Ink, OnZoomChanged);
        GenerateCustomCursor();
        Ink.PreviewMouseDown += Ink_PreviewMouseDown;

        foreach (Line ln in _quadLines)
        {
            ln.Stroke = System.Windows.Media.Brushes.Red;
            ln.StrokeThickness = 1;
            ln.SnapsToDevicePixels = true;
            _ = LineCanvas.Children.Add(ln);
        }

        SaveEditedImage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is not ScannedImage scannedImage)
                {
                    return;
                }
                if (SilentApply)
                {
                    scannedImage.Resim = await SaveInkCanvasToImage(TemporaryImage, Ink);
                    scannedImage.ScannedImageNotifyBrush = System.Windows.Media.Brushes.Yellow;
                    return;
                }
                if (MessageBox.Show($"{Translation.GetResStringValue("GRAPH")} {Translation.GetResStringValue("APPLY")}", Window.GetWindow(this)?.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
                {
                    return;
                }

                scannedImage.Resim = await SaveInkCanvasToImage(TemporaryImage, Ink);
                scannedImage.ScannedImageNotifyBrush = System.Windows.Media.Brushes.Yellow;
            },
            parameter => TemporaryImage is not null);

        SaveAllEditedImage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is not ObservableCollection<ScannedImage> scannedImages)
                {
                    return;
                }

                if (MessageBox.Show(
                    $"{Translation.GetResStringValue("GRAPH")} {Translation.GetResStringValue("ALL")} {Translation.GetResStringValue("APPLY")}",
                    Window.GetWindow(this)?.Title,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No) !=
                MessageBoxResult.Yes)
                {
                    return;
                }

                int count = scannedImages.Count;
                for (int i = 0; i < count; i++)
                {
                    if (scannedImages[i].Seçili)
                    {
                        TemporaryImage = scannedImages[i].Resim;
                        scannedImages[i].Resim = await SaveInkCanvasToImage(TemporaryImage, Ink);
                        scannedImages[i].ScannedImageNotifyBrush = System.Windows.Media.Brushes.Yellow;
                        TemporaryImage = null;
                        if (DataContext is TwainCtrl twainCtrl)
                        {
                            twainCtrl.AllRotateProgressValue = (i + 1) / (double)count;
                        }
                    }
                }
            },
            parameter => TemporaryImage is not null);

        ClearTemporaryImage = new RelayCommand<object>(
            parameter =>
            {
                if (MessageBox.Show($"{Translation.GetResStringValue("CLOSEFILE")}", Window.GetWindow(this)?.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    Ink?.Strokes?.Clear();
                    TemporaryImage = null;
                }
            },
            parameter => TemporaryImage is not null);

        FlattenImage = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is not ScannedImage scannedImage)
                {
                    return;
                }

                if (TemporaryImage == null)
                {
                    return;
                }

                BitmapSource rawBmp = (BitmapSource)TemporaryImage;
                FormatConvertedBitmap srcBmp = new(rawBmp, PixelFormats.Bgra32, null, 0);

                double scaleX = srcBmp.PixelWidth / OverlayCanvas.ActualWidth;
                double scaleY = srcBmp.PixelHeight / OverlayCanvas.ActualHeight;

                List<Point> points = _corners.ConvertAll(t => new Point((Canvas.GetLeft(t) + 12) * scaleX, (Canvas.GetTop(t) + 12) * scaleY));

                points = SortPoints(points);

                double w1 = PointDist(points[0], points[1]);
                double w2 = PointDist(points[3], points[2]);
                double h1 = PointDist(points[0], points[3]);
                double h2 = PointDist(points[1], points[2]);

                int finalW = (int)Math.Max(w1, w2);
                int finalH = (int)Math.Max(h1, h2);

                try
                {
                    WriteableBitmap result = PerspectiveWarpBilinear(srcBmp, points, finalW, finalH);
                    result.Freeze();
                    scannedImage.Resim = BitmapFrame.Create(result.ToBitmapImage());
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message);
                }
            },
            parameter => TemporaryImage is not null && _corners?.Count == 4);

        ResetThumbs = new RelayCommand<object>(
            parameter =>
            {
                OverlayCanvas.Children.Clear();
                _corners.Clear();
            },
            parameter => _corners?.Any() == true);

        CopyBitmapFile = new RelayCommand<object>(async parameter => Clipboard.SetImage(await SaveInkCanvasToImage(TemporaryImage, Ink)), parameter => TemporaryImage is not null);

        FitImage = new RelayCommand<object>(parameter => Ink.CurrentZoom = (ActualHeight / TemporaryImage?.Height) ?? 1, parameter => TemporaryImage is not null);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public RelayCommand<object> ClearTemporaryImage { get; }

    public RelayCommand<object> CopyBitmapFile { get; }

    public bool DrawControlContextMenu
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DrawControlContextMenu));
            }
        }
    }

    public Cursor DrawCursor
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DrawCursor));
            }
        }
    }

    public Ellipse Ellipse
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Ellipse));
            }
        }
    } = new();

    public RelayCommand<object> FitImage { get; }

    public bool FlatFixMode
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FlatFixMode));
            }
        }
    }

    public RelayCommand<object> FlattenImage { get; }

    public bool Highlighter
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Highlighter));
            }
        }
    }

    public bool IgnorePressure
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(IgnorePressure));
            }
        }
    }

    public bool Lock
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Lock));
            }
        }
    } = true;

    public Rectangle Rectangle
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Rectangle));
            }
        }
    } = new();

    public RelayCommand<object> ResetThumbs { get; }

    public RelayCommand<object> SaveAllEditedImage { get; }

    public RelayCommand<object> SaveEditedImage { get; }

    public SolidColorBrush SelectedBrush
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedBrush));
            }
        }
    }

    public string SelectedColor
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedColor));
                OnPropertyChanged(nameof(StylusHeight));
                OnPropertyChanged(nameof(stylusWidth));
            }
        }
    } = "Black";

    public StylusTip SelectedStylus
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedStylus));
                OnPropertyChanged(nameof(StylusHeight));
                OnPropertyChanged(nameof(stylusWidth));
            }
        }
    } = StylusTip.Ellipse;

    public bool SilentApply
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SilentApply));
            }
        }
    }

    public bool Smooth
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Smooth));
            }
        }
    }

    public double StylusHeight
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(StylusHeight));
            }
        }
    } = 3d;

    public double StylusWidth
    {
        get => stylusWidth;

        set
        {
            if (stylusWidth != value)
            {
                stylusWidth = value;
                OnPropertyChanged(nameof(StylusWidth));
            }
        }
    }

    public ImageSource TemporaryImage { get => (ImageSource)GetValue(TemporaryImageProperty); set => SetValue(TemporaryImageProperty, value); }

    public Cursor ConvertToCursor(FrameworkElement fe)
    {
        if (fe.Width < 1 || fe.Height < 1)
        {
            return Cursors.None;
        }

        fe.Arrange(new Rect(new Size(fe.Width, fe.Height)));
        RenderTargetBitmap rtb = new((int)fe.Width, (int)fe.Height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(fe);
        rtb.Freeze();
        using Bitmap img = rtb.BitmapSourceToBitmap();
        using Icon icon = Icon.FromHandle(img.GetHicon());
        return CursorInteropHelper.Create(new SafeIconHandle(icon.Handle));
    }

    protected override void OnDrop(DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ScannedImage)) is ScannedImage scannedImage && scannedImage?.Resim is not null)
        {
            TemporaryImage = scannedImage.Resim;
            FitImage.Execute(null);
            if (DataContext is TwainCtrl twainCtrl)
            {
                twainCtrl.TümününİşaretiniKaldır?.Execute(null);
            }
            scannedImage.Seçili = true;
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        Dispatcher dispatcher = Application.Current?.Dispatcher;
        if (dispatcher?.CheckAccess() == true)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        else
        {
            _ = dispatcher?.InvokeAsync(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }

    private void AddCorner(Point p)
    {
        Thumb t = new() { Style = (Style)TryFindResource("CornerThumb") };

        t.DragDelta += (s, e) =>
                       {
                           double left = Canvas.GetLeft(t) + e.HorizontalChange;
                           double top = Canvas.GetTop(t) + e.VerticalChange;
                           Canvas.SetLeft(t, left);
                           Canvas.SetTop(t, top);
                           UpdateQuadrilateralLines();
                       };

        Canvas.SetLeft(t, p.X - 12);
        Canvas.SetTop(t, p.Y - 12);
        _corners.Add(t);
        _ = OverlayCanvas.Children.Add(t);
        UpdateQuadrilateralLines();
    }

    private void DrawControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "SelectedStylus")
        {
            DrawingAttribute.StylusTip = SelectedStylus;
        }

        if (e.PropertyName is "StylusWidth")
        {
            DrawingAttribute.Width = Lock ? StylusHeight = StylusWidth : StylusWidth;
            GenerateCustomCursor();
        }

        if (e.PropertyName is "StylusHeight")
        {
            DrawingAttribute.Height = Lock ? StylusWidth = StylusHeight : StylusHeight;
            GenerateCustomCursor();
        }

        if (e.PropertyName is "Smooth")
        {
            DrawingAttribute.FitToCurve = Smooth;
        }

        if (e.PropertyName is "IgnorePressure")
        {
            DrawingAttribute.IgnorePressure = IgnorePressure;
        }

        if (e.PropertyName is "Highlighter")
        {
            DrawingAttribute.IsHighlighter = Highlighter;
        }

        if (e.PropertyName is "SelectedColor")
        {
            DrawingAttribute.Color = (Color)ColorConverter.ConvertFromString(SelectedColor);
        }
    }

    private double[] FindHomography(List<Point> p1, List<Point> p2)
    {
        double[][] system = new double[8][];
        for (int i = 0; i < 4; i++)
        {
            double x = p1[i].X, y = p1[i].Y;
            double u = p2[i].X, v = p2[i].Y;
            system[2 * i] = [ x, y, 1, 0, 0, 0, -x * u, -y * u, u ];
            system[(2 * i) + 1] = [ 0, 0, 0, x, y, 1, -x * v, -y * v, v ];
        }

        double[] s = GaussianElimination(system);
        return[ s[0], s[1], s[2], s[3], s[4], s[5], s[6], s[7], 1.0 ];
    }

    private double[] GaussianElimination(double[][] A)
    {
        const int n = 8;
        for (int i = 0; i < n; i++)
        {
            int max = i;
            for (int k = i + 1; k < n; k++)
            {
                if (Math.Abs(A[k][i]) > Math.Abs(A[max][i]))
                {
                    max = k;
                }
            }

            (A[max], A[i]) = (A[i], A[max]);
            for (int k = i + 1; k < n; k++)
            {
                double t = A[k][i] / A[i][i];
                for (int j = i; j <= n; j++)
                {
                    A[k][j] -= t * A[i][j];
                }
            }
        }
        double[] x = new double[n];
        for (int i = n - 1; i >= 0; i--)
        {
            double sum = 0;
            for (int j = i + 1; j < n; j++)
            {
                sum += A[i][j] * x[j];
            }

            x[i] = (A[i][n] - sum) / A[i][i];
        }
        return x;
    }

    private void GenerateCustomCursor()
    {
        PresentationSource source = PresentationSource.FromVisual(this);
        double m11 = source?.CompositionTarget?.TransformToDevice.M11 ?? 1;
        double m22 = source?.CompositionTarget?.TransformToDevice.M22 ?? 1;
        SelectedBrush = new SolidColorBrush(DrawingAttribute.Color);
        double width = StylusWidth * Ink.CurrentZoom * m11;
        double height = StylusHeight * Ink.CurrentZoom * m22;
        Ellipse.Width = width;
        Ellipse.Height = height;
        Ellipse.Fill = SelectedBrush;
        Rectangle.Width = width;
        Rectangle.Height = height;
        Rectangle.Fill = SelectedBrush;
        DrawCursor = SelectedStylus == StylusTip.Ellipse ? ConvertToCursor(Ellipse) : ConvertToCursor(Rectangle);
    }

    private void Ink_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.RightButton == MouseButtonState.Pressed)
        {
            DrawControlContextMenu = Keyboard.Modifiers == ModifierKeys.Shift;
            if (DrawControlContextMenu)
            {
                Point mousemovecoord = e.GetPosition(Scr);
                mousemovecoord.X += Scr.HorizontalOffset;
                mousemovecoord.Y += Scr.VerticalOffset;
                double widthmultiply = ((BitmapSource)Img.ImageSource).PixelWidth / Scr.ExtentWidth;
                double heightmultiply = ((BitmapSource)Img.ImageSource).PixelHeight / Scr.ExtentHeight;
                if (Scr.ExtentWidth < Scr.ViewportWidth)
                {
                    mousemovecoord.X -= (Scr.ViewportWidth - Scr.ExtentWidth) / 2;
                }
                if (Scr.ExtentHeight < Scr.ViewportHeight)
                {
                    mousemovecoord.Y -= (Scr.ViewportHeight - Scr.ExtentHeight) / 2;
                }
                Int32Rect sourceRect = new((int)(mousemovecoord.X * widthmultiply), (int)(mousemovecoord.Y * heightmultiply), 1, 1);
                CroppedBitmap croppedbitmap = new((BitmapSource)Img.ImageSource, sourceRect);
                byte[] pixels = new byte[4];
                croppedbitmap.CopyPixels(pixels, 4, 0);
                croppedbitmap.Freeze();
                DrawingAttribute.Color = Color.FromRgb(pixels[2], pixels[1], pixels[0]);
                SelectedBrush = new SolidColorBrush(DrawingAttribute.Color);
                GenerateCustomCursor();
            }
        }
    }

    private void Ink_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            Ink.CurrentZoom = e.Delta > 0 ? Ink.CurrentZoom + .005 : Ink.CurrentZoom + -.005;
            Ink.CurrentZoom = Math.Max(Ink.MinZoom, Math.Min(Ink.MaxZoom, Ink.CurrentZoom));
        }
    }

    private void OnZoomChanged(object sender, EventArgs e) => GenerateCustomCursor();

    private void OverlayCanvas_MouseClick(object sender, MouseButtonEventArgs e)
    {
        if (TemporaryImage == null)
        {
            return;
        }

        if (_corners.Count >= 4)
        {
            return;
        }
        Point clickPoint = e.GetPosition(OverlayCanvas);
        AddCorner(clickPoint);
    }

    private WriteableBitmap PerspectiveWarpBilinear(BitmapSource src, List<Point> srcPts, int w, int h)
    {
        List<Point> dstPts = [ new Point(0, 0), new Point(w, 0), new Point(w, h), new Point(0, h) ];

        double[] M = FindHomography(dstPts, srcPts);

        WriteableBitmap wb = new(w, h, 96, 96, PixelFormats.Bgra32, null);

        int srcW = src.PixelWidth;
        int srcH = src.PixelHeight;
        int srcStride = srcW * 4;
        byte[] srcPixels = new byte[srcH * srcStride];
        src.CopyPixels(srcPixels, srcStride, 0);

        int dstStride = w * 4;
        byte[] dstPixels = new byte[h * dstStride];

        unsafe
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double div = (M[6] * x) + (M[7] * y) + 1;
                    if (Math.Abs(div) < 1e-9)
                    {
                        continue;
                    }

                    double srcX = ((M[0] * x) + (M[1] * y) + M[2]) / div;
                    double srcY = ((M[3] * x) + (M[4] * y) + M[5]) / div;

                    if (srcX >= 0 && srcX < srcW - 1 && srcY >= 0 && srcY < srcH - 1)
                    {
                        int x0 = (int)srcX;
                        int y0 = (int)srcY;
                        int x1 = x0 + 1;
                        int y1 = y0 + 1;

                        double dx = srcX - x0;
                        double dy = srcY - y0;
                        double w00 = (1 - dx) * (1 - dy);
                        double w10 = dx * (1 - dy);
                        double w01 = (1 - dx) * dy;
                        double w11 = dx * dy;

                        int i00 = (y0 * srcStride) + (x0 * 4);
                        int i10 = (y0 * srcStride) + (x1 * 4);
                        int i01 = (y1 * srcStride) + (x0 * 4);
                        int i11 = (y1 * srcStride) + (x1 * 4);

                        for (int c = 0; c < 3; c++)
                        {
                            double val = (srcPixels[i00 + c] * w00) + (srcPixels[i10 + c] * w10) + (srcPixels[i01 + c] * w01) + (srcPixels[i11 + c] * w11);
                            dstPixels[(y * dstStride) + (x * 4) + c] = (byte)val;
                        }
                        dstPixels[(y * dstStride) + (x * 4) + 3] = 255;
                    }
                }
            }
        }

        wb.WritePixels(new Int32Rect(0, 0, w, h), dstPixels, dstStride, 0);
        return wb;
    }

    private double PointDist(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private Task<BitmapFrame> SaveInkCanvasToImage(ImageSource imageSource, Visual visual)
    {
        return Task.Run(
            () => imageSource is not BitmapSource temporaryimage
                  ? null
                  : Dispatcher.Invoke(
                () =>
                {
                    RenderTargetBitmap renderTargetBitmap = new(temporaryimage.PixelWidth, temporaryimage.PixelHeight, 96, 96, PixelFormats.Pbgra32);
                    DrawingVisual dv = new();
                    using (DrawingContext ctx = dv.RenderOpen())
                    {
                        ctx?.DrawRectangle(new VisualBrush(visual), null, new Rect(0, 0, temporaryimage.PixelWidth, temporaryimage.PixelHeight));
                    }
                    renderTargetBitmap?.Render(dv);
                    renderTargetBitmap?.Freeze();
                    BitmapFrame image = BitmapFrame.Create(renderTargetBitmap.ToBitmapImage());
                    image?.Freeze();
                    return image;
                }));
    }

    private void SetLine(Line line, Point p1, Point p2)
    {
        line.X1 = p1.X;
        line.Y1 = p1.Y;
        line.X2 = p2.X;
        line.Y2 = p2.Y;
    }

    private List<Point> SortPoints(List<Point> pts)
    {
        List<Point> sortedY = [ .. pts.OrderBy(p => p.Y) ];
        List<Point> top = [ .. sortedY.Take(2).OrderBy(p => p.X) ];
        List<Point> bottom = [ .. sortedY.Skip(2).OrderBy(p => p.X) ];

        return[ top[0], top[1], bottom[1], bottom[0] ];
    }

    private void UpdateQuadrilateralLines()
    {
        if (_corners == null || _corners.Count != 4)
        {
            return;
        }

        List<Point> pts = _corners.ConvertAll(c => new Point(Canvas.GetLeft(c) + 12, Canvas.GetTop(c) + 12));

        pts = SortPoints(pts);

        SetLine(_quadLines[0], pts[0], pts[1]);
        SetLine(_quadLines[1], pts[1], pts[2]);
        SetLine(_quadLines[2], pts[2], pts[3]);
        SetLine(_quadLines[3], pts[3], pts[0]);

        Rect canvasRect = new(0, 0, OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight);
        RectangleGeometry outer = new(canvasRect);
        PathFigure fig = new() { StartPoint = pts[0], IsClosed = true, Segments = { new LineSegment(pts[1], true), new LineSegment(pts[2], true), new LineSegment(pts[3], true) } };
        PathGeometry quadGeo = new();
        quadGeo.Figures.Add(fig);
        MaskPath.Data = new CombinedGeometry(GeometryCombineMode.Exclude, outer, quadGeo);
    }
}