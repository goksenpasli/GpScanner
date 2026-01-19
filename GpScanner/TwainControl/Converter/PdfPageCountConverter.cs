using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Data;
using TwainControl.Properties;

namespace TwainControl.Converter;

public sealed class PdfPageCountConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is string path && File.Exists(path) && Settings.Default.ShowDocumentPageCount ? GetPageCount(path) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();

    private async Task<int> GetPageCount(string path)
    {
        return await Task.Run(
            () =>
            {
                ILoadFileHandler imgFileHandler = new ImageFileHandler();
                if (imgFileHandler.IsValidFile(path))
                {
                    return imgFileHandler.GetPageCount(path);
                }
                ILoadFileHandler cbrFileHandler = new CbrCbzFileHandler();
                if (cbrFileHandler.IsValidFile(path))
                {
                    return cbrFileHandler.GetPageCount(path);
                }
                ILoadFileHandler tifFileHandler = new TiffFileHandler();
                if (tifFileHandler.IsValidFile(path))
                {
                    return tifFileHandler.GetPageCount(path);
                }
                ILoadFileHandler webpFileHandler = new WebpFileHandler();
                if (webpFileHandler.IsValidFile(path))
                {
                    return webpFileHandler.GetPageCount(path);
                }
                ILoadFileHandler xpsFileHandler = new XpsFileHandler();
                if (xpsFileHandler.IsValidFile(path))
                {
                    return xpsFileHandler.GetPageCount(path);
                }
                ILoadFileHandler pdfFileHandler = new PdfFileHandler();
                return pdfFileHandler.IsValidFile(path) ? pdfFileHandler.GetPageCount(path) : 0;
            });
    }
}