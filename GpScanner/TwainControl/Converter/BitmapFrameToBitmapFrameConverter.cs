using Extensions;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace TwainControl.Converter;

public sealed class BitmapFrameToBitmapFrameConverter : DependencyObject, IValueConverter
{
    public static readonly DependencyProperty DecodeHeightProperty = DependencyProperty.Register("DecodeHeight", typeof(int), typeof(BitmapFrameToBitmapFrameConverter), new PropertyMetadata(0));

    public int DecodeHeight { get => (int)GetValue(DecodeHeightProperty); set => SetValue(DecodeHeightProperty, value); }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BitmapFrame bitmapFrame)
        {
            BitmapFrame bf = BitmapFrame.Create(bitmapFrame.ToBitmapImage(DecodeHeight));
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