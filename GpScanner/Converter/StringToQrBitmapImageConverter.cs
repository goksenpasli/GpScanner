using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace GpScanner.Converter;

public sealed class StringToQrBitmapImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string data && !string.IsNullOrWhiteSpace(data) ? QrCode.QrCode.GenerateQr(data) : null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is WriteableBitmap bitmapImage ? QrCode.QrCode.GetImageBarcodeResult(BitmapFrame.Create(bitmapImage)) : null;
    }
}