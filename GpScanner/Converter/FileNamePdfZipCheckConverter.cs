using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace GpScanner.Converter
{
    public sealed class FileNamePdfZipCheckConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is string filename && (string.Equals(Path.GetExtension(filename), ".pdf", StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(filename), ".zip", StringComparison.OrdinalIgnoreCase));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}