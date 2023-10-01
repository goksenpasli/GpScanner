using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace GpScanner.Converter;

public sealed class StringToQrBitmapImageConverter : IValueConverter
{
    private readonly QrCode.QrCode qrcode = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is string data && !string.IsNullOrWhiteSpace(data)
        ? qrcode.GenerateQr(data)
        : (object)null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is WriteableBitmap bitmapImage
        ? qrcode.GetImageBarcodeResult(BitmapFrame.Create(bitmapImage))
        : (object)null;
}