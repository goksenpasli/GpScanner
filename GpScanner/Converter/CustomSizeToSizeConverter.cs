using GpScanner.Properties;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GpScanner.Converter
{
    public sealed class CustomSizeToSizeConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) => values[0] is double width && values[1] is double height ? new Size(width, height) : Settings.Default.PreviewIndex;

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
