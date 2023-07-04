using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Extensions.Controls;

public partial class MediaViewer : UserControl, INotifyPropertyChanged
{
    public MediaViewer()
    {
        InitializeComponent();
        Player.Pause();
        Player.MediaEnded += MediaElement_MediaEnded;
        PanoramaViewPort.Visibility = Visibility.Collapsed;
        DataContext = this;
        PropertyChanged += MediaViewer_PropertyChanged;

        GoToFrame = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is TimeSpan timeSpan)
                {
                    MediaPosition = timeSpan;
                }
            },
            parameter => GetMediaState(Player) == MediaState.Play);

        LoadSubtitle = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new() { Multiselect = false, Filter = "Srt Dosyası (*.srt)|*.srt" };
                if (openFileDialog.ShowDialog() == true)
                {
                    SubtitleFilePath = openFileDialog.FileName;
                    ParsedSubtitle = ParseSrtFile(SubtitleFilePath);
                }
            },
            parameter => true);

        OpenFile = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new()
                {
                    Multiselect = false,
                    Filter = VideoFileExtensions
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    MediaDataFilePath = openFileDialog.FileName;
                }
            },
            parameter => true);

        CaptureImage = new RelayCommand<object>(
            parameter =>
            {
                string picturesfolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                byte[] data = grid.ToRenderTargetBitmap().ToTiffJpegByteArray(ExtensionMethods.Format.Jpg);
                string dosya = picturesfolder.SetUniqueFile("Resim", "jpg");
                File.WriteAllBytes(dosya, data);
                ExtensionMethods.OpenFolderAndSelectItem(picturesfolder, dosya);
                OsdText = "Görüntü Yakalandı";
                GC.Collect();
            },
            parameter => Player?.NaturalVideoWidth > 0 && MediaDataFilePath != null);

        CaptureThumbnail = new RelayCommand<object>(
            async parameter =>
            {
                string picturesfolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                MediaVolume = 0;
                long timemultiplier = EndTimeSpan.Ticks / (ThumbWidthCount * ThumbHeightCount);
                byte[] imgdata;

                Player.Play();

                if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                {
                    string singlefile = null;
                    for (int i = 1; i <= ThumbHeightCount * ThumbWidthCount; i++)
                    {
                        Player.Position = new TimeSpan(i * timemultiplier);
                        await Task.Delay(MillisecondsDelay);
                        imgdata = grid.ToRenderTargetBitmap().ToTiffJpegByteArray(ExtensionMethods.Format.Jpg);
                        singlefile = picturesfolder.SetUniqueFile("Resim", "jpg");
                        File.WriteAllBytes(singlefile, imgdata);
                    }

                    ExtensionMethods.OpenFolderAndSelectItem(picturesfolder, singlefile);
                    GC.Collect();
                    return;
                }

                UniformGrid uniformgrid = new() { Rows = ThumbHeightCount, Columns = ThumbWidthCount };
                double oran = 1d / ThumbWidthCount;
                for (int i = 1; i <= ThumbHeightCount * ThumbWidthCount; i++)
                {
                    Player.Position = new TimeSpan(i * timemultiplier);
                    await Task.Delay(MillisecondsDelay);

                    imgdata = ThumbApplyEffects
                        ? grid.ToRenderTargetBitmap().Resize(oran).ToTiffJpegByteArray(ExtensionMethods.Format.Jpg)
                        : Player.ToRenderTargetBitmap().Resize(oran).ToTiffJpegByteArray(ExtensionMethods.Format.Jpg);

                    Grid imagegrid = GenerateImageGrid();
                    Image image = GenerateImage(imgdata, ThumbMargin);
                    image.SetValue(Grid.RowProperty, 0);
                    _ = imagegrid.Children.Add(image);

                    if (ThumbShowTime)
                    {
                        TextBlock textBlock = GenerateWhiteTextBlock(Player.Position.ToString());
                        textBlock.SetValue(Grid.RowProperty, 1);
                        _ = imagegrid.Children.Add(textBlock);
                    }

                    _ = uniformgrid.Children.Add(imagegrid);
                }

                string dosya = picturesfolder.SetUniqueFile("Resim", "jpg");
                File.WriteAllBytes(dosya,
                    uniformgrid.ToRenderTargetBitmap().ToTiffJpegByteArray(ExtensionMethods.Format.Jpg));
                ExtensionMethods.OpenFolderAndSelectItem(picturesfolder, dosya);
                MediaVolume = 1;
                GC.Collect();
            },
            parameter => Player?.NaturalVideoWidth > 0 && MediaDataFilePath != null);

        AddToPlaylist = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new()
                {
                    Multiselect = true,
                    Filter = VideoFileExtensions
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    foreach (string item in openFileDialog.FileNames)
                    {
                        if (!PlayList.Contains(item))
                        {
                            PlayList.Add(item);
                        }
                    }
                }
            },
            parameter => true);

        SetSubtitleMargin = new RelayCommand<object>(
            parameter =>
            {
                Thickness defaultsubtitlethickness = new(
                    SubTitleMargin.Left,
                    SubTitleMargin.Top,
                    SubTitleMargin.Right,
                    SubTitleMargin.Bottom);
                if (parameter is object content)
                {
                    switch (content)
                    {
                        case "6":
                            defaultsubtitlethickness.Bottom -= 10;
                            break;

                        case "5":
                            defaultsubtitlethickness.Bottom += 10;
                            break;

                        case "4":
                            defaultsubtitlethickness.Left += 10;
                            break;

                        case "3":
                            defaultsubtitlethickness.Left -= 10;
                            break;

                        case "=":
                            defaultsubtitlethickness.Left = 0;
                            defaultsubtitlethickness.Bottom = 0;
                            break;
                    }

                    SubTitleMargin = defaultsubtitlethickness;
                }
            },
            parameter => true);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public RelayCommand<object> AddToPlaylist { get; }

    [Description("Video Controls")]
    [Category("Controls")]
    public double Angle {
        get => (double)GetValue(AngleProperty);
        set => SetValue(AngleProperty, value);
    }

    [Description("Video Effects")]
    [Category("Effects")]
    public bool ApplyBw {
        get => (bool)GetValue(ApplyBwProperty);
        set => SetValue(ApplyBwProperty, value);
    }

    [Description("Video Effects")]
    [Category("Effects")]
    public bool ApplyEmboss {
        get => (bool)GetValue(ApplyEmbossProperty);
        set => SetValue(ApplyEmbossProperty, value);
    }

    [Description("Video Effects")]
    [Category("Effects")]
    public bool ApplyGrayscale {
        get => (bool)GetValue(ApplyGrayscaleProperty);
        set => SetValue(ApplyGrayscaleProperty, value);
    }

    [Description("Video Effects")]
    [Category("Effects")]
    public bool ApplyPixelate {
        get => (bool)GetValue(ApplyPixelateProperty);
        set => SetValue(ApplyPixelateProperty, value);
    }

    [Description("Video Effects")]
    [Category("Effects")]
    public bool ApplySharpen {
        get => (bool)GetValue(ApplySharpenProperty);
        set => SetValue(ApplySharpenProperty, value);
    }

    [Description("Subtitle Controls")]
    [Category("Subtitle")]
    public bool AutoLoadSameNameSubtitleFile {
        get => (bool)GetValue(AutoLoadSameNameSubtitleFileProperty);
        set => SetValue(AutoLoadSameNameSubtitleFileProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    public bool AutoPlay {
        get => (bool)GetValue(AutoPlayProperty);
        set => SetValue(AutoPlayProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    public bool AutoSkipNextVideo { get; set; }

    [Description("Subtitle Translate")]
    [Category("Translate")]
    public bool AutoTranslate {
        get => (bool)GetValue(AutoTranslateProperty);
        set => SetValue(AutoTranslateProperty, value);
    }

    [Description("Video Effects")]
    [Category("Effects")]
    public double BlurAmount {
        get => (double)GetValue(BlurAmountProperty);
        set => SetValue(BlurAmountProperty, value);
    }

    [Description("Video Effects")]
    [Category("Effects")]
    public bool BlurColor {
        get => (bool)GetValue(BlurColorProperty);
        set => SetValue(BlurColorProperty, value);
    }

    [Description("Video Effects")]
    [Category("Effects")]
    public double BwAmount {
        get => (double)GetValue(BwAmountProperty);
        set => SetValue(BwAmountProperty, value);
    }

    public RelayCommand<object> CaptureImage { get; }

    public RelayCommand<object> CaptureThumbnail { get; }

    [Description("Video Controls")]
    [Category("Controls")]
    public Visibility ContextMenuVisibility {
        get => (Visibility)GetValue(ContextMenuVisibilityProperty);
        set => SetValue(ContextMenuVisibilityProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    public Visibility ControlVisible {
        get => (Visibility)GetValue(ControlVisibleProperty);
        set => SetValue(ControlVisibleProperty, value);
    }

    [Description("Subtitle Translate")]
    [Category("Translate")]
    public string ÇevrilenDil { get; set; } = "en";

    [Description("Video Controls")]
    [Category("Controls")]
    public TimeSpan EndTimeSpan {
        get => (TimeSpan)GetValue(EndTimeSpanProperty);
        set => SetValue(EndTimeSpanProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    public double FlipX {
        get => (double)GetValue(FlipXProperty);
        set => SetValue(FlipXProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    public double FlipY {
        get => (double)GetValue(FlipYProperty);
        set => SetValue(FlipYProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    public int ForwardBackwardSkipSecond { get; set; } = 30;

    [Description("Video Controls")]
    [Category("Controls")]
    public double Fov {
        get => (double)GetValue(FovProperty);
        set => SetValue(FovProperty, value);
    }

    public RelayCommand<object> GoToFrame { get; }

    [Description("Video Effects")]
    [Category("Effects")]
    public bool InvertColor {
        get => (bool)GetValue(InvertColorProperty);
        set => SetValue(InvertColorProperty, value);
    }

    public RelayCommand<object> LoadSubtitle { get; }

    [Description("Video Controls")]
    [Category("Controls")]
    public string MediaDataFilePath {
        get => (string)GetValue(MediaDataFilePathProperty);
        set => SetValue(MediaDataFilePathProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    public TimeSpan MediaPosition {
        get => (TimeSpan)GetValue(MediaPositionProperty);
        set => SetValue(MediaPositionProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    public double MediaVolume {
        get => (double)GetValue(MediaVolumeProperty);
        set => SetValue(MediaVolumeProperty, value);
    }

    [Description("Subtitle Translate")]
    [Category("Translate")]
    public string MevcutDil { get; set; } = "auto";

    [Description("Video Controls")]
    [Category("Controls")]
    public Visibility OpenButtonVisibility {
        get => (Visibility)GetValue(OpenButtonVisibilityProperty);
        set => SetValue(OpenButtonVisibilityProperty, value);
    }

    public RelayCommand<object> OpenFile { get; }

    [Description("Video Controls")]
    [Category("Controls")]
    public int OsdDisplayTime {
        get => (int)GetValue(OsdDisplayTimeProperty);
        set => SetValue(OsdDisplayTimeProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    [Browsable(false)]
    public string OsdText {
        get => (string)GetValue(OsdTextProperty);
        set => SetValue(OsdTextProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    public Visibility OsdTextVisibility {
        get => (Visibility)GetValue(OsdTextVisibilityProperty);
        set => SetValue(OsdTextVisibilityProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    public bool PanoramaMode {
        get => (bool)GetValue(PanoramaModeProperty);
        set => SetValue(PanoramaModeProperty, value);
    }

    [Description("Subtitle Controls")]
    [Category("Subtitle")]
    [Browsable(false)]
    public ObservableCollection<SrtContent> ParsedSubtitle {
        get => parsedSubtitle;

        set {
            if (parsedSubtitle != value)
            {
                parsedSubtitle = value;
                OnPropertyChanged(nameof(ParsedSubtitle));
            }
        }
    }

    [Description("Video Effects")]
    [Category("Effects")]
    public Size PixelateSize {
        get => (Size)GetValue(PixelateSizeProperty);
        set => SetValue(PixelateSizeProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    [Browsable(false)]
    public ObservableCollection<string> PlayList { get; set; } = new();

    [Description("Video Controls")]
    [Category("Controls")]
    [Browsable(false)]
    public double RotateX {
        get => (double)GetValue(RotateXProperty);
        set => SetValue(RotateXProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    [Browsable(false)]
    public double RotateY {
        get => (double)GetValue(RotateYProperty);
        set => SetValue(RotateYProperty, value);
    }

    public string SearchSubtitle {
        get => searchSubtitle;

        set {
            if (searchSubtitle != value)
            {
                searchSubtitle = value;
                OnPropertyChanged(nameof(SearchSubtitle));
            }
        }
    }

    public RelayCommand<object> SetSubtitleMargin { get; }

    [Description("Video Effects")]
    [Category("Effects")]
    public double SharpenAmount {
        get => (double)GetValue(SharpenAmountProperty);
        set => SetValue(SharpenAmountProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    public Visibility SliderControlVisible {
        get => (Visibility)GetValue(SliderControlVisibleProperty);
        set => SetValue(SliderControlVisibleProperty, value);
    }

    public Geometry3D SphereModel { get; set; } = CreateGeometry();

    [Description("Subtitle Controls")]
    [Category("Subtitle")]
    [Browsable(false)]
    public string SubTitle {
        get => (string)GetValue(SubTitleProperty);
        set => SetValue(SubTitleProperty, value);
    }

    [Description("Subtitle Controls")]
    [Category("Subtitle")]
    public Brush SubTitleColor {
        get => (Brush)GetValue(SubTitleColorProperty);
        set => SetValue(SubTitleColorProperty, value);
    }

    [Description("Subtitle Controls")]
    [Category("Subtitle")]
    public string SubtitleFilePath {
        get => (string)GetValue(SubtitleFilePathProperty);
        set => SetValue(SubtitleFilePathProperty, value);
    }

    [Description("Subtitle Controls")]
    [Category("Subtitle")]
    public HorizontalAlignment SubTitleHorizontalAlignment {
        get => (HorizontalAlignment)GetValue(SubTitleHorizontalAlignmentProperty);
        set => SetValue(SubTitleHorizontalAlignmentProperty, value);
    }

    [Description("Subtitle Controls")]
    [Category("Subtitle")]
    public Thickness SubTitleMargin {
        get => (Thickness)GetValue(SubTitleMarginProperty);
        set => SetValue(SubTitleMarginProperty, value);
    }

    [Description("Subtitle Controls")]
    [Category("Subtitle")]
    public double SubTitleSize {
        get => (double)GetValue(SubTitleSizeProperty);
        set => SetValue(SubTitleSizeProperty, value);
    }

    [Description("Subtitle Controls")]
    [Category("Subtitle")]
    public bool SubtitleTooltipEnabled {
        get => (bool)GetValue(SubtitleTooltipEnabledProperty);
        set => SetValue(SubtitleTooltipEnabledProperty, value);
    }

    [Description("Subtitle Controls")]
    [Category("Subtitle")]
    public VerticalAlignment SubTitleVerticalAlignment {
        get => (VerticalAlignment)GetValue(SubTitleVerticalAlignmentProperty);
        set => SetValue(SubTitleVerticalAlignmentProperty, value);
    }

    [Description("Subtitle Controls")]
    [Category("Subtitle")]
    public Visibility SubTitleVisibility {
        get => (Visibility)GetValue(SubTitleVisibilityProperty);
        set => SetValue(SubTitleVisibilityProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    public bool ThumbApplyEffects {
        get => (bool)GetValue(ThumbApplyEffectsProperty);
        set => SetValue(ThumbApplyEffectsProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    public int ThumbHeightCount {
        get => (int)GetValue(ThumbHeightCountProperty);
        set => SetValue(ThumbHeightCountProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    public Thickness ThumbMargin {
        get => (Thickness)GetValue(ThumbMarginProperty);
        set => SetValue(ThumbMarginProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    public bool ThumbShowTime {
        get => (bool)GetValue(ThumbShowTimeProperty);
        set => SetValue(ThumbShowTimeProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    public int ThumbWidthCount {
        get => (int)GetValue(ThumbWidthCountProperty);
        set => SetValue(ThumbWidthCountProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    public Visibility TimeDisplayVisibility {
        get => (Visibility)GetValue(TimeDisplayVisibilityProperty);
        set => SetValue(TimeDisplayVisibilityProperty, value);
    }

    [Description("Subtitle Controls")]
    [Category("Subtitle")]
    public string TooltipOriginalSubtitle {
        get => (string)GetValue(TooltipOriginalSubtitleProperty);
        set => SetValue(TooltipOriginalSubtitleProperty, value);
    }

    [Description("Video Controls")]
    [Category("Controls")]
    public Stretch VideoStretch {
        get => (Stretch)GetValue(VideoStretchProperty);
        set => SetValue(VideoStretchProperty, value);
    }

    public static Geometry3D CreateGeometry()
    {
        const int tDiv = 64;
        const int yDiv = 64;
        const double maxTheta = 360.0 / 180.0 * Math.PI;
        const double minY = -1.0;
        const double maxY = 1.0;
        const double dt = maxTheta / tDiv;
        const double dy = (maxY - minY) / yDiv;
        MeshGeometry3D mesh = new();
        for (int yi = 0; yi <= yDiv; yi++)
        {
            double y = minY + (yi * dy);
            for (int ti = 0; ti <= tDiv; ti++)
            {
                double t = ti * dt;
                mesh.Positions.Add(GetPosition(t, y));
                mesh.Normals.Add(GetNormal(t, y));
                mesh.TextureCoordinates.Add(GetTextureCoordinate(t, y));
            }
        }

        for (int yi = 0; yi < yDiv; yi++)
        {
            for (int ti = 0; ti < tDiv; ti++)
            {
                int x0 = ti;
                int x1 = ti + 1;
                int y0 = yi * (tDiv + 1);
                int y1 = (yi + 1) * (tDiv + 1);
                mesh.TriangleIndices.Add(x0 + y0);
                mesh.TriangleIndices.Add(x0 + y1);
                mesh.TriangleIndices.Add(x1 + y0);
                mesh.TriangleIndices.Add(x1 + y0);
                mesh.TriangleIndices.Add(x0 + y1);
                mesh.TriangleIndices.Add(x1 + y1);
            }
        }

        mesh.Freeze();
        return mesh;
    }

    public ObservableCollection<SrtContent> ParseSrtFile(string filepath)
    {
        try
        {
            ObservableCollection<SrtContent> content = new();
            const string pattern = "<[/]?[ib]>";
            foreach (string element in File.ReadAllText(filepath, Encoding.GetEncoding("UTF-8"))
                         .Split(new[] { "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] lines = element.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                content.Add(
                    new SrtContent
                    {
                        StartTime = TimeSpan.Parse(lines[1].Substring(0, lines[1].LastIndexOf("-->")).Trim()),
                        EndTime = TimeSpan.Parse(lines[1].Substring(lines[1].LastIndexOf("-->") + 3).Trim()),
                        Text = Regex.Replace(string.Concat(lines.Skip(2).Take(lines.Length - 2)), pattern,
                            string.Empty),
                        Segment = lines[0]
                    });
            }

            return content;
        }
        catch (Exception ex)
        {
            throw new ArgumentException(nameof(filepath), ex);
        }
    }

    public static readonly DependencyProperty AngleProperty =
        DependencyProperty.Register("Angle", typeof(double), typeof(MediaViewer), new PropertyMetadata(0.0d));

    public static readonly DependencyProperty ApplyBwProperty =
        DependencyProperty.Register("ApplyBw", typeof(bool), typeof(MediaViewer), new PropertyMetadata(false));

    public static readonly DependencyProperty ApplyEmbossProperty =
        DependencyProperty.Register("ApplyEmboss", typeof(bool), typeof(MediaViewer), new PropertyMetadata(false));

    public static readonly DependencyProperty ApplyGrayscaleProperty =
        DependencyProperty.Register("ApplyGrayscale", typeof(bool), typeof(MediaViewer), new PropertyMetadata(false));

    public static readonly DependencyProperty ApplyPixelateProperty =
        DependencyProperty.Register("ApplyPixelate", typeof(bool), typeof(MediaViewer), new PropertyMetadata(false));

    public static readonly DependencyProperty ApplySharpenProperty =
        DependencyProperty.Register("ApplySharpen", typeof(bool), typeof(MediaViewer), new PropertyMetadata(false));

    public static readonly DependencyProperty AutoLoadSameNameSubtitleFileProperty =
        DependencyProperty.Register("AutoLoadSameNameSubtitleFile", typeof(bool), typeof(MediaViewer),
            new PropertyMetadata(false));

    public static readonly DependencyProperty AutoPlayProperty = DependencyProperty.Register(
        "AutoPlay",
        typeof(bool),
        typeof(MediaViewer),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, AutoplayChanged));

    public static readonly DependencyProperty AutoTranslateProperty =
        DependencyProperty.Register("AutoTranslate", typeof(bool), typeof(MediaViewer), new PropertyMetadata(false));

    public static readonly DependencyProperty BlurAmountProperty =
        DependencyProperty.Register("BlurAmount", typeof(double), typeof(MediaViewer), new PropertyMetadata(5.0d));

    public static readonly DependencyProperty BlurColorProperty =
        DependencyProperty.Register("BlurColor", typeof(bool), typeof(MediaViewer), new PropertyMetadata(false));

    public static readonly DependencyProperty BwAmountProperty =
        DependencyProperty.Register("BwAmount", typeof(double), typeof(MediaViewer), new PropertyMetadata(0.6D));

    public static readonly DependencyProperty ContextMenuVisibilityProperty =
        DependencyProperty.Register("ContextMenuVisibility", typeof(Visibility), typeof(MediaElement),
            new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty ControlVisibleProperty = DependencyProperty.Register(
        "ControlVisible",
        typeof(Visibility),
        typeof(MediaViewer),
        new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty EndTimeSpanProperty = DependencyProperty.Register(
        "EndTimeSpan",
        typeof(TimeSpan),
        typeof(MediaViewer),
        new PropertyMetadata(TimeSpan.Zero));

    public static readonly DependencyProperty FlipXProperty =
        DependencyProperty.Register("FlipX", typeof(double), typeof(MediaViewer), new PropertyMetadata(1.0d));

    public static readonly DependencyProperty FlipYProperty =
        DependencyProperty.Register("FlipY", typeof(double), typeof(MediaViewer), new PropertyMetadata(1.0d));

    public static readonly DependencyProperty FovProperty = DependencyProperty.Register("Fov", typeof(double),
        typeof(MediaViewer), new PropertyMetadata(95d, FovChanged));

    public static readonly DependencyProperty InvertColorProperty =
        DependencyProperty.Register("InvertColor", typeof(bool), typeof(MediaViewer), new PropertyMetadata(false));

    public static readonly DependencyProperty MediaDataFilePathProperty =
        DependencyProperty.Register("MediaDataFilePath", typeof(string), typeof(MediaViewer),
            new PropertyMetadata(null, MediaDataFilePathChanged));

    public static readonly DependencyProperty MediaPositionProperty = DependencyProperty.Register(
        "MediaPosition",
        typeof(TimeSpan),
        typeof(MediaViewer),
        new PropertyMetadata(TimeSpan.Zero, MediaPositionChanged));

    public static readonly DependencyProperty MediaVolumeProperty = DependencyProperty.Register(
        "MediaVolume",
        typeof(double),
        typeof(MediaViewer),
        new PropertyMetadata(1d, MediaVolumeChanged));

    public static readonly DependencyProperty OpenButtonVisibilityProperty =
        DependencyProperty.Register("OpenButtonVisibility", typeof(Visibility), typeof(MediaViewer),
            new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty OsdDisplayTimeProperty =
        DependencyProperty.Register("OsdDisplayTime", typeof(int), typeof(MediaViewer), new PropertyMetadata(3));

    public static readonly DependencyProperty OsdTextProperty = DependencyProperty.Register(
        "OsdText",
        typeof(string),
        typeof(MediaViewer),
        new PropertyMetadata(null, OsdTextChanged));

    public static readonly DependencyProperty OsdTextVisibilityProperty =
        DependencyProperty.Register("OsdTextVisibility", typeof(Visibility), typeof(MediaViewer),
            new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty PanoramaModeProperty = DependencyProperty.Register(
        "PanoramaMode",
        typeof(bool),
        typeof(MediaViewer),
        new PropertyMetadata(PanoramaModeChanged));

    public static readonly DependencyProperty PixelateSizeProperty = DependencyProperty.Register(
        "PixelateSize",
        typeof(Size),
        typeof(MediaViewer),
        new PropertyMetadata(new Size(60, 40)));

    public static readonly DependencyProperty RotateXProperty =
        DependencyProperty.Register("RotateX", typeof(double), typeof(MediaViewer), new PropertyMetadata(0.0));

    public static readonly DependencyProperty RotateYProperty =
        DependencyProperty.Register("RotateY", typeof(double), typeof(MediaViewer), new PropertyMetadata(0.0));

    public static readonly DependencyProperty SharpenAmountProperty =
        DependencyProperty.Register("SharpenAmount", typeof(double), typeof(MediaViewer), new PropertyMetadata(1.0d));

    public static readonly DependencyProperty SliderControlVisibleProperty =
        DependencyProperty.Register("SliderControlVisible", typeof(Visibility), typeof(MediaViewer),
            new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty SubTitleColorProperty = DependencyProperty.Register(
        "SubTitleColor",
        typeof(Brush),
        typeof(MediaViewer),
        new PropertyMetadata(Brushes.White));

    public static readonly DependencyProperty SubtitleFilePathProperty = DependencyProperty.Register(
        "SubtitleFilePath",
        typeof(string),
        typeof(MediaViewer),
        new PropertyMetadata(null, SubtitleFilePathChanged));

    public static readonly DependencyProperty SubTitleHorizontalAlignmentProperty =
        DependencyProperty.Register("SubTitleHorizontalAlignment", typeof(HorizontalAlignment), typeof(MediaViewer),
            new PropertyMetadata(HorizontalAlignment.Center));

    public static readonly DependencyProperty SubTitleMarginProperty = DependencyProperty.Register(
        "SubTitleMargin",
        typeof(Thickness),
        typeof(MediaViewer),
        new PropertyMetadata(new Thickness(0d, 0d, 0d, 10d)));

    public static readonly DependencyProperty SubTitleProperty = DependencyProperty.Register("SubTitle", typeof(string),
        typeof(MediaViewer), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubTitleSizeProperty =
        DependencyProperty.Register("SubTitleSize", typeof(double), typeof(MediaViewer), new PropertyMetadata(32.0d));

    public static readonly DependencyProperty SubtitleTooltipEnabledProperty =
        DependencyProperty.Register("SubtitleTooltipEnabled", typeof(bool), typeof(MediaViewer),
            new PropertyMetadata(false));

    public static readonly DependencyProperty SubTitleVerticalAlignmentProperty =
        DependencyProperty.Register("SubTitleVerticalAlignment", typeof(VerticalAlignment), typeof(MediaViewer),
            new PropertyMetadata(VerticalAlignment.Bottom));

    public static readonly DependencyProperty SubTitleVisibilityProperty =
        DependencyProperty.Register("SubTitleVisibility", typeof(Visibility), typeof(MediaViewer),
            new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty ThumbApplyEffectsProperty =
        DependencyProperty.Register("ThumbApplyEffects", typeof(bool), typeof(MediaViewer),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ThumbHeightCountProperty =
        DependencyProperty.Register("ThumbHeightCount", typeof(int), typeof(MediaViewer), new PropertyMetadata(1));

    public static readonly DependencyProperty ThumbMarginProperty = DependencyProperty.Register(
        "ThumbMargin",
        typeof(Thickness),
        typeof(MediaViewer),
        new PropertyMetadata(new Thickness(5)));

    public static readonly DependencyProperty ThumbShowTimeProperty =
        DependencyProperty.Register("ThumbShowTime", typeof(bool), typeof(MediaViewer), new PropertyMetadata(true));

    public static readonly DependencyProperty ThumbWidthCountProperty =
        DependencyProperty.Register("ThumbWidthCount", typeof(int), typeof(MediaViewer), new PropertyMetadata(1));

    public static readonly DependencyProperty TimeDisplayVisibilityProperty =
        DependencyProperty.Register("TimeDisplayVisibility", typeof(Visibility), typeof(MediaViewer),
            new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty TooltipOriginalSubtitleProperty =
        DependencyProperty.Register("TooltipOriginalSubtitle", typeof(string), typeof(MediaViewer),
            new PropertyMetadata(null));

    public static readonly DependencyProperty VideoStretchProperty = DependencyProperty.Register(
        "VideoStretch",
        typeof(Stretch),
        typeof(MediaViewer),
        new PropertyMetadata(Stretch.Uniform));

    internal static Point3D GetPosition(double t, double y)
    {
        double r = Math.Sqrt(1 - (y * y));
        double x = r * Math.Cos(t);
        double z = r * Math.Sin(t);
        return new Point3D(x, y, z);
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static void AutoplayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()) && d is MediaViewer viewer &&
            (bool)e.NewValue && viewer.MediaDataFilePath != null)
        {
            viewer.Player.Play();
        }
    }

    private static void FovChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MediaViewer viewer && e.NewValue != null)
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

    private static Grid GenerateImageGrid()
    {
        Grid visualgrid = new();
        visualgrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        visualgrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        return visualgrid;
    }

    private static string GetAutoSubtitlePath(string uriString)
    {
        string autosrtfile = Path.ChangeExtension(uriString, ".srt");
        return File.Exists(autosrtfile) ? autosrtfile : null;
    }

    private static Vector3D GetNormal(double t, double y)
    {
        return (Vector3D)GetPosition(t, y);
    }

    private static Point GetTextureCoordinate(double t, double y)
    {
        Matrix TYtoUV = new();
        TYtoUV.Scale(1 / (2 * Math.PI), -0.5);
        Point p = new(t, y);
        p *= TYtoUV;
        return p;
    }

    private static void MediaDataFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()) && d is MediaViewer viewer &&
            e.NewValue != null)
        {
            try
            {
                string uriString = (string)e.NewValue;
                if (!File.Exists(uriString))
                {
                    return;
                }

                viewer.Player.Source = new Uri(uriString);
                if (viewer.AutoLoadSameNameSubtitleFile)
                {
                    viewer.SubtitleFilePath = GetAutoSubtitlePath(uriString);
                }

                if (viewer.AutoPlay)
                {
                    viewer.Player.Play();
                    viewer.OsdText = "Çalıyor";
                }

                viewer.Player.MediaOpened -= MediaOpened;
                viewer.Player.MediaOpened += MediaOpened;

                void MediaOpened(object f, RoutedEventArgs g)
                {
                    if (f is MediaElement mediaelement && mediaelement.NaturalDuration.HasTimeSpan)
                    {
                        viewer.EndTimeSpan = mediaelement.NaturalDuration.TimeSpan;
                        timer = new DispatcherTimer(
                            TimeSpan.FromSeconds(1),
                            DispatcherPriority.Normal,
                            (s, _) => viewer.MediaPosition = mediaelement.Position,
                            Dispatcher.CurrentDispatcher);
                        timer.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException(nameof(viewer.Player.Source), ex);
            }
        }
    }

    private static void MediaPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MediaViewer viewer && e.NewValue != null && !sliderdragging)
        {
            TimeSpan position = (TimeSpan)e.NewValue;
            viewer.Player.Position = position;
            if (viewer.SubTitleVisibility == Visibility.Visible && viewer.ParsedSubtitle is not null)
            {
                RenderSubtitle(viewer, position);
            }
        }
    }

    private static void MediaVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MediaViewer viewer && e.NewValue != null)
        {
            viewer.Player.Volume = (double)e.NewValue;
            viewer.OsdText = $"Ses: {(int)(viewer.Player.Volume * 100)}";
        }
    }

    private static void OsdTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MediaViewer mediaViewer && e.NewValue is not null)
        {
            osdtimer.Interval = new TimeSpan(0, 0, mediaViewer.OsdDisplayTime);
            osdtimer.Start();
            osdtimer.Tick -= OsdTextChange;
            osdtimer.Tick += OsdTextChange;

            void OsdTextChange(object s, EventArgs e)
            {
                osdtimer.Stop();
                mediaViewer.OsdText = null;
            }
        }
    }

    private static void PanoramaModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()) && d is MediaViewer viewer)
        {
            if ((bool)e.NewValue)
            {
                viewer.PanoramaViewPort.Visibility = Visibility.Visible;
                viewer.panoramaBrush.Brush = null;
                viewer.panoramaBrush.Brush = new VisualBrush(viewer.Player);
                return;
            }

            viewer.PanoramaViewPort.Visibility = Visibility.Collapsed;
            viewer.panoramaBrush.Brush = null;
        }
    }

    private static void RenderSubtitle(MediaViewer viewer, TimeSpan position)
    {
        foreach (SrtContent srtcontent in viewer.ParsedSubtitle)
        {
            if (position > srtcontent.StartTime && position < srtcontent.EndTime)
            {
                srtcontent.BackgroundColor = Brushes.Yellow;
                viewer.MediaViewerSubtitleControl.LbSubtitle.ScrollIntoView(srtcontent);
                if (viewer.AutoTranslate)
                {
                    viewer.TooltipOriginalSubtitle = srtcontent.Text;
                    viewer.SubTitle = TranslateViewModel
                        .DileÇevirAsync(srtcontent.Text, viewer.MevcutDil, viewer.ÇevrilenDil).ConfigureAwait(false)
                        .GetAwaiter().GetResult();
                }
                else
                {
                    viewer.SubTitle = srtcontent.Text;
                }
            }

            if (position > srtcontent.EndTime)
            {
                srtcontent.BackgroundColor = null;
                viewer.TooltipOriginalSubtitle = string.Empty;
                viewer.SubTitle = string.Empty;
            }
        }
    }

    private static void SubtitleFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()) && d is MediaViewer viewer &&
            e.NewValue != null)
        {
            viewer.ParsedSubtitle = viewer.ParseSrtFile((string)e.NewValue);
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (MediaDataFilePath != null)
        {
            Player.Position = Player.Position.Subtract(new TimeSpan(0, 0, ForwardBackwardSkipSecond));
            OsdText = "Geri";
        }
    }

    private void FlipHor_Click(object sender, RoutedEventArgs e)
    {
        FlipX = FlipX == 1 ? -1 : 1;
    }

    private void FlipVer_Click(object sender, RoutedEventArgs e)
    {
        FlipY = FlipY == 1 ? -1 : 1;
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (MediaDataFilePath != null)
        {
            Player.Position = Player.Position.Add(new TimeSpan(0, 0, ForwardBackwardSkipSecond));
            OsdText = "İleri";
        }
    }

    private Image GenerateImage(byte[] imgdata, Thickness thickness)
    {
        BitmapImage bitmapImage = new();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = new MemoryStream(imgdata);
        bitmapImage.EndInit();
        bitmapImage.Freeze();
        Image image = new();
        image.BeginInit();
        image.Margin = thickness;
        image.Source = bitmapImage;
        image.EndInit();
        return image;
    }

    private TextBlock GenerateWhiteTextBlock(string text)
    {
        return new TextBlock
        { Text = text, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center };
    }

    private MediaState GetMediaState(MediaElement myMedia)
    {
        FieldInfo hlp = typeof(MediaElement).GetField("_helper", BindingFlags.NonPublic | BindingFlags.Instance);
        object helperObject = hlp.GetValue(myMedia);
        FieldInfo stateField = helperObject.GetType()
            .GetField("_currentState", BindingFlags.NonPublic | BindingFlags.Instance);
        return (MediaState)stateField.GetValue(helperObject);
    }

    private string GetNextPlayListFile()
    {
        int index = PlayList.IndexOf(MediaDataFilePath);
        return index < PlayList.Count - 1 ? PlayList[index + 1] : null;
    }

    private void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
    {
        if (PlayList.Any() && AutoSkipNextVideo)
        {
            MediaDataFilePath = GetNextPlayListFile();
        }
    }

    private void MediaViewer_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "SearchSubtitle")
        {
            MediaViewerSubtitleControl.cvs.Filter += (s, x) =>
            {
                SrtContent srtContent = x.Item as SrtContent;
                x.Accepted = srtContent.Text.Contains(SearchSubtitle);
            };
        }
    }

    private void Mute_Checked(object sender, RoutedEventArgs e)
    {
        MediaVolume = 0;
        OsdText = "Ses Kısıldı";
    }

    private void Mute_Unchecked(object sender, RoutedEventArgs e)
    {
        MediaVolume = 1;
        OsdText = "Ses Açıldı";
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (Player.CanPause)
        {
            Player.Pause();
            OsdText = "Durduruldu";
        }
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (MediaDataFilePath != null)
        {
            if (Player.Position == Player.NaturalDuration)
            {
                Player.Stop();
            }

            Player.Play();
            OsdText = "Çalıyor";
        }
    }

    private void Player_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (GetMediaState(Player) == MediaState.Play)
        {
            Player.Pause();
            OsdText = "Durduruldu";
            return;
        }

        Player.Play();
        OsdText = "Çalıyor";
    }

    private void Rotate_Click(object sender, RoutedEventArgs e)
    {
        Angle += 90;
        OsdText = "Döndürüldü";
        if (Angle == 360)
        {
            Angle = 0;
        }
    }

    private void Sld_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        sliderdragging = false;
        Player.Position = TimeSpan.FromSeconds(Sld.Value);
        timer.Start();
    }

    private void Sld_DragStarted(object sender, DragStartedEventArgs e)
    {
        sliderdragging = true;
        timer.Stop();
    }

    private void Slider_HeightValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        PixelateSize = new Size(PixelateSize.Width, e.NewValue);
    }

    private void Slider_WidthValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        PixelateSize = new Size(e.NewValue, PixelateSize.Height);
    }

    private void SlowBackward_Click(object sender, RoutedEventArgs e)
    {
        if (Player.CanPause)
        {
            Player.Play();
            Player.Position = Player.Position.Subtract(new TimeSpan(0, 0, 0, 0, 1000 / 30));
            Player.Pause();
        }
    }

    private void SlowForward_Click(object sender, RoutedEventArgs e)
    {
        if (Player.CanPause)
        {
            Player.Play();
            Player.Position = Player.Position.Add(new TimeSpan(0, 0, 0, 0, 1000 / 30));
            Player.Pause();
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (Player.Source != null)
        {
            Player.Stop();
            OsdText = "Durdu";
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
        Fov -= e.Delta / 100d;
    }

    private const int MillisecondsDelay = 500;

    private const string VideoFileExtensions =
        "Video Dosyaları (*.*)|*.3g2;*.3gp;*.3gp2;*.3gpp;*.amr;*.amv;*.asf;*.avi;*.bdmv;*.bik;*.d2v;*.divx;*.drc;*.dsa;*.dsm;*.dss;*.dsv;*.evo;*.f4v;*.flc;*.fli;*.flic;*.flv;*.hdmov;*.ifo;*.ivf;*.m1v;*.m2p;*.m2t;*.m2ts;*.m2v;*.m4b;*.m4p;*.m4v;*.mkv;*.mp2v;*.mp4;*.mp4v;*.mpe;*.mpeg;*.mpg;*.mpls;*.mpv2;*.mpv4;*.mov;*.mts;*.ogm;*.ogv;*.pss;*.pva;*.qt;*.ram;*.ratdvd;*.rm;*.rmm;*.rmvb;*.roq;*.rpm;*.smil;*.smk;*.swf;*.tp;*.tpr;*.ts;*.vob;*.vp6;*.webm;*.wm;*.wmp;*.wmv";

    private static readonly DispatcherTimer osdtimer = new();

    private static bool sliderdragging;

    private static DispatcherTimer timer;

    private bool _isOnDrag;

    private Point _startPoint;

    private double _startRotateX;

    private double _startRotateY;

    private ObservableCollection<SrtContent> parsedSubtitle;

    private string searchSubtitle;
}

public class SrtContent : InpcBase
{
    public SolidColorBrush BackgroundColor {
        get => backgroundColor;

        set {
            if (backgroundColor != value)
            {
                backgroundColor = value;
                OnPropertyChanged(nameof(BackgroundColor));
            }
        }
    }

    public TimeSpan EndTime {
        get => endTime;

        set {
            if (endTime != value)
            {
                endTime = value;
                OnPropertyChanged(nameof(EndTime));
            }
        }
    }

    public string Segment { get; set; }

    public TimeSpan StartTime {
        get => startTime;

        set {
            if (startTime != value)
            {
                startTime = value;
                OnPropertyChanged(nameof(StartTime));
            }
        }
    }

    public string Text {
        get => text;

        set {
            if (text != value)
            {
                text = value;
                OnPropertyChanged(nameof(Text));
            }
        }
    }

    private SolidColorBrush backgroundColor;

    private TimeSpan endTime;

    private TimeSpan startTime;

    private string text;
}