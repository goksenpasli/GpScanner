using Extensions;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace TwainControl.Converter;

public sealed class BitmapFrameToBitmapFrameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BitmapFrame bitmapFrame)
        {
            BitmapFrame bf = BitmapFrame.Create(bitmapFrame.ToBitmapImage());
            bf.Freeze();
            return bf;
        }
        else
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}