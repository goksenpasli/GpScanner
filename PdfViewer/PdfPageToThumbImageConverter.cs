﻿using Extensions;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace PdfViewer;

public sealed class PdfPageToThumbImageConverter : InpcBase, IMultiValueConverter
{
    private int dpi = 16;

    public int Dpi
    {
        get => dpi;

        set
        {
            if (dpi != value)
            {
                dpi = value;
                OnPropertyChanged(nameof(Dpi));
            }
        }
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values[0] is string PdfFilePath && values[1] is int index && File.Exists(PdfFilePath))
        {
            try
            {
                return Task.Run(
                    async () =>
                    {
                        BitmapImage bitmapImage = await PdfViewer.ConvertToImgAsync(PdfFilePath, index, Dpi);
                        if (bitmapImage == null)
                        {
                            return null;
                        }
                        bitmapImage.Freeze();
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

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}