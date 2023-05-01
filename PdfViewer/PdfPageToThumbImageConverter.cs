using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Extensions;

namespace PdfViewer
{
    public sealed class PdfPageToThumbImageConverter : InpcBase, IMultiValueConverter
    {
        public int Dpi {
            get => dpi;

            set {
                if (dpi != value)
                {
                    dpi = value;
                    OnPropertyChanged(nameof(Dpi));
                }
            }
        }

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
                            BitmapSource bitmapImage = await PdfViewer.ConvertToImgAsync(PdfData, index, Dpi).ConfigureAwait(false);
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

        private int dpi = 9;
    }
}