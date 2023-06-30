﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WebPWrapper;
using static Extensions.ExtensionMethods;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace TwainControl;

public static class BitmapMethods
{
    public static WriteableBitmap AdjustBrightness(this BitmapSource bitmap, int brightness)
    {
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int stride = ((width * bitmap.Format.BitsPerPixel) + 7) / 8;
        int bytesPerPixel = bitmap.Format.BitsPerPixel / 8;
        int totalBytes = height * stride;

        byte[] pixelData = new byte[totalBytes];
        bitmap.CopyPixels(pixelData, stride, 0);

        _ = Parallel.For(
            0,
            height,
            y =>
            {
                int rowOffset = y * stride;

                for (int x = 0; x < width; x++)
                {
                    int offset = rowOffset + (x * bytesPerPixel);

                    for (int i = 0; i < bytesPerPixel; i++)
                    {
                        int value = pixelData[offset + i] + brightness;

                        if (value < 0)
                        {
                            value = 0;
                        }
                        else if (value > 255)
                        {
                            value = 255;
                        }

                        pixelData[offset + i] = (byte)value;
                    }
                }
            });

        WriteableBitmap adjustedBitmap = new(width, height, bitmap.DpiX, bitmap.DpiY, bitmap.Format, bitmap.Palette);
        adjustedBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixelData, stride, 0);
        adjustedBitmap.Freeze();
        pixelData = null;
        bitmap = null;
        return adjustedBitmap;
    }

    public static CroppedBitmap AutoCropImage(this BitmapSource bitmapSource, Color color)
    {
        int maxX = 0;
        int maxY = 0;

        int minX = bitmapSource.PixelWidth;
        int minY = bitmapSource.PixelHeight;

        int bytesPerPixel = (bitmapSource.Format.BitsPerPixel + 7) / 8;
        int stride = bytesPerPixel * bitmapSource.PixelWidth;
        byte[] pixelData = new byte[bitmapSource.PixelHeight * stride];
        bitmapSource.CopyPixels(pixelData, stride, 0);
        bitmapSource.Freeze();
        _ = Parallel.For(0, bitmapSource.PixelHeight, y =>
        {
            for (int x = 0; x < bitmapSource.PixelWidth; x++)
            {
                int offset = (y * stride) + (x * bytesPerPixel);
                Color pixelColor = Color.FromArgb(bytesPerPixel == 4 ? pixelData[offset + 3] : (byte)255, pixelData[offset + 2], pixelData[offset + 1], pixelData[offset]);
                if (pixelColor != color)
                {
                    if (x > maxX)
                    {
                        maxX = x;
                    }

                    if (x < minX)
                    {
                        minX = x;
                    }

                    if (y > maxY)
                    {
                        maxY = y;
                    }

                    if (y < minY)
                    {
                        minY = y;
                    }
                }
            }
        });
        maxX += 2;
        CroppedBitmap croppedimage = new(bitmapSource, new Int32Rect(minX, minY, maxX - minX - 1, maxY - minY - 1));
        croppedimage.Freeze();
        bitmapSource = null;
        pixelData = null;
        return croppedimage;
    }

    public static Bitmap BitmapSourceToBitmap(this BitmapSource bitmapsource)
    {
        FormatConvertedBitmap src = new();
        src.BeginInit();
        src.Source = bitmapsource;
        src.DestinationFormat = PixelFormats.Bgra32;
        src.EndInit();
        src.Freeze();
        Bitmap bitmap = new(src.PixelWidth, src.PixelHeight, PixelFormat.Format32bppArgb);
        BitmapData data = bitmap.LockBits(new Rectangle(System.Drawing.Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        src.CopyPixels(Int32Rect.Empty, data.Scan0, data.Height * data.Stride, data.Stride);
        bitmap.UnlockBits(data);
        return bitmap;
    }

    public static byte[] CaptureScreen(double coordx, double coordy, double selectionwidth, double selectionheight, ScrollViewer scrollviewer, BitmapFrame bitmapFrame)
    {
        try
        {
            coordx += scrollviewer.HorizontalOffset;
            coordy += scrollviewer.VerticalOffset;

            double widthmultiply = bitmapFrame.PixelWidth / (scrollviewer.ExtentWidth < scrollviewer.ViewportWidth ? scrollviewer.ViewportWidth : scrollviewer.ExtentWidth);
            double heightmultiply = bitmapFrame.PixelHeight / (scrollviewer.ExtentHeight < scrollviewer.ViewportHeight ? scrollviewer.ViewportHeight : scrollviewer.ExtentHeight);

            Int32Rect ınt32Rect = new((int)(coordx * widthmultiply), (int)(coordy * heightmultiply), (int)(selectionwidth * widthmultiply), (int)(selectionheight * heightmultiply));
            CroppedBitmap cb = new(bitmapFrame, ınt32Rect);
            bitmapFrame = null;
            return cb.ToTiffJpegByteArray(Format.Png);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static RenderTargetBitmap CombineImages(this List<ScannedImage> images, Orientation orientation)
    {
        int totalWidth = 0;
        int totalHeight = 0;

        foreach (ScannedImage image in images)
        {
            totalWidth = Math.Max(totalWidth, image.Resim.PixelWidth);
            totalHeight = Math.Max(totalHeight, image.Resim.PixelHeight);
        }

        if (orientation == Orientation.Horizontal)
        {
            totalWidth *= images.Count;
        }
        else
        {
            totalHeight *= images.Count;
        }

        DrawingVisual drawingVisual = new();
        using (DrawingContext drawingContext = drawingVisual.RenderOpen())
        {
            int curWidth = 0;
            int curHeight = 0;
            foreach (ScannedImage image in images)
            {
                Rect rect = new(new Point(curWidth, curHeight), new Size(image.Resim.PixelWidth, image.Resim.PixelHeight));
                drawingContext.DrawImage(image.Resim, rect);
                if (orientation == Orientation.Horizontal)
                {
                    curWidth += image.Resim.PixelWidth;
                }
                else
                {
                    curHeight += image.Resim.PixelHeight;
                }
            }
        }

        RenderTargetBitmap renderTargetBitmap = new(totalWidth, totalHeight, 96, 96, PixelFormats.Pbgra32);
        renderTargetBitmap.Render(drawingVisual);
        renderTargetBitmap.Freeze();
        return renderTargetBitmap;
    }

    public static async Task<BitmapFrame> FlipImageAsync(this BitmapFrame bitmapFrame, double angle)
    {
        TransformedBitmap transformedBitmap = null;
        switch (angle)
        {
            case 1:
                transformedBitmap = new TransformedBitmap(bitmapFrame, new ScaleTransform(angle, -1, 0, 0));
                break;

            case -1:
                transformedBitmap = new TransformedBitmap(bitmapFrame, new ScaleTransform(angle, 1, 0, 0));
                break;
        }

        transformedBitmap.Freeze();
        return await Task.Run(
            () =>
            {
                BitmapFrame frame = BitmapFrame.Create(transformedBitmap);
                frame.Freeze();
                return frame;
            });
    }

    public static async Task<BitmapFrame> GenerateImageDocumentBitmapFrameAsync(MemoryStream ms, bool deskew = false)
    {
        BitmapImage image = new();
        image.BeginInit();
        _ = ms.Seek(0, SeekOrigin.Begin);
        image.CacheOption = BitmapCacheOption.None;
        image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.DelayCreation;
        image.StreamSource = ms;
        image.EndInit();
        image.Freeze();

        RenderTargetBitmap skewedimage = null;
        if (deskew)
        {
            double deskewAngle = Deskew.GetDeskewAngle(image);
            skewedimage = await image.RotateImageAsync(deskewAngle);
            skewedimage.Freeze();
        }

        return deskew ? BitmapFrame.Create(skewedimage) : BitmapFrame.Create(image);
    }

    public static WriteableBitmap InvertBitmap(this BitmapSource bitmap)
    {
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int stride = ((width * bitmap.Format.BitsPerPixel) + 7) / 8;
        int bytesPerPixel = bitmap.Format.BitsPerPixel / 8;
        int totalBytes = height * stride;

        byte[] pixelData = new byte[totalBytes];
        bitmap.CopyPixels(pixelData, stride, 0);

        _ = Parallel.For(
            0,
            height,
            y =>
            {
                int offset = y * stride;

                for (int x = 0; x < width * bytesPerPixel; x++)
                {
                    pixelData[offset + x] = (byte)(255 - pixelData[offset + x]);
                }
            });

        WriteableBitmap invertedBitmap = new(width, height, bitmap.DpiX, bitmap.DpiY, bitmap.Format, bitmap.Palette);
        invertedBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixelData, stride, 0);
        invertedBitmap.Freeze();
        bitmap = null;
        pixelData = null;
        return invertedBitmap;
    }

    public static WriteableBitmap MedianFilterBitmap(this BitmapSource inputBitmap, int threshold)
    {
        int width = inputBitmap.PixelWidth;
        int height = inputBitmap.PixelHeight;
        int bytesPerPixel = (inputBitmap.Format.BitsPerPixel + 7) / 8;
        int stride = width * bytesPerPixel;
        WriteableBitmap outputBitmap = new(width, height, inputBitmap.DpiX, inputBitmap.DpiY, inputBitmap.Format, null);
        byte[] inputPixels = new byte[height * stride];
        inputBitmap.CopyPixels(inputPixels, stride, 0);

        byte[] outputPixels = new byte[inputPixels.Length];
        _ = Parallel.For(
            0,
            height,
            y =>
            {
                for (int x = 0; x < width; x++)
                {
                    int minX = Math.Max(x - (threshold / 2), 0);
                    int maxX = Math.Min(x + (threshold / 2), width - 1);
                    int minY = Math.Max(y - (threshold / 2), 0);
                    int maxY = Math.Min(y + (threshold / 2), height - 1);

                    List<byte> values = new();
                    for (int wy = minY; wy <= maxY; wy++)
                    {
                        for (int wx = minX; wx <= maxX; wx++)
                        {
                            int pixelIndex = (wy * stride) + (wx * bytesPerPixel);
                            byte pixelValue = inputPixels[pixelIndex];
                            values.Add(pixelValue);
                        }
                    }

                    values.Sort();
                    byte medianValue = values[values.Count / 2];
                    int outputIndex = (y * stride) + (x * bytesPerPixel);
                    outputPixels[outputIndex] = medianValue;
                    outputPixels[outputIndex + 1] = medianValue;
                    outputPixels[outputIndex + 2] = medianValue;
                    if (bytesPerPixel == 4)
                    {
                        outputPixels[outputIndex + 3] = 255;
                    }
                }
            });

        outputBitmap.WritePixels(new Int32Rect(0, 0, width, height), outputPixels, stride, 0);
        outputBitmap.Freeze();
        inputPixels = null;
        outputPixels = null;
        return outputBitmap;
    }

    public static WriteableBitmap ReplaceColor(this BitmapSource source, Color toReplace, Color replacement, int threshold)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        int bytesPerPixel = (source.Format.BitsPerPixel + 7) / 8;
        int stride = width * bytesPerPixel;

        byte[] sourcePixels = new byte[height * stride];
        source.CopyPixels(new Int32Rect(0, 0, width, height), sourcePixels, stride, 0);

        byte[] targetPixels = new byte[height * stride];
        Buffer.BlockCopy(sourcePixels, 0, targetPixels, 0, sourcePixels.Length);

        int pixelSize = bytesPerPixel;

        _ = Parallel.For(
            0,
            height,
            y =>
            {
                int rowOffset = y * stride;

                for (int x = 0; x < width; x++)
                {
                    int offset = rowOffset + (x * pixelSize);

                    byte b = targetPixels[offset];
                    byte g = targetPixels[offset + 1];
                    byte r = targetPixels[offset + 2];

                    if (Math.Abs(toReplace.R - r) <= threshold && Math.Abs(toReplace.G - g) <= threshold && Math.Abs(toReplace.B - b) <= threshold)
                    {
                        targetPixels[offset] = replacement.B;
                        targetPixels[offset + 1] = replacement.G;
                        targetPixels[offset + 2] = replacement.R;
                    }
                }
            });

        WriteableBitmap target = new(width, height, source.DpiX, source.DpiY, source.Format, null);
        target.WritePixels(new Int32Rect(0, 0, width, height), targetPixels, stride, 0);
        target.Freeze();
        sourcePixels = null;
        targetPixels = null;
        return target;
    }

    public static async Task<RenderTargetBitmap> RotateImageAsync(this ImageSource Source, double angle)
    {
        try
        {
            BitmapSource bitmapSource = (BitmapSource)Source;
            return await Task.Run(
                () =>
                {
                    DrawingVisual dv = new();
                    using (DrawingContext dc = dv.RenderOpen())
                    {
                        dc.PushTransform(new RotateTransform(angle, bitmapSource.PixelWidth / 2, bitmapSource.PixelHeight / 2));
                        dc.DrawImage(Source, new Rect(0, 0, bitmapSource.PixelWidth, bitmapSource.PixelHeight));
                        dc.Pop();
                    }

                    RenderTargetBitmap rtb = new(bitmapSource.PixelWidth, bitmapSource.PixelHeight, 96, 96, PixelFormats.Default);
                    rtb.Render(dv);
                    rtb.Freeze();
                    bitmapSource = null;
                    Source = null;
                    dv = null;
                    return rtb;
                });
        }
        catch (Exception ex)
        {
            Source = null;
            throw new ArgumentException(nameof(Source), ex);
        }
    }

    public static async Task<BitmapFrame> RotateImageAsync(this BitmapFrame bitmapFrame, double angle)
    {
        if (angle is not -1 and not 1)
        {
            throw new ArgumentOutOfRangeException(nameof(angle), "angle should be -1 or 1");
        }

        TransformedBitmap transformedBitmap = new(bitmapFrame, new RotateTransform(angle * 90));
        transformedBitmap.Freeze();
        bitmapFrame = null;
        return await Task.Run(
            () =>
            {
                BitmapFrame frame = BitmapFrame.Create(transformedBitmap);
                frame.Freeze();
                return frame;
            });
    }

    public static IEnumerable<int> SteppedRange(int fromInclusive, int toExclusive, int step)
    {
        for (int i = fromInclusive; i < toExclusive; i += step)
        {
            yield return i;
        }
    }

    public static RenderTargetBitmap ÜstüneResimÇiz(this ImageSource Source, Point konum, Brush brushes, double emSize = 64, string metin = null, double angle = 315, string font = "Arial")
    {
        FlowDirection flowDirection = (CultureInfo.CurrentCulture == CultureInfo.GetCultureInfo("ar-AR")) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        FormattedText formattedText =
            new(metin, CultureInfo.CurrentCulture, flowDirection, new Typeface(font), emSize, brushes) { TextAlignment = TextAlignment.Center };
        DrawingVisual dv = new();
        using (DrawingContext dc = dv.RenderOpen())
        {
            dc.DrawImage(Source, new Rect(0, 0, ((BitmapSource)Source).Width, ((BitmapSource)Source).Height));
            dc.PushTransform(new RotateTransform(angle, konum.X, konum.Y));
            dc.DrawText(formattedText, new Point(konum.X, konum.Y - (formattedText.Height / 2)));
        }

        RenderTargetBitmap rtb = new((int)((BitmapSource)Source).Width, (int)((BitmapSource)Source).Height, 96, 96, PixelFormats.Default);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    public static BitmapSource WebpDecode(this string webpresimyolu, bool fullresolution, int decodeheight)
    {
        using WebP webp = new();
        WebPDecoderOptions options = new() { use_threads = 1, bypass_filtering = 0, no_fancy_upsampling = 1 };
        using Bitmap bmp = webp.Load(webpresimyolu, options);
        BitmapImage bitmapimage = bmp.PixelFormat == PixelFormat.Format32bppArgb
            ? fullresolution ? bmp.ToBitmapImage(ImageFormat.Png) : bmp.ToBitmapImage(ImageFormat.Png, decodeheight)
            : fullresolution ? bmp.ToBitmapImage(ImageFormat.Jpeg) : bmp.ToBitmapImage(ImageFormat.Jpeg, decodeheight);
        bitmapimage.Freeze();
        return bitmapimage;
    }

    public static byte[] WebpEncode(this byte[] resim, int kalite)
    {
        try
        {
            using WebP webp = new();
            using MemoryStream ms = new(resim);
            using Bitmap bmp = System.Drawing.Image.FromStream(ms) as Bitmap;
            return bmp.PixelFormat is PixelFormat.Format24bppRgb or PixelFormat.Format32bppArgb
                ? webp.EncodeLossy(bmp, kalite)
                : webp.EncodeLossy(bmp.BitmapChangeFormat(PixelFormat.Format24bppRgb), kalite);
        }
        catch (Exception)
        {
            return null;
        }
    }
}