using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Extensions;

namespace TwainControl
{
    /// <summary>
    /// Interaction logic for ToolBox.xaml
    /// </summary>
    public partial class ToolBox : UserControl, INotifyPropertyChanged
    {
        public ToolBox()
        {
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static Scanner Scanner { get; set; }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void Scanner_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "EnAdet")
            {
                LineGrid.ColumnDefinitions.Clear();
                for (int i = 0; i < Scanner.EnAdet; i++)
                {
                    LineGrid.ColumnDefinitions.Add(new ColumnDefinition());
                }
            }
            if (e.PropertyName is "BoyAdet")
            {
                LineGrid.RowDefinitions.Clear();
                for (int i = 0; i < Scanner.BoyAdet; i++)
                {
                    LineGrid.RowDefinitions.Add(new RowDefinition());
                }
            }
            if (e.PropertyName is "Brightness" && Scanner.CopyCroppedImage is not null)
            {
                using Bitmap bmp = ((BitmapSource)Scanner.CopyCroppedImage).BitmapSourceToBitmap();
                Scanner.CroppedImage = bmp.AdjustBrightness((int)Scanner.Brightness).ToBitmapImage(ImageFormat.Png);
            }
            if (e.PropertyName is "CroppedImageAngle" && Scanner.CopyCroppedImage is not null)
            {
                TransformedBitmap transformedBitmap = new((BitmapSource)Scanner.CopyCroppedImage, new RotateTransform(Scanner.CroppedImageAngle));
                transformedBitmap.Freeze();
                Scanner.CroppedImage = transformedBitmap;
            }
            if (e.PropertyName is "Threshold" && Scanner.CopyCroppedImage is not null)
            {
                System.Windows.Media.Color source = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Scanner.SourceColor);
                System.Windows.Media.Color target = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Scanner.TargetColor);
                using Bitmap bmp = ((BitmapSource)Scanner.CopyCroppedImage).BitmapSourceToBitmap();
                Scanner.CroppedImage = bmp.ReplaceColor(source, target, (int)Scanner.Threshold).ToBitmapImage(ImageFormat.Png);
            }
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Scanner.PropertyChanged += Scanner_PropertyChanged;
        }
    }
}