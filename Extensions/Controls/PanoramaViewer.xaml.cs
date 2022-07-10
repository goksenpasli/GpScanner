using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace Extensions.Controls
{
    /// <summary>
    /// Interaction logic for PanoramaViewer.xaml
    /// </summary>
    public partial class PanoramaViewer : UserControl
    {
        public static readonly DependencyProperty FovProperty = DependencyProperty.Register("Fov", typeof(double), typeof(PanoramaViewer), new PropertyMetadata(95d, FovChanged));

        public static readonly DependencyProperty PanoramaImageProperty = DependencyProperty.Register("PanoramaImage", typeof(string), typeof(PanoramaViewer), new PropertyMetadata(null, PanoramaImageChanged));

        public static readonly DependencyProperty PanoramaVideoProperty = DependencyProperty.Register("PanoramaVideo", typeof(string), typeof(PanoramaViewer), new PropertyMetadata(null, PanoramaVideoChanged));

        public static readonly DependencyProperty RotateXProperty = DependencyProperty.Register("RotateX", typeof(double), typeof(PanoramaViewer), new PropertyMetadata(0.0));

        public static readonly DependencyProperty RotateYProperty = DependencyProperty.Register("RotateY", typeof(double), typeof(PanoramaViewer), new PropertyMetadata(0.0));

        public static Geometry3D SphereModel = CreateGeometry();

        public PanoramaViewer()
        {
            InitializeComponent();
            DataContext = this;

            DosyaAç = new RelayCommand<object>(parameter =>
            {
                OpenFileDialog openFileDialog = new() { Multiselect = false, Filter = "Resim Dosyaları (*.jpg;*.jpeg;*.tif;*.tiff;*.png)|*.jpg;*.jpeg;*.tif;*.tiff;*.png| Video Dosyaları (*.mp4;*.mpg;*.wmv;*.avi;*.3gp)|*.mp4;*.mpg;*.wmv;*.avi;*.3gp" };
                if (openFileDialog.ShowDialog() == true)
                {
                    string file = openFileDialog.FileName.ToLower();
                    if (imageext.Contains(Path.GetExtension(file)))
                    {
                        PanoramaImage = file;
                        return;
                    }
                    if (videoext.Contains(Path.GetExtension(file)))
                    {
                        PanoramaVideo = file;
                    }
                }
            }, parameter => true);
        }

        public ICommand DosyaAç { get; }

        public double Fov { get => (double)GetValue(FovProperty); set => SetValue(FovProperty, value); }

        public string PanoramaImage { get => (string)GetValue(PanoramaImageProperty); set => SetValue(PanoramaImageProperty, value); }

        public string PanoramaVideo { get => (string)GetValue(PanoramaVideoProperty); set => SetValue(PanoramaVideoProperty, value); }

        public double RotateX { get => (double)GetValue(RotateXProperty); set => SetValue(RotateXProperty, value); }

        public double RotateY { get => (double)GetValue(RotateYProperty); set => SetValue(RotateYProperty, value); }

        internal static Point3D GetPosition(double t, double y)
        {
            double r = Math.Sqrt(1 - (y * y));
            double x = r * Math.Cos(t);
            double z = r * Math.Sin(t);
            return new Point3D(x, y, z);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            _isOnDrag = true;
            _startPoiint = e.GetPosition(this);
            _startRotateX = RotateX;
            _startRotateY = RotateY;
            base.OnMouseLeftButtonDown(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            _isOnDrag = false;
            base.OnMouseLeftButtonUp(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Cursor = Cursors.SizeAll;
            ToolTip = "Farenin Sol Tuşunu Basılı Tutup Sürükleyin.";
            if (_isOnDrag && e.LeftButton == MouseButtonState.Pressed)
            {
                Vector delta = _startPoiint - e.GetPosition(this);
                RotateX = _startRotateX + (delta.X / ActualWidth * 360);
                RotateY = _startRotateY + (delta.Y / ActualHeight * 360);
            }

            base.OnMouseMove(e);
        }

        private readonly string[] imageext = new string[] { ".jpg", ".jpeg", ".tif", ".tiff", ".png" };

        private readonly string[] videoext = new string[] { ".mp4", ".wmv", ".avi", ".mpg", ".3gp" };

        private bool _isOnDrag;

        private System.Windows.Point _startPoiint;

        private double _startRotateX;

        private double _startRotateY;

        private static Geometry3D CreateGeometry()
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

        private static void FovChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PanoramaViewer viewer && e.NewValue != null)
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

        private static System.Windows.Point GetTextureCoordinate(double t, double y)
        {
            Matrix TYtoUV = new();
            TYtoUV.Scale(1 / (2 * Math.PI), -0.5);
            System.Windows.Point p = new(t, y);
            p *= TYtoUV;
            return p;
        }

        private static void PanoramaImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PanoramaViewer viewer && e.NewValue != null)
            {
                try
                {
                    string resimyolu = e.NewValue as string;
                    switch (Path.GetExtension(resimyolu).ToLower())
                    {
                        case ".tiff":
                        case ".tif":
                        case ".png":
                        case ".jpg":
                        case ".jpeg":
                            {
                                BitmapImage bi = new();
                                bi.BeginInit();
                                bi.UriSource = new Uri(resimyolu);
                                bi.CacheOption = BitmapCacheOption.None;
                                bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                                bi.EndInit();
                                if (!bi.IsFrozen && bi.CanFreeze)
                                {
                                    bi.Freeze();
                                }

                                viewer.panoramaBrush.Brush = new ImageBrush(bi);
                                break;
                            }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private static void PanoramaVideoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PanoramaViewer viewer && e.NewValue != null)
            {
                try
                {
                    string videoyolu = e.NewValue as string;
                    viewer.panoramaBrush.Brush = new VisualBrush(new MediaElement { Source = new Uri(videoyolu) });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Viewport3D_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Fov -= e.Delta / 100;
        }
    }
}