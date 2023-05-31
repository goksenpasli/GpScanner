
using Extensions;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TwainControl.Properties;

namespace TwainControl
{
    public partial class DrawControl : UserControl, INotifyPropertyChanged
    {
        public DrawControl()
        {
            InitializeComponent();
            PropertyChanged += DrawControl_PropertyChanged;
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
            }
            if(e.PropertyName is "StylusHeight")
            {
                DrawingAttribute.Height = Lock ? (StylusHeight = StylusWidth) : StylusHeight;
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

        public static readonly DependencyProperty EditingImageProperty = DependencyProperty.Register(
            "EditingImage",
            typeof(BitmapFrame),
            typeof(DrawControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        private bool highlighter;

        private bool ıgnorePressure;

        private bool @lock = true;

        private string selectedColor = "Black";

        private StylusTip selectedStylus = StylusTip.Ellipse;

        private bool smooth;

        private double stylusHeight = 2d;

        private double stylusWidth = 2d;

        private ImageSource temporaryImage;
    }
}
