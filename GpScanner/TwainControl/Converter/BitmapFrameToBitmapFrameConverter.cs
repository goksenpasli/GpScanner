using Extensions;
using System;
using System.Drawing.Imaging;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace TwainControl.Converter;

public sealed class BitmapFrameToBitmapFrameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is BitmapFrame bitmapFrame ? BitmapFrame.Create(bitmapFrame.BitmapSourceToBitmap().ToBitmapImage(ImageFormat.Jpeg)) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}