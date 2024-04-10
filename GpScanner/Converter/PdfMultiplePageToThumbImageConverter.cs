using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Extensions;

namespace GpScanner.Converter;

public sealed class PdfMultiplePageToThumbImageConverter : InpcBase, IMultiValueConverter
{
    private uint maxPageCount = 2;

    public uint MaxPageCount {
        get => maxPageCount;
        set {
            if (maxPageCount != value)
            {
                maxPageCount = value;
                OnPropertyChanged(nameof(MaxPageCount));
            }
        }
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values[0] is not string pdfFilePath || !File.Exists(pdfFilePath))
        {
            return null;
        }
        try
        {
            return Task.Run(
                async () =>
                {
                    if (MaxPageCount == 0)
                    {
                        MaxPageCount = 1;
                    }
                    BitmapImage[] bitmapImages = new BitmapImage[Math.Min(PdfViewer.PdfViewer.PdfPageCount(pdfFilePath), MaxPageCount)];

                    for (int i = 0; i < bitmapImages.Length; i++)
                    {
                        bitmapImages[i] = await PdfViewer.PdfViewer.ConvertToImgAsync(pdfFilePath, i + 1, 16);
                        bitmapImages[i].Freeze();
                    }

                    DrawingVisual drawingVisual = new();
                    double totalWidth = 0;
                    using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                    {
                        foreach (BitmapImage bitmapImage in bitmapImages)
                        {
                            Rect rect = new(new Point(totalWidth, 0), new Size(bitmapImage.PixelWidth, bitmapImage.PixelHeight));
                            drawingContext.DrawImage(bitmapImage, rect);
                            totalWidth += bitmapImage.PixelWidth;
                        }
                    }

                    RenderTargetBitmap renderTargetBitmap = new((int)totalWidth, bitmapImages[0].PixelHeight, 96, 96, PixelFormats.Pbgra32);
                    renderTargetBitmap.Render(drawingVisual);
                    renderTargetBitmap.Freeze();
                    return renderTargetBitmap;
                });
        }
        catch (Exception)
        {
            return null;
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}