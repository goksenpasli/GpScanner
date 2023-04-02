using System;
using System.Globalization;
using System.Windows.Data;

namespace PdfViewer
{
    public sealed class PageNumberOneUpConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is int page ? page + 1 : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}