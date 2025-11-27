using Extensions;
using PdfiumViewer;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace PdfViewer;

public sealed class PdfPageToThumbImageConverter : InpcBase, IMultiValueConverter
{
    public int Dpi
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Dpi));
            }
        }
    } = 20;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values[0] is string PdfFilePath && values[1] is int index && File.Exists(PdfFilePath))
        {
            try
            {
                return Task.Run(() => ConvertPdfPageToImageAsync(PdfFilePath, index, Dpi));
            }
            catch (Exception)
            {
                return null;
            }
        }
        return null;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();

    private async Task<BitmapSource> ConvertPdfPageToImageAsync(string pdfFilePath, int index, int dpi)
    {
        using PdfDocument pdfDoc = PdfDocument.Load(pdfFilePath);
        if (index < 1 || index > pdfDoc.PageCount)
        {
            return null;
        }
        int width = (int)(pdfDoc.PageSizes[index - 1].Width / 96 * dpi);
        int height = (int)(pdfDoc.PageSizes[index - 1].Height / 96 * dpi);
        return await PdfViewer.ConvertToImgAsync(pdfDoc, dpi, index - 1, width, height);
    }
}