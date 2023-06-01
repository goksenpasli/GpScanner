
using Extensions;
using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TwainControl.Properties;

namespace TwainControl
{
    public partial class DrawControl : UserControl, INotifyPropertyChanged
    {
        public DrawControl()
        {
            InitializeComponent();
            PropertyChanged += DrawControl_PropertyChanged;

            GenerateCustomCursor();

            SaveEditedImage = new RelayCommand<object>(
                parameter =>
                {
                    if(parameter is BitmapFrame bitmapFrame &&
                        MessageBox.Show(
                        $"{Translation.GetResStringValue("GRAPH")} {Translation.GetResStringValue("APPLY")}",
                        Application.Current.MainWindow.Title,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question,
                        MessageBoxResult.No) ==
                        MessageBoxResult.Yes)
                    {
                        EditingImage = SaveInkCanvasToImage();
                    }
                },
                parameter => TemporaryImage is not null);

            LoadImage = new RelayCommand<object>(parameter => TemporaryImage = EditingImage, parameter => EditingImage is not null);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public Cursor ConvertToCursor(FrameworkElement fe)
        {
            if(fe.Width < 1 || fe.Height < 1)
            {
                return Cursors.None;
            }
            fe.Arrange(new Rect(new Size(fe.Width, fe.Height)));
            RenderTargetBitmap rtb = new((int)fe.Width, (int)fe.Height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(fe);
            rtb.Freeze();
            using System.Drawing.Icon icon = System.Drawing.Icon.FromHandle(rtb.BitmapSourceToBitmap().GetHicon());
            return CursorInteropHelper.Create(new SafeIconHandle(icon.Handle));
        }

        public BitmapFrame SaveInkCanvasToImage()
        {
            if(DataContext is TwainCtrl twainctrl)
            {
                RenderTargetBitmap renderTargetBitmap = new((int)Ink.ActualWidth, (int)Ink.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                Rect bounds = VisualTreeHelper.GetDescendantBounds(Ink);
                DrawingVisual dv = new();
                using(DrawingContext ctx = dv.RenderOpen())
                {
                    ctx.DrawRectangle(new VisualBrush(Ink), null, bounds);
                }
                renderTargetBitmap.Render(dv);
                renderTargetBitmap.Freeze();
                BitmapSource thumbnail = Ink.ActualWidth < Ink.ActualHeight
                    ? renderTargetBitmap.Resize(
                        Settings.Default.PreviewWidth,
                        Settings.Default.PreviewWidth / twainctrl.SelectedPaper.Width * twainctrl.SelectedPaper.Height)
                    : renderTargetBitmap.Resize(
                        Settings.Default.PreviewWidth,
                        Settings.Default.PreviewWidth / twainctrl.SelectedPaper.Height * twainctrl.SelectedPaper.Width);
                thumbnail.Freeze();
                BitmapFrame image = BitmapFrame.Create(renderTargetBitmap, thumbnail);
                image.Freeze();
                return image;
            }
            return null;
        }

        public Cursor DrawCursor
        {
            get => drawCursor;
            set
            {
                if(drawCursor != value)
                {
                    drawCursor = value;
                    OnPropertyChanged(nameof(DrawCursor));
                }
            }
        }

        public BitmapFrame EditingImage { get => (BitmapFrame)GetValue(EditingImageProperty); set => SetValue(EditingImageProperty, value); }

        public bool Highlighter
        {
            get => highlighter;
            set
            {
                if(highlighter != value)
                {
                    highlighter = value;
                    OnPropertyChanged(nameof(Highlighter));
                }
            }
        }

        public bool IgnorePressure
        {
            get => ıgnorePressure;
            set
            {
                if(ıgnorePressure != value)
                {
                    ıgnorePressure = value;
                    OnPropertyChanged(nameof(IgnorePressure));
                }
            }
        }

        public RelayCommand<object> LoadImage { get; }

        public bool Lock
        {
            get => @lock;
            set
            {
                if(@lock != value)
                {
                    @lock = value;
                    OnPropertyChanged(nameof(Lock));
                }
            }
        }

        public RelayCommand<object> SaveEditedImage { get; }

        public string SelectedColor
        {
            get => selectedColor;
            set
            {
                if(selectedColor != value)
                {
                    selectedColor = value;
                    OnPropertyChanged(nameof(SelectedColor));
                    OnPropertyChanged(nameof(StylusHeight));
                    OnPropertyChanged(nameof(stylusWidth));
                }
            }
        }

        public StylusTip SelectedStylus
        {
            get => selectedStylus;
            set
            {
                if(selectedStylus != value)
                {
                    selectedStylus = value;
                    OnPropertyChanged(nameof(SelectedStylus));
                    OnPropertyChanged(nameof(StylusHeight));
                    OnPropertyChanged(nameof(stylusWidth));
                }
            }
        }

        public bool Smooth
        {
            get => smooth;
            set
            {
                if(smooth != value)
                {
                    smooth = value;
                    OnPropertyChanged(nameof(Smooth));
                }
            }
        }

        public double StylusHeight
        {
            get => stylusHeight;
            set
            {
                if(stylusHeight != value)
                {
                    stylusHeight = value;
                    OnPropertyChanged(nameof(StylusHeight));
                }
            }
        }

        public double StylusWidth
        {
            get => stylusWidth;
            set
            {
                if(stylusWidth != value)
                {
                    stylusWidth = value;
                    OnPropertyChanged(nameof(StylusWidth));
                }
            }
        }

        public ImageSource TemporaryImage
        {
            get => temporaryImage;
            set
            {
                if(temporaryImage != value)
                {
                    temporaryImage = value;
                    OnPropertyChanged(nameof(TemporaryImage));
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName = null) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

        private void DrawControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName is "SelectedStylus")
            {
                DrawingAttribute.StylusTip = SelectedStylus;
            }
            if(e.PropertyName is "StylusWidth")
            {
                DrawingAttribute.Width = Lock ? (StylusHeight = StylusWidth) : StylusWidth;
                GenerateCustomCursor();
            }
            if(e.PropertyName is "StylusHeight")
            {
                DrawingAttribute.Height = Lock ? (StylusWidth = StylusHeight) : StylusHeight;
                GenerateCustomCursor();
            }
            if(e.PropertyName is "Smooth")
            {
                DrawingAttribute.FitToCurve = Smooth;
            }
            if(e.PropertyName is "IgnorePressure")
            {
                DrawingAttribute.IgnorePressure = IgnorePressure;
            }
            if(e.PropertyName is "Highlighter")
            {
                DrawingAttribute.IsHighlighter = Highlighter;
            }
            if(e.PropertyName is "SelectedColor")
            {
                DrawingAttribute.Color = (Color)ColorConverter.ConvertFromString(SelectedColor);
            }
        }

        private void GenerateCustomCursor()
        {
            Ellipse ellipse = new() { Fill = new SolidColorBrush(DrawingAttribute.Color), Width = StylusWidth * Ink.CurrentZoom, Height = StylusHeight * Ink.CurrentZoom };
            Rectangle rect = new() { Fill = new SolidColorBrush(DrawingAttribute.Color), Width = StylusWidth * Ink.CurrentZoom, Height = StylusHeight * Ink.CurrentZoom };
            DrawCursor = (SelectedStylus == StylusTip.Ellipse) ? ConvertToCursor(ellipse) : ConvertToCursor(rect);
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            OnPropertyChanged(nameof(StylusHeight));
            OnPropertyChanged(nameof(stylusWidth));
        }

        public static readonly DependencyProperty EditingImageProperty = DependencyProperty.Register(
            "EditingImage",
            typeof(BitmapFrame),
            typeof(DrawControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        private Cursor drawCursor;

        private bool highlighter;

        private bool ıgnorePressure;

        private bool @lock = true;

        private string selectedColor = "Black";

        private StylusTip selectedStylus = StylusTip.Ellipse;

        private bool smooth;

        private double stylusHeight = 2d;

        private double stylusWidth = 2d;

        private ImageSource temporaryImage;

        public class SafeIconHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            private SafeIconHandle() : base(true)
            {
            }

            public SafeIconHandle(IntPtr hIcon) : base(true) { SetHandle(hIcon); }

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool DestroyIcon([In] IntPtr hIcon);

            protected override bool ReleaseHandle() { return DestroyIcon(handle); }
        }
    }
}
