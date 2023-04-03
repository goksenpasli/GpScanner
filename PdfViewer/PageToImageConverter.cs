using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace PdfViewer
{
    public sealed class PageToImageConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is string filename && values[1] is int index)
            {
                try
                {
                    return Task.Run(async () =>
                    {
                        byte[] data = await PdfViewer.ReadAllFileAsync(filename);
                        BitmapImage bitmapImage = await PdfViewer.ConvertToImgAsync(data, index, 9);
                        data = null;
                        GC.Collect();
                        return bitmapImage;
                    });
                }
                catch (Exception)
                {
                    return null;
                }
            }
            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}