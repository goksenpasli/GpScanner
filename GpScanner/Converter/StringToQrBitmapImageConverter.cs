using Extensions;
using System;
using System.Drawing.Imaging;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using TwainControl;

namespace GpScanner.Converter;

public sealed class StringToQrBitmapImageConverter : IValueConverter
{
    private readonly QrCode.QrCode qrcode = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is string data && !string.IsNullOrWhiteSpace(data) ? qrcode.GenerateQr(data, 200, 200) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is WriteableBitmap bitmapImage
                                                                                                       ? qrcode.GetImageBarcodeResult(BitmapFrame.Create(bitmapImage.BitmapSourceToBitmap().ToBitmapImage(ImageFormat.Jpeg)))
                                                                                                       : null;
}