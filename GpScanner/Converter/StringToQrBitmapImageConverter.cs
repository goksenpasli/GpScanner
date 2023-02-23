using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using GpScanner.ViewModel;

namespace GpScanner.Converter
{
    public sealed class StringToQrBitmapImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is string data && !string.IsNullOrWhiteSpace(data)
                ? GpScannerViewModel.GenerateQr(data)
                : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is WriteableBitmap bitmapImage) ? GpScannerViewModel.GetImageBarcodeResult(BitmapFrame.Create(bitmapImage)).Text : null;
        }
    }
}