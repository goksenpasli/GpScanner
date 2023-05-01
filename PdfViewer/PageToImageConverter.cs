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
            if (values[0] is byte[] PdfData && values[1] is int index)
            {
                try
                {
                    return Task.Run(async () =>
                    {
                        if (PdfData != null)
                        {
                            BitmapSource bitmapImage = await PdfViewer.ConvertToImgAsync(PdfData, index, 9).ConfigureAwait(false);
                            bitmapImage.Freeze();
                            GC.Collect();
                            return bitmapImage;
                        }
                        return null;
                    }).ConfigureAwait(false).GetAwaiter().GetResult();
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