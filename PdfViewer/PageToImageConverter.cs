using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Data;

namespace PdfViewer {
    public sealed class PageToImageConverter : IMultiValueConverter {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            if (values[0] is string filename && values[1] is int index) {
                try {
                    return Task.Run(async () => {
                        byte[] data = await PdfViewer.ReadAllFileAsync(filename);
                        return await PdfViewer.ConvertToImgAsync(data, index, 8);
                    });
                }
                catch (Exception) {
                    return null;
                }
            }
            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}