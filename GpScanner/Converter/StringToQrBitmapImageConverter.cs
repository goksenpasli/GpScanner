using System;
using System.Globalization;
using System.Windows.Data;
using GpScanner.ViewModel;

namespace GpScanner.Converter
{
    public sealed class StringToQrBitmapImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string data && !string.IsNullOrWhiteSpace(data))
            {
                return GpScannerViewModel.GenerateQr(data);
            }
            return GpScannerViewModel.GenerateQr("Goksen");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}