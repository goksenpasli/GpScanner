using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.Globalization;
using System.Windows.Data;

namespace TwainControl.Converter;

public sealed class PdfPageCountConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string path)
        {
            using PdfDocument pdfDocument = PdfReader.Open(path, PdfDocumentOpenMode.InformationOnly);
            return pdfDocument?.PageCount;
        }
        return Translation.GetResStringValue("ERROR");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}