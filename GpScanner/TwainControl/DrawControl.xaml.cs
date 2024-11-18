using Extensions;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Rectangle = System.Windows.Shapes.Rectangle;
using Size = System.Windows.Size;

namespace TwainControl;

public partial class DrawControl : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty TemporaryImageProperty = DependencyProperty.Register("TemporaryImage", typeof(ImageSource), typeof(DrawControl), new PropertyMetadata(null));
    private double stylusWidth = 3d;

    public DrawControl()
    {
        InitializeComponent();
        PropertyChanged += DrawControl_PropertyChanged;
        DependencyPropertyDescriptor.FromProperty(ZoomableInkCanvas.CurrentZoomProperty, typeof(ZoomableInkCanvas))?.AddValueChanged(Ink, OnZoomChanged);
        GenerateCustomCursor();
        Ink.PreviewMouseDown += Ink_PreviewMouseDown;

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

        CopyBitmapFile = new RelayCommand<object>(async parameter => Clipboard.SetImage(await SaveInkCanvasToImage(TemporaryImage, Ink)), parameter => TemporaryImage is not null);

        FitImage = new RelayCommand<object>(parameter => Ink.CurrentZoom = ActualHeight / TemporaryImage?.Height ?? 1, parameter => TemporaryImage is not null);
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
        using Icon icon = Icon.FromHandle(rtb.BitmapSourceToBitmap().GetHicon());
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

    protected virtual void OnPropertyChanged(string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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
                System.Windows.Point mousemovecoord = e.GetPosition(Scr);
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
}