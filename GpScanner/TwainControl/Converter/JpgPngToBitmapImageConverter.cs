using Extensions;
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace TwainControl.Converter;

public sealed class JpgPngToBitmapImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string path && parameter is string stringparam
               ? Task.Run(
            () =>
            {
                BitmapImage image = new();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.None;
                image.DecodePixelHeight = int.TryParse(stringparam, out int val) ? val : 150;
                image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache | BitmapCreateOptions.DelayCreation;
                image.UriSource = new Uri(path);
                image.EndInit();
                if (!image.IsFrozen && image.CanFreeze)
                {
                    image.Freeze();
                }
                return image;
            })
               : (object)null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}