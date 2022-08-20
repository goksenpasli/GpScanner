using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

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

        public static readonly DependencyProperty InvertColorProperty = DependencyProperty.Register("InvertColor", typeof(bool), typeof(MediaViewer), new PropertyMetadata(false));

        public static readonly DependencyProperty MediaDataFilePathProperty = DependencyProperty.Register("MediaDataFilePath", typeof(string), typeof(MediaViewer), new PropertyMetadata(null, MediaDataFilePathChanged));

        public static readonly DependencyProperty MediaPositionProperty = DependencyProperty.Register("MediaPosition", typeof(TimeSpan), typeof(MediaViewer), new PropertyMetadata(TimeSpan.Zero, MediaPositionChanged));

        public static readonly DependencyProperty MediaVolumeProperty = DependencyProperty.Register("MediaVolume", typeof(double), typeof(MediaViewer), new PropertyMetadata(1d, MediaVolumeChanged));

        public static readonly DependencyProperty PixelateSizeProperty = DependencyProperty.Register("PixelateSize", typeof(Size), typeof(MediaViewer), new PropertyMetadata(new Size(60, 40)));

        public static readonly DependencyProperty SharpenAmountProperty = DependencyProperty.Register("SharpenAmount", typeof(double), typeof(MediaViewer), new PropertyMetadata(1.0d));

        public static readonly DependencyProperty SliderControlVisibleProperty = DependencyProperty.Register("SliderControlVisible", typeof(Visibility), typeof(MediaViewer), new PropertyMetadata(Visibility.Visible));

        public static readonly DependencyProperty ThumbnailsVisibleProperty = DependencyProperty.Register("ThumbnailsVisible", typeof(bool), typeof(MediaViewer), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        private static readonly Image image = new();

        private static readonly MediaElement mediaElement = new()
        {
            UnloadedBehavior = MediaState.Manual,
            ScrubbingEnabled = true,
            IsMuted = true,
            Height = 96,
            Width = 96 * SystemParameters.PrimaryScreenWidth / SystemParameters.PrimaryScreenHeight,
        };

        private static readonly TaskFactory task;

        private static readonly ToolTip tooltip = new()
        {
            Width = 96 * SystemParameters.PrimaryScreenWidth / SystemParameters.PrimaryScreenHeight,
            Height = 96,
            Placement = PlacementMode.Mouse
        };

        private static bool dragging;

        private static DispatcherTimer timer;

        static MediaViewer()
        {
            task = new TaskFactory();
        }

        public MediaViewer()
        {
            InitializeComponent();
            mediaElement.Pause();
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
            get { return (Visibility)GetValue(ControlVisibleProperty); }
            set { SetValue(ControlVisibleProperty, value); }
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

        public Size PixelateSize
        {
            get => (Size)GetValue(PixelateSizeProperty);
            set => SetValue(PixelateSizeProperty, value);
        }

        public double SharpenAmount
        {
            get => (double)GetValue(SharpenAmountProperty);
            set => SetValue(SharpenAmountProperty, value);
        }

        public Visibility SliderControlVisible
        {
            get { return (Visibility)GetValue(SliderControlVisibleProperty); }
            set { SetValue(SliderControlVisibleProperty, value); }
        }

        public bool ThumbnailsVisible { get => (bool)GetValue(ThumbnailsVisibleProperty); set => SetValue(ThumbnailsVisibleProperty, value); }

        private static void AutoplayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()) && d is MediaViewer viewer && (bool)e.NewValue && viewer.Player.Source != null)
            {
                viewer.Player.Play();
            }
        }

        private static void MediaDataFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()) && d is MediaViewer viewer && e.NewValue != null)
            {
                try
                {
                    string uriString = (string)e.NewValue;
                    viewer.Player.Source = new Uri(uriString);
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
                viewer.Player.Position = (TimeSpan)e.NewValue;
            }
        }

        private static void MediaVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MediaViewer viewer && e.NewValue != null)
            {
                viewer.Player.Volume = (double)e.NewValue;
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            Player.Position = Player.Position.Subtract(new TimeSpan(0, 0, 30));
        }

        private void Capture_Click(object sender, RoutedEventArgs e)
        {
            if (Player.NaturalVideoWidth > 0)
            {
                string picturesfolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                byte[] data = grid.ToRenderTargetBitmap().ToTiffJpegByteArray(ExtensionMethods.Format.Jpg);
                File.WriteAllBytes(picturesfolder.SetUniqueFile("Resim", "jpg"), data);
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
            Player.Position = Player.Position.Add(new TimeSpan(0, 0, 30));
        }

        private MediaState GetMediaState(MediaElement myMedia)
        {
            FieldInfo hlp = typeof(MediaElement).GetField("_helper", BindingFlags.NonPublic | BindingFlags.Instance);
            object helperObject = hlp.GetValue(myMedia);
            FieldInfo stateField = helperObject.GetType().GetField("_currentState", BindingFlags.NonPublic | BindingFlags.Instance);
            return (MediaState)stateField.GetValue(helperObject);
        }

        private void Mute_Checked(object sender, RoutedEventArgs e)
        {
            MediaVolume = 0;
        }

        private void Mute_Unchecked(object sender, RoutedEventArgs e)
        {
            MediaVolume = 1;
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
                    if (mediaElement.CanPause)
                    {
                        mediaElement.Pause();
                    }
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
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
                               mediaElement.Source = Player.Source;
                               tooltip.PlacementTarget = Sld;
                               mediaElement.Position = TimeSpan.FromSeconds(PixelsToValue(e.GetPosition(Sld).X, Sld.Minimum, Sld.Maximum, Sld.ActualWidth));
                               image.Source = mediaElement.ToRenderTargetBitmap();
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
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}