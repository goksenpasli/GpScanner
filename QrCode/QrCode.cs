using Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.QrCode.Internal;
using ZXing.Rendering;

namespace QrCode;

public class QrCode : InpcBase
{
    private ResultPoint[] barcodePosition;

    public ResultPoint[] BarcodePosition
    {
        get => barcodePosition;

        set
        {
            if (barcodePosition != value)
            {
                barcodePosition = value;
                OnPropertyChanged(nameof(BarcodePosition));
            }
        }
    }

    public WriteableBitmap GenerateQr(string text, int width = 120, int height = 120)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            BarcodeWriter barcodeWriter = new() { Format = BarcodeFormat.QR_CODE, Renderer = new BitmapRenderer() };
            EncodingOptions encodingOptions = new() { Width = width, Height = height, Margin = 0 };
            encodingOptions.Hints.Add(EncodeHintType.ERROR_CORRECTION, ErrorCorrectionLevel.L);
            barcodeWriter.Options = encodingOptions;
            return barcodeWriter.WriteAsWriteableBitmap(text);
        }

        return null;
    }

    public string GetImageBarcodeResult(BitmapFrame bitmapFrame)
    {
        if (bitmapFrame is not null)
        {
            BarcodeReader reader = new();
            reader.Options.TryHarder = true;
            return reader.Decode(bitmapFrame)?.Text;
        }

        return null;
    }

    public string GetImageBarcodeResult(byte[] imgbyte)
    {
        if (imgbyte is not null)
        {
            using MemoryStream ms = new(imgbyte);
            BitmapImage bitmapImage = new();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = ms;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            BarcodeReader reader = new();
            reader.Options.TryHarder = true;
            Result result = reader.Decode(bitmapImage);
            imgbyte = null;
            bitmapImage = null;
            
            return result?.Text;
        }

        return null;
    }

    public List<string> GetMultipleImageBarcodeResult(BitmapFrame bitmapFrame)
    {
        if (bitmapFrame is not null)
        {
            BarcodeReader reader = new();
            reader.Options.TryHarder = true;
            return reader.DecodeMultiple(bitmapFrame)?.Select(z => z.Text).ToList();
        }

        return null;
    }
}