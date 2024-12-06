using PdfiumViewer;
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Data;

namespace TwainControl.Converter;

public sealed class PdfPageCountConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is string path ? GetPageCount(path) : 0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();

    private async Task<int?> GetPageCount(string path)
    {
        return await Task.Run(
            () =>
            {
                if (!PdfViewer.PdfViewer.IsValidPdfFile(path))
                {
                    return 0;
                }
                using PdfDocument pdfDocument = PdfDocument.Load(path);
                return pdfDocument?.PageCount;
            });
    }
}