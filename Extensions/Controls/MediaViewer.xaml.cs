using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Extensions.Controls
{
    public partial class MediaViewer : UserControl
    {
        public static readonly DependencyProperty AngleProperty = DependencyProperty.Register("Angle", typeof(double), typeof(MediaViewer), new PropertyMetadata(0.0d));

        public static readonly DependencyProperty ApplyBwProperty = DependencyProperty.Register("ApplyBw", typeof(bool), typeof(MediaViewer), new PropertyMetadata(false));

        public static readonly DependencyProperty ApplyEmbossProperty = DependencyProperty.Register("ApplyEmboss", typeof(bool), typeof(MediaViewer), new PropertyMetadata(false));

        public static readonly DependencyProperty ApplyGrayscaleProperty = DependencyProperty.Register("ApplyGrayscale", typeof(bool), typeof(MediaViewer), new PropertyMetadata(false));

        public static readonly DependencyProperty ApplyPixelateProperty = DependencyProperty.Register("ApplyPixelate", typeof(bool), typeof(MediaViewer), new PropertyMetadata(false));

        public static readonly DependencyProperty ApplySharpenProperty = DependencyProperty.Register("ApplySharpen", typeof(bool), typeof(MediaViewer), new PropertyMetadata(false));

        public static readonly DependencyProperty AutoPlayProperty = DependencyProperty.Register("AutoPlay", typeof(bool), typeof(MediaViewer), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, new PropertyChangedCallback(AutoplayChanged)));

        public static readonly DependencyProperty BwAmountProperty = DependencyProperty.Register("BwAmount", typeof(double), typeof(MediaViewer), new PropertyMetadata(0.6D));

        public static readonly DependencyProperty ContextMenuVisibilityProperty = DependencyProperty.Register("ContextMenuVisibility", typeof(Visibility), typeof(MediaElement), new PropertyMetadata(Visibility.Collapsed));

        public static readonly DependencyProperty ControlVisibleProperty = DependencyProperty.Register("ControlVisible", typeof(Visibility), typeof(MediaViewer), new PropertyMetadata(Visibility.Visible));

        public static readonly DependencyProperty EndTimeSpanProperty = DependencyProperty.Register("EndTimeSpan", typeof(TimeSpan), typeof(MediaViewer), new PropertyMetadata(TimeSpan.Zero));

        public static readonly DependencyProperty FlipXProperty = DependencyProperty.Register("FlipX", typeof(double), typeof(MediaViewer), new PropertyMetadata(1.0d));

        public static readonly DependencyProperty FlipYProperty = DependencyProperty.Register("FlipY", typeof(double), typeof(MediaViewer), new PropertyMetadata(1.0d));

        public static readonly DependencyProperty FovProperty = DependencyProperty.Register("Fov", typeof(double), typeof(MediaViewer), new PropertyMetadata(95d, FovChanged));

        public static readonly DependencyProperty InvertColorProperty = DependencyProperty.Register("InvertColor", typeof(bool), typeof(MediaViewer), new PropertyMetadata(false));

        public static readonly DependencyProperty MediaDataFilePathProperty = DependencyProperty.Register("MediaDataFilePath", typeof(string), typeof(MediaViewer), new PropertyMetadata(null, MediaDataFilePathChanged));

        public static readonly DependencyProperty MediaPositionProperty = DependencyProperty.Register("MediaPosition", typeof(TimeSpan), typeof(MediaViewer), new PropertyMetadata(TimeSpan.Zero, MediaPositionChanged));

        public static readonly DependencyProperty MediaVolumeProperty = DependencyProperty.Register("MediaVolume", typeof(double), typeof(MediaViewer), new PropertyMetadata(1d, MediaVolumeChanged));

        public static readonly DependencyProperty PanoramaModeProperty = DependencyProperty.Register("PanoramaMode", typeof(bool), typeof(MediaViewer), new PropertyMetadata(PanoramaModeChanged));

        public static readonly DependencyProperty PixelateSizeProperty = DependencyProperty.Register("PixelateSize", typeof(Size), typeof(MediaViewer), new PropertyMetadata(new Size(60, 40)));

        public static readonly DependencyProperty RotateXProperty = DependencyProperty.Register("RotateX", typeof(double), typeof(MediaViewer), new PropertyMetadata(0.0));

        public static readonly DependencyProperty RotateYProperty = DependencyProperty.Register("RotateY", typeof(double), typeof(MediaViewer), new PropertyMetadata(0.0));

        public static readonly DependencyProperty SharpenAmountProperty = DependencyProperty.Register("SharpenAmount", typeof(double), typeof(MediaViewer), new PropertyMetadata(1.0d));

        public static readonly DependencyProperty SliderControlVisibleProperty = DependencyProperty.Register("SliderControlVisible", typeof(Visibility), typeof(MediaViewer), new PropertyMetadata(Visibility.Visible));

        public static readonly DependencyProperty SubTitleColorProperty = DependencyProperty.Register("SubTitleColor", typeof(Brush), typeof(MediaViewer), new PropertyMetadata(Brushes.White));

        public static readonly DependencyProperty SubtitleFilePathProperty = DependencyProperty.Register("SubtitleFilePath", typeof(string), typeof(MediaViewer), new PropertyMetadata(null, SubtitleFilePathChanged));

        public static readonly DependencyProperty SubTitleHorizontalAlignmentProperty = DependencyProperty.Register("SubTitleHorizontalAlignment", typeof(HorizontalAlignment), typeof(MediaViewer), new PropertyMetadata(HorizontalAlignment.Center));

        public static readonly DependencyProperty SubTitleMarginProperty = DependencyProperty.Register("SubTitleMargin", typeof(Thickness), typeof(MediaViewer), new PropertyMetadata(new Thickness(0d, 0d, 0d, 10d)));

        public static readonly DependencyProperty SubTitleProperty = DependencyProperty.Register("SubTitle", typeof(string), typeof(MediaViewer), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SubTitleSizeProperty = DependencyProperty.Register("SubTitleSize", typeof(double), typeof(MediaViewer), new PropertyMetadata(32.0d));

        public static readonly DependencyProperty SubTitleVerticalAlignmentProperty = DependencyProperty.Register("SubTitleVerticalAlignment", typeof(VerticalAlignment), typeof(MediaViewer), new PropertyMetadata(VerticalAlignment.Bottom));

        public static readonly DependencyProperty SubTitleVisibilityProperty = DependencyProperty.Register("SubTitleVisibility", typeof(Visibility), typeof(MediaViewer), new PropertyMetadata(Visibility.Collapsed));

        public static readonly DependencyProperty ThumbnailsVisibleProperty = DependencyProperty.Register("ThumbnailsVisible", typeof(bool), typeof(MediaViewer), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty VideoStretchProperty = DependencyProperty.Register("VideoStretch", typeof(Stretch), typeof(MediaViewer), new PropertyMetadata(Stretch.Uniform));

        private static readonly Image image = new();

        private static readonly TaskFactory task;

        private static readonly MediaElement thumbMediaElement = new()
        {
            UnloadedBehavior = MediaState.Manual,
            ScrubbingEnabled = true,
            IsMuted = true,
            Height = 96,
            Width = 96 * SystemParameters.PrimaryScreenWidth / SystemParameters.PrimaryScreenHeight,
        };

        private static readonly ToolTip tooltip = new()
        {
            Width = 96 * SystemParameters.PrimaryScreenWidth / SystemParameters.PrimaryScreenHeight,
            Height = 96,
            Placement = PlacementMode.Mouse
        };

        private static bool dragging;

        private static DispatcherTimer timer;

        private bool _isOnDrag;

        private Point _startPoint;

        private double _startRotateX;

        private double _startRotateY;

        static MediaViewer()
        {
            task = new TaskFactory();
        }

        public MediaViewer()
        {
            InitializeComponent();
            Player.Pause();
            Player.MediaEnded += MediaElement_MediaEnded;
            PanoramaViewPort.Visibility = Visibility.Collapsed;
            DataContext = this;
        }

        public double Angle
        {
            get => (double)GetValue(AngleProperty);
            set => SetValue(AngleProperty, value);
        }

        public bool ApplyBw
        {
            get => (bool)GetValue(ApplyBwProperty);
            set => SetValue(ApplyBwProperty, value);
        }

        public bool ApplyEmboss
        {
            get => (bool)GetValue(ApplyEmbossProperty);
            set => SetValue(ApplyEmbossProperty, value);
        }

        public bool ApplyGrayscale
        {
            get => (bool)GetValue(ApplyGrayscaleProperty);
            set => SetValue(ApplyGrayscaleProperty, value);
        }

        public bool ApplyPixelate
        {
            get => (bool)GetValue(ApplyPixelateProperty);
            set => SetValue(ApplyPixelateProperty, value);
        }

        public bool ApplySharpen
        {
            get => (bool)GetValue(ApplySharpenProperty);
            set => SetValue(ApplySharpenProperty, value);
        }

        public bool AutoPlay
        {
            get => (bool)GetValue(AutoPlayProperty);
            set => SetValue(AutoPlayProperty, value);
        }

        public bool AutoSkipNextVideo { get; set; }

        public double BwAmount
        {
            get => (double)GetValue(BwAmountProperty);
            set => SetValue(BwAmountProperty, value);
        }

        public Visibility ContextMenuVisibility
        {
            get => (Visibility)GetValue(ContextMenuVisibilityProperty);
            set => SetValue(ContextMenuVisibilityProperty, value);
        }

        public Visibility ControlVisible
        {
            get => (Visibility)GetValue(ControlVisibleProperty);
            set => SetValue(ControlVisibleProperty, value);
        }

        public TimeSpan EndTimeSpan { get => (TimeSpan)GetValue(EndTimeSpanProperty); set => SetValue(EndTimeSpanProperty, value); }

        public double FlipX
        {
            get => (double)GetValue(FlipXProperty);
            set => SetValue(FlipXProperty, value);
        }

        public double FlipY
        {
            get => (double)GetValue(FlipYProperty);
            set => SetValue(FlipYProperty, value);
        }

        public int ForwardBackwardSkipSecond { get; set; } = 30;

        public double Fov { get => (double)GetValue(FovProperty); set => SetValue(FovProperty, value); }

        public bool InvertColor
        {
            get => (bool)GetValue(InvertColorProperty);
            set => SetValue(InvertColorProperty, value);
        }

        public string MediaDataFilePath { get => (string)GetValue(MediaDataFilePathProperty); set => SetValue(MediaDataFilePathProperty, value); }

        public TimeSpan MediaPosition { get => (TimeSpan)GetValue(MediaPositionProperty); set => SetValue(MediaPositionProperty, value); }

        public double MediaVolume
        {
            get => (double)GetValue(MediaVolumeProperty);
            set => SetValue(MediaVolumeProperty, value);
        }

        public Visibility OpenButtonVisibility { get; set; } = Visibility.Collapsed;

        public bool PanoramaMode
        {
            get => (bool)GetValue(PanoramaModeProperty);
            set => SetValue(PanoramaModeProperty, value);
        }

        [Browsable(false)]
        public ObservableCollection<SrtContent> ParsedSubtitle { get; set; }

        public Size PixelateSize
        {
            get => (Size)GetValue(PixelateSizeProperty);
            set => SetValue(PixelateSizeProperty, value);
        }

        [Browsable(false)]
        public ObservableCollection<string> PlayList { get; set; } = new();

        public double RotateX { get => (double)GetValue(RotateXProperty); set => SetValue(RotateXProperty, value); }

        public double RotateY { get => (double)GetValue(RotateYProperty); set => SetValue(RotateYProperty, value); }

        public double SharpenAmount
        {
            get => (double)GetValue(SharpenAmountProperty);
            set => SetValue(SharpenAmountProperty, value);
        }

        public Visibility SliderControlVisible
        {
            get => (Visibility)GetValue(SliderControlVisibleProperty);
            set => SetValue(SliderControlVisibleProperty, value);
        }

        public Geometry3D SphereModel { get; set; } = CreateGeometry();

        public string SubTitle
        {
            get => (string)GetValue(SubTitleProperty);
            set => SetValue(SubTitleProperty, value);
        }

        public Brush SubTitleColor
        {
            get => (Brush)GetValue(SubTitleColorProperty);
            set => SetValue(SubTitleColorProperty, value);
        }

        public string SubtitleFilePath
        {
            get => (string)GetValue(SubtitleFilePathProperty);
            set => SetValue(SubtitleFilePathProperty, value);
        }

        public HorizontalAlignment SubTitleHorizontalAlignment
        {
            get => (HorizontalAlignment)GetValue(SubTitleHorizontalAlignmentProperty);
            set => SetValue(SubTitleHorizontalAlignmentProperty, value);
        }

        public Thickness SubTitleMargin
        {
            get => (Thickness)GetValue(SubTitleMarginProperty);
            set => SetValue(SubTitleMarginProperty, value);
        }

        public double SubTitleSize
        {
            get => (double)GetValue(SubTitleSizeProperty);
            set => SetValue(SubTitleSizeProperty, value);
        }

        public VerticalAlignment SubTitleVerticalAlignment
        {
            get => (VerticalAlignment)GetValue(SubTitleVerticalAlignmentProperty);
            set => SetValue(SubTitleVerticalAlignmentProperty, value);
        }

        public Visibility SubTitleVisibility
        {
            get => (Visibility)GetValue(SubTitleVisibilityProperty);
            set => SetValue(SubTitleVisibilityProperty, value);
        }

        public bool ThumbnailsVisible { get => (bool)GetValue(ThumbnailsVisibleProperty); set => SetValue(ThumbnailsVisibleProperty, value); }

        public Stretch VideoStretch
        {
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

        internal static Point3D GetPosition(double t, double y)
        {
            double r = Math.Sqrt(1 - (y * y));
            double x = r * Math.Cos(t);
            double z = r * Math.Sin(t);
            return new Point3D(x, y, z);
        }

        private static void AutoplayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()) && d is MediaViewer viewer && (bool)e.NewValue && viewer.MediaDataFilePath != null)
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
            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()) && d is MediaViewer viewer && e.NewValue != null)
            {
                try
                {
                    string uriString = (string)e.NewValue;
                    viewer.Player.Source = new Uri(uriString);
                    if (viewer.AutoPlay)
                    {
                        viewer.Player.Play();
                    }
                    viewer.Player.MediaOpened += (f, g) =>
                    {
                        if (f is MediaElement mediaelement && mediaelement.NaturalDuration.HasTimeSpan)
                        {
                            viewer.EndTimeSpan = mediaelement.NaturalDuration.TimeSpan;
                            timer = new DispatcherTimer(TimeSpan.FromMilliseconds(1000), DispatcherPriority.Normal, (s, ee) => viewer.MediaPosition = mediaelement.Position, Dispatcher.CurrentDispatcher);
                            timer.Start();
                        }
                    };
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private static void MediaPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MediaViewer viewer && e.NewValue != null && !dragging)
            {
                TimeSpan position = (TimeSpan)e.NewValue;
                viewer.Player.Position = position;
                if (viewer.SubTitleVisibility == Visibility.Visible && viewer.ParsedSubtitle is not null)
                {
                    foreach (SrtContent subtitle in viewer.ParsedSubtitle)
                    {
                        if (position > subtitle.StartTime && position < subtitle.EndTime)
                        {
                            viewer.SubTitle = subtitle.Text;
                        }
                        if (position > subtitle.EndTime)
                        {
                            viewer.SubTitle = string.Empty;
                        }
                    }
                }
            }
        }

        private static void MediaVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MediaViewer viewer && e.NewValue != null)
            {
                viewer.Player.Volume = (double)e.NewValue;
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
                }
                else
                {
                    viewer.PanoramaViewPort.Visibility = Visibility.Collapsed;
                    viewer.panoramaBrush.Brush = null;
                }
            }
        }

        private static void SubtitleFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()) && d is MediaViewer viewer && e.NewValue != null)
            {
                viewer.ParsedSubtitle = viewer.ParseSrtFile((string)e.NewValue);
            }
        }

        private void AddPlaylist_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new() { Multiselect = true, Filter = "Video Dosyaları (*.*)|*.3g2;*.3gp;*.3gp2;*.3gpp;*.amr;*.amv;*.asf;*.avi;*.bdmv;*.bik;*.d2v;*.divx;*.drc;*.dsa;*.dsm;*.dss;*.dsv;*.evo;*.f4v;*.flc;*.fli;*.flic;*.flv;*.hdmov;*.ifo;*.ivf;*.m1v;*.m2p;*.m2t;*.m2ts;*.m2v;*.m4b;*.m4p;*.m4v;*.mkv;*.mp2v;*.mp4;*.mp4v;*.mpe;*.mpeg;*.mpg;*.mpls;*.mpv2;*.mpv4;*.mov;*.mts;*.ogm;*.ogv;*.pss;*.pva;*.qt;*.ram;*.ratdvd;*.rm;*.rmm;*.rmvb;*.roq;*.rpm;*.smil;*.smk;*.swf;*.tp;*.tpr;*.ts;*.vob;*.vp6;*.webm;*.wm;*.wmp;*.wmv" };
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
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            Player.Position = Player.Position.Subtract(new TimeSpan(0, 0, ForwardBackwardSkipSecond));
        }

        private void Capture_Click(object sender, RoutedEventArgs e)
        {
            if (Player.NaturalVideoWidth > 0)
            {
                string picturesfolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                byte[] data = grid.ToRenderTargetBitmap().ToTiffJpegByteArray(ExtensionMethods.Format.Jpg);
                string dosya = picturesfolder.SetUniqueFile("Resim", "jpg");
                File.WriteAllBytes(dosya, data);
                ExtensionMethods.OpenFolderAndSelectItem(picturesfolder, dosya);
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
            Player.Position = Player.Position.Add(new TimeSpan(0, 0, ForwardBackwardSkipSecond));
        }

        private MediaState GetMediaState(MediaElement myMedia)
        {
            FieldInfo hlp = typeof(MediaElement).GetField("_helper", BindingFlags.NonPublic | BindingFlags.Instance);
            object helperObject = hlp.GetValue(myMedia);
            FieldInfo stateField = helperObject.GetType().GetField("_currentState", BindingFlags.NonPublic | BindingFlags.Instance);
            return (MediaState)stateField.GetValue(helperObject);
        }

        private void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (PlayList.Any() && AutoSkipNextVideo)
            {
                int index = PlayList.IndexOf(MediaDataFilePath);
                if (index < PlayList.Count() - 1)
                {
                    MediaDataFilePath = PlayList[index + 1];
                }
            }
        }

        private void Mute_Checked(object sender, RoutedEventArgs e)
        {
            MediaVolume = 0;
        }

        private void Mute_Unchecked(object sender, RoutedEventArgs e)
        {
            MediaVolume = 1;
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new() { Multiselect = false, Filter = "Video Dosyaları (*.*)|*.3g2;*.3gp;*.3gp2;*.3gpp;*.amr;*.amv;*.asf;*.avi;*.bdmv;*.bik;*.d2v;*.divx;*.drc;*.dsa;*.dsm;*.dss;*.dsv;*.evo;*.f4v;*.flc;*.fli;*.flic;*.flv;*.hdmov;*.ifo;*.ivf;*.m1v;*.m2p;*.m2t;*.m2ts;*.m2v;*.m4b;*.m4p;*.m4v;*.mkv;*.mp2v;*.mp4;*.mp4v;*.mpe;*.mpeg;*.mpg;*.mpls;*.mpv2;*.mpv4;*.mov;*.mts;*.ogm;*.ogv;*.pss;*.pva;*.qt;*.ram;*.ratdvd;*.rm;*.rmm;*.rmvb;*.roq;*.rpm;*.smil;*.smk;*.swf;*.tp;*.tpr;*.ts;*.vob;*.vp6;*.webm;*.wm;*.wmp;*.wmv" };
            if (openFileDialog.ShowDialog() == true)
            {
                MediaDataFilePath = openFileDialog.FileName;
            }
        }

        private ObservableCollection<SrtContent> ParseSrtFile(string filepath)
        {
            try
            {
                ObservableCollection<SrtContent> content = new();
                foreach (string element in File.ReadAllText(filepath, Encoding.UTF8).Split(new string[] { "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    content.Add(new SrtContent()
                    {
                        StartTime = TimeSpan.Parse(element.Split('\n')[1].Substring(0, element.Split('\n')[1].LastIndexOf("-->")).Trim()),
                        EndTime = TimeSpan.Parse(element.Split('\n')[1].Substring(element.Split('\n')[1].LastIndexOf("-->") + 3).Trim()),
                        Text = string.Concat(element.Split('\n').Skip(2).Take(element.Split('\n').Length - 2)),
                        Segment = element.Split('\n')[0]
                    });
                }
                return content;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            if (Player.CanPause)
            {
                Player.Pause();
            }
        }

        private double PixelsToValue(double pixels, double minValue, double maxValue, double width)
        {
            double range = maxValue - minValue;
            double percentage = pixels / width * 100;
            return (percentage / 100 * range) + minValue;
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
            }
        }

        private void Player_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (GetMediaState(Player) == MediaState.Play)
            {
                Player.Pause();
            }
            else
            {
                Player.Play();
            }
        }

        private void Rotate_Click(object sender, RoutedEventArgs e)
        {
            Angle += 90;
            if (Angle == 360)
            {
                Angle = 0;
            }
        }

        private void Sld_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            dragging = false;
            Player.Position = TimeSpan.FromSeconds(Sld.Value);
            timer.Start();
        }

        private void Sld_DragStarted(object sender, DragStartedEventArgs e)
        {
            dragging = true;
            timer.Stop();
        }

        private void Sld_MouseLeave(object sender, MouseEventArgs e)
        {
            if (ThumbnailsVisible)
            {
                try
                {
                    tooltip.IsOpen = false;
                    image.Source = null;
                    if (Player.CanPause)
                    {
                        Player.Pause();
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private void Sld_MouseMove(object sender, MouseEventArgs e)
        {
            if (ThumbnailsVisible && Player.HasVideo)
            {
                try
                {
                    _ = task.StartNew(() =>
                       {
                           _ = Dispatcher.BeginInvoke(() =>
                           {
                               thumbMediaElement.Source = Player.Source;
                               tooltip.PlacementTarget = Sld;
                               thumbMediaElement.Position = TimeSpan.FromSeconds(PixelsToValue(e.GetPosition(Sld).X, Sld.Minimum, Sld.Maximum, Sld.ActualWidth));
                               image.Source = thumbMediaElement.ToRenderTargetBitmap();
                               if (image.Source.CanFreeze)
                               {
                                   image.Source.Freeze();
                               }
                               tooltip.Content = image;
                               if (!tooltip.IsOpen)
                               {
                                   tooltip.IsOpen = true;
                               }
                           });
                       }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
                }
                catch (Exception)
                {
                }
            }
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
            }
        }

        private void Subtitle_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new() { Multiselect = false, Filter = "Srt Dosyası (*.srt)|*.srt" };
            if (openFileDialog.ShowDialog() == true)
            {
                SubtitleFilePath = openFileDialog.FileName;
                ParsedSubtitle = ParseSrtFile(SubtitleFilePath);
            }
        }

        private void SubtitleMargin_Click(object sender, RoutedEventArgs e)
        {
            Thickness defaultsubtitlethickness = new(SubTitleMargin.Left, SubTitleMargin.Top, SubTitleMargin.Right, SubTitleMargin.Bottom);
            if (sender is Button button)
            {
                switch (button.Content)
                {
                    case "6":
                        defaultsubtitlethickness.Bottom -= 10;
                        SubTitleMargin = defaultsubtitlethickness;
                        return;

                    case "5":
                        defaultsubtitlethickness.Bottom += 10;
                        SubTitleMargin = defaultsubtitlethickness;
                        return;

                    case "4":
                        defaultsubtitlethickness.Left += 10;
                        SubTitleMargin = defaultsubtitlethickness;
                        return;

                    case "3":
                        defaultsubtitlethickness.Left -= 10;
                        SubTitleMargin = defaultsubtitlethickness;
                        return;
                }
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

    public class SrtContent
    {
        public TimeSpan EndTime { get; set; }

        public string Segment { get; set; }

        public TimeSpan StartTime { get; set; }

        public string Text { get; set; }
    }
}