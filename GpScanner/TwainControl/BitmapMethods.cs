using Extensions;
using PdfCompressor;
using System;
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
using Image = System.Drawing.Image;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace TwainControl;

public static class BitmapMethods
{
    public static WriteableBitmap ApplyHueSaturationLightness(this BitmapSource source, double hue, double saturation, double lightness)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        int stride = ((width * source.Format.BitsPerPixel) + 7) / 8;
        int bytesPerPixel = source.Format.BitsPerPixel / 8;
        byte[] pixelData = new byte[height * stride];
        source.CopyPixels(pixelData, stride, 0);

        _ = Parallel.For(
            0,
            pixelData.Length / bytesPerPixel,
            i =>
            {
                int offset = i * bytesPerPixel;

                byte r = pixelData[offset + 2];
                byte g = pixelData[offset + 1];
                byte b = pixelData[offset];

                RgbToHsv(r, g, b, out double h, out double s, out double v);

                h = (h + hue) % 1.0;
                s *= saturation;
                v *= lightness;

                HsvToRgb(h, s, v, out byte newR, out byte newG, out byte newB);

                pixelData[offset + 2] = newR;
                pixelData[offset + 1] = newG;
                pixelData[offset] = newB;
            });

        WriteableBitmap modifiedBitmap = new(width, height, source.DpiX, source.DpiY, source.Format, source.Palette);
        modifiedBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixelData, width * bytesPerPixel, 0);
        modifiedBitmap.Freeze();
        pixelData = null;
        source = null;
        return modifiedBitmap;
    }

    public static BitmapSource AutoCropImage(this BitmapSource source, byte threshold = 140)
    {
        if (source.Format != PixelFormats.Bgra32)
        {
            source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        }

        WriteableBitmap wb = new(source);

        int width = wb.PixelWidth;
        int height = wb.PixelHeight;
        int stride = width * 4;

        byte[] pixels = new byte[height * stride];
        wb.CopyPixels(pixels, stride, 0);

        bool IsRowWhite(int y)
        {
            int row = y * stride;
            for (int x = 0; x < width; x++)
            {
                int i = row + (x * 4);
                byte gray = (byte)((pixels[i] + pixels[i + 1] + pixels[i + 2]) / 3);
                if (gray < threshold)
                {
                    return false;
                }
            }
            return true;
        }

        bool IsColumnWhite(int x)
        {
            for (int y = 0; y < height; y++)
            {
                int i = (y * stride) + (x * 4);
                byte gray = (byte)((pixels[i] + pixels[i + 1] + pixels[i + 2]) / 3);
                if (gray < threshold)
                {
                    return false;
                }
            }
            return true;
        }

        int top = 0;
        while (top < height && IsRowWhite(top))
        {
            top++;
        }

        int bottom = height - 1;
        while (bottom > top && IsRowWhite(bottom))
        {
            bottom--;
        }

        int left = 0;
        while (left < width && IsColumnWhite(left))
        {
            left++;
        }

        int right = width - 1;
        while (right > left && IsColumnWhite(right))
        {
            right--;
        }

        return left >= right || top >= bottom ? source : new CroppedBitmap(source, new Int32Rect(left, top, right - left, bottom - top));
    }

    public static Bitmap BitmapSourceToBitmap(this BitmapSource bitmapsource) => Compressor.BitmapSourceToBitmap(bitmapsource);

    public static WriteableBitmap BwAdaptiveThreshold(this BitmapSource source, int blockSize = 5, int c = 25)
    {
        if (blockSize % 2 == 0 || blockSize < 3)
        {
            throw new ArgumentException("Block size must be an odd number >= 3", nameof(blockSize));
        }

        int width = source.PixelWidth;
        int height = source.PixelHeight;
        int stride = width;
        int halfBlock = blockSize / 2;

        FormatConvertedBitmap graySource = new(source, PixelFormats.Gray8, null, 0);
        byte[] pixels = new byte[width * height];
        graySource.CopyPixels(pixels, stride, 0);

        byte[] resultPixels = new byte[width * height];

        _ = Parallel.For(
            halfBlock,
            height - halfBlock,
            y =>
            {
                int yStride = y * stride;
                for (int x = halfBlock; x < width - halfBlock; x++)
                {
                    int sum = 0;
                    int count = 0;

                    for (int dy = -halfBlock; dy <= halfBlock; dy++)
                    {
                        int rowOffset = (y + dy) * stride;
                        for (int dx = -halfBlock; dx <= halfBlock; dx++)
                        {
                            sum += pixels[rowOffset + x + dx];
                            count++;
                        }
                    }

                    int mean = sum / count;
                    byte current = pixels[yStride + x];
                    resultPixels[yStride + x] = (byte)(current < (mean - c) ? 0 : 255);
                }
            });

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (y < halfBlock || y >= height - halfBlock || x < halfBlock || x >= width - halfBlock)
                {
                    resultPixels[(y * stride) + x] = 255;
                }
            }
        }

        WriteableBitmap result = new(width, height, 96, 96, PixelFormats.Gray8, null);
        result.WritePixels(new Int32Rect(0, 0, width, height), resultPixels, stride, 0);

        return result;
    }

    public static byte[] CaptureScreen(this BitmapFrame bitmapFrame, double coordx, double coordy, double selectionwidth, double selectionheight, ScrollViewer scrollviewer)
    {
        try
        {
            if (scrollviewer.ExtentWidth < scrollviewer.ViewportWidth)
            {
                coordx -= (scrollviewer.ViewportWidth - scrollviewer.ExtentWidth) / 2;
            }
            if (scrollviewer.ExtentHeight < scrollviewer.ViewportHeight)
            {
                coordy -= (scrollviewer.ViewportHeight - scrollviewer.ExtentHeight) / 2;
            }
            double widthmultiply = bitmapFrame.PixelWidth / scrollviewer.ExtentWidth;
            double heightmultiply = bitmapFrame.PixelHeight / scrollviewer.ExtentHeight;
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

    public static async Task<BitmapImage> FlipImageAsync(this BitmapFrame bitmapFrame, double angle)
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
                BitmapImage frame = BitmapFrame.Create(transformedBitmap).ToBitmapImage();
                frame.Freeze();
                transformedBitmap = null;
                return frame;
            });
    }

    public static BitmapFrame GenerateBitmapFrameFromMemoryStream(this MemoryStream ms)
    {
        using (ms)
        {
            BitmapImage image = new();
            image.BeginInit();
            ms.Position = 0;
            image.CacheOption = BitmapCacheOption.None;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.DelayCreation;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            BitmapFrame bitmapFrame = BitmapFrame.Create(image.ToBitmapImage());
            bitmapFrame.Freeze();
            return bitmapFrame;
        }
    }

    public static WriteableBitmap InvertBitmap(this BitmapSource bitmap)
    {
        if (bitmap is null)
        {
            return null;
        }
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
        invertedBitmap?.Freeze();
        bitmap = null;
        pixelData = null;
        return invertedBitmap;
    }

    public static WriteableBitmap MedianFilterBitmap(this BitmapSource inputBitmap, int windowSize)
    {
        int width = inputBitmap.PixelWidth;
        int height = inputBitmap.PixelHeight;
        int bytesPerPixel = (inputBitmap.Format.BitsPerPixel + 7) / 8;
        int stride = width * bytesPerPixel;
        WriteableBitmap output = new(width, height, inputBitmap.DpiX, inputBitmap.DpiY, inputBitmap.Format, null);
        byte[] inputPixels = new byte[height * stride];
        byte[] outputPixels = new byte[inputPixels.Length];

        inputBitmap.CopyPixels(inputPixels, stride, 0);

        int radius = windowSize / 2;
        _ = new int[256];

        _ = Parallel.For(
            0,
            height,
            () => new int[256],
            (y, state, localHist) =>
            {
                int yMin = Math.Max(0, y - radius);
                int yMax = Math.Min(height - 1, y + radius);

                for (int x = 0; x < width; x++)
                {
                    Array.Clear(localHist, 0, 256);

                    int xMin = Math.Max(0, x - radius);
                    int xMax = Math.Min(width - 1, x + radius);

                    for (int yy = yMin; yy <= yMax; yy++)
                    {
                        int idx = (yy * stride) + (xMin * bytesPerPixel);

                        for (int xx = xMin; xx <= xMax; xx++)
                        {
                            byte val = inputPixels[idx];
                            localHist[val]++;
                            idx += bytesPerPixel;
                        }
                    }

                    int count = 0;
                    int target = (xMax - xMin + 1) * (yMax - yMin + 1) / 2;
                    byte median = 0;

                    for (int i = 0; i < 256; i++)
                    {
                        count += localHist[i];
                        if (count > target)
                        {
                            median = (byte)i;
                            break;
                        }
                    }

                    int outIdx = (y * stride) + (x * bytesPerPixel);

                    outputPixels[outIdx] = median;
                    if (bytesPerPixel >= 3)
                    {
                        outputPixels[outIdx + 1] = median;
                        outputPixels[outIdx + 2] = median;
                    }
                    if (bytesPerPixel == 4)
                    {
                        outputPixels[outIdx + 3] = 255;
                    }
                }

                return localHist;
            },
            _ =>
            {
            });

        output.WritePixels(new Int32Rect(0, 0, width, height), outputPixels, stride, 0);
        output.Freeze();

        return output;
    }

    public static WriteableBitmap RemoveVerticalLines(this WriteableBitmap source, int sensitivity = 6)
    {
        WriteableBitmap colorBmp = source.Format != PixelFormats.Bgra32 ? new WriteableBitmap(new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0)) : source;
        int width = colorBmp.PixelWidth;
        int height = colorBmp.PixelHeight;
        int cStride = colorBmp.BackBufferStride;

        FormatConvertedBitmap grayBmp = new(colorBmp, PixelFormats.Gray8, null, 0);
        WriteableBitmap gray = new(grayBmp);
        int gStride = gray.BackBufferStride;

        long[] columnSums = new long[width];
        bool[] isLine = new bool[width];
        const int windowSize = 15;

        gray.Lock();
        colorBmp.Lock();

        try
        {
            unsafe
            {
                byte* gPtr = (byte*)gray.BackBuffer;
                byte* cPtr = (byte*)colorBmp.BackBuffer;

                _ = Parallel.For(
                    0,
                    width,
                    x =>
                    {
                        long sum = 0;
                        for (int y = 0; y < height; y++)
                        {
                            sum += gPtr[(y * gStride) + x];
                        }

                        columnSums[x] = sum / height;
                    });

                for (int x = 0; x < width; x++)
                {
                    long neighborSum = 0;
                    int count = 0;

                    for (int k = x - windowSize; k <= x + windowSize; k++)
                    {
                        if (k >= 0 && k < width && k != x)
                        {
                            neighborSum += columnSums[k];
                            count++;
                        }
                    }

                    long avg = (count > 0) ? (neighborSum / count) : columnSums[x];

                    if (columnSums[x] < avg - (sensitivity * 3))
                    {
                        isLine[x] = true;
                    }
                }

                _ = Parallel.For(
                    0,
                    height,
                    y =>
                    {
                        int rowC = y * cStride;

                        for (int x = 0; x < width; x++)
                        {
                            if (!isLine[x])
                            {
                                continue;
                            }

                            int lx = x - 1;
                            while (lx >= 0 && isLine[lx])
                            {
                                lx--;
                            }

                            int rx = x + 1;
                            while (rx < width && isLine[rx])
                            {
                                rx++;
                            }

                            byte r, g, b, a;

                            if (lx >= 0 && rx < width)
                            {
                                byte* left = cPtr + rowC + (lx * 4);
                                byte* right = cPtr + rowC + (rx * 4);

                                b = (byte)((left[0] + right[0]) / 2);
                                g = (byte)((left[1] + right[1]) / 2);
                                r = (byte)((left[2] + right[2]) / 2);
                                a = (byte)((left[3] + right[3]) / 2);
                            }
                            else if (lx >= 0)
                            {
                                byte* left = cPtr + rowC + (lx * 4);
                                b = left[0];
                                g = left[1];
                                r = left[2];
                                a = left[3];
                            }
                            else if (rx < width)
                            {
                                byte* right = cPtr + rowC + (rx * 4);
                                b = right[0];
                                g = right[1];
                                r = right[2];
                                a = right[3];
                            }
                            else
                            {
                                continue;
                            }

                            byte* dest = cPtr + rowC + (x * 4);
                            dest[0] = b;
                            dest[1] = g;
                            dest[2] = r;
                            dest[3] = a;
                        }
                    });
            }

            colorBmp.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            gray.Unlock();
            colorBmp.Unlock();
        }

        return colorBmp;
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
        target?.WritePixels(new Int32Rect(0, 0, width, height), targetPixels, stride, 0);
        target.Freeze();
        sourcePixels = null;
        targetPixels = null;
        return target;
    }

    public static async Task<BitmapImage> RotateImageAsync(this BitmapFrame bitmapFrame, double angle)
    {
        if (angle is not -1 and not 1 and not 2 and not -2)
        {
            throw new ArgumentOutOfRangeException(nameof(angle), "angle should be -1 or 1 or -2 or 2");
        }

        TransformedBitmap transformedBitmap = new(bitmapFrame, new RotateTransform(angle * 90));
        transformedBitmap.Freeze();
        bitmapFrame = null;
        return await Task.Run(
            () =>
            {
                BitmapImage bitmapimage = transformedBitmap.ToBitmapImage();
                bitmapimage?.Freeze();
                return bitmapimage;
            });
    }

    public static async Task<BitmapImage> RotateImageAsync(this ImageSource Source, double angle, Brush backgroundbrush = null)
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
                        Rect rect = new(0, 0, bitmapSource.PixelWidth, bitmapSource.PixelHeight);
                        if (backgroundbrush is not null)
                        {
                            dc.DrawRectangle(backgroundbrush, null, rect);
                        }
                        dc.PushTransform(new RotateTransform(angle, bitmapSource.PixelWidth / 2, bitmapSource.PixelHeight / 2));
                        dc.DrawImage(Source, rect);
                        dc.Pop();
                    }

                    RenderTargetBitmap rtb = new(bitmapSource.PixelWidth, bitmapSource.PixelHeight, 96, 96, PixelFormats.Default);
                    rtb.Render(dv);
                    rtb.Freeze();
                    BitmapImage bitmapimage = rtb.ToBitmapImage();
                    bitmapimage?.Freeze();
                    bitmapSource = null;
                    Source = null;
                    dv = null;
                    rtb = null;
                    return bitmapimage;
                });
        }
        catch (Exception ex)
        {
            Source = null;
            throw new ArgumentException(ex?.Message);
        }
    }

    public static IEnumerable<int> SteppedRange(int fromInclusive, int toExclusive, int step)
    {
        for (int i = fromInclusive; i < toExclusive; i += step)
        {
            yield return i;
        }
    }

    public static RenderTargetBitmap ÜstüneMetinÇiz(this ImageSource Source, Point konum, Brush brushes, DpiScale dpiScale, double emSize = 64, string metin = null, double angle = 315, string font = "Arial")
    {
        FlowDirection flowDirection = CultureInfo.CurrentCulture == CultureInfo.GetCultureInfo("ar-AR") ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        FormattedText formattedText = new(metin, CultureInfo.CurrentCulture, flowDirection, new Typeface(font), emSize, brushes, dpiScale.PixelsPerDip) { TextAlignment = TextAlignment.Center };
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
        using Bitmap bmp = webp?.Load(webpresimyolu, options);
        BitmapImage bitmapimage = bmp.PixelFormat == PixelFormat.Format32bppArgb
                                  ? fullresolution ? bmp.ToBitmapImage(ImageFormat.Png) : bmp.ToBitmapImage(ImageFormat.Png, decodeheight)
                                  : fullresolution ? bmp.ToBitmapImage(ImageFormat.Jpeg) : bmp.ToBitmapImage(ImageFormat.Jpeg, decodeheight);
        bitmapimage?.Freeze();
        return bitmapimage;
    }

    public static byte[] WebpEncode(this byte[] resim, int kalite)
    {
        try
        {
            using WebP webp = new();
            using MemoryStream ms = new(resim);
            using Bitmap bmp = Image.FromStream(ms) as Bitmap;
            resim = null;
            return bmp.PixelFormat is PixelFormat.Format24bppRgb or PixelFormat.Format32bppArgb ? webp.EncodeLossy(bmp, kalite) : webp.EncodeLossy(bmp.BitmapChangeFormat(PixelFormat.Format24bppRgb), kalite);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void HsvToRgb(double h, double s, double v, out byte r, out byte g, out byte b)
    {
        if (s == 0)
        {
            r = (byte)(v * 255);
            g = (byte)(v * 255);
            b = (byte)(v * 255);
            return;
        }

        double c = v * s;
        double x = c * (1 - Math.Abs((h * 6 % 2) - 1));
        double m = v - c;

        double rf, gf, bf;
        if (h < 1.0 / 6.0)
        {
            rf = c;
            gf = x;
            bf = 0;
        }
        else if (h < 2.0 / 6.0)
        {
            rf = x;
            gf = c;
            bf = 0;
        }
        else if (h < 3.0 / 6.0)
        {
            rf = 0;
            gf = c;
            bf = x;
        }
        else if (h < 4.0 / 6.0)
        {
            rf = 0;
            gf = x;
            bf = c;
        }
        else if (h < 5.0 / 6.0)
        {
            rf = x;
            gf = 0;
            bf = c;
        }
        else
        {
            rf = c;
            gf = 0;
            bf = x;
        }

        r = (byte)((rf + m) * 255);
        g = (byte)((gf + m) * 255);
        b = (byte)((bf + m) * 255);
    }

    private static void RgbToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
    {
        double rf = r / 255.0;
        double gf = g / 255.0;
        double bf = b / 255.0;

        double max = Math.Max(rf, Math.Max(gf, bf));
        double min = Math.Min(rf, Math.Min(gf, bf));
        double delta = max - min;

        h = delta == 0 ? 0 : max == rf ? (gf - bf) / delta % 6.0 : max == gf ? ((bf - rf) / delta) + 2.0 : ((rf - gf) / delta) + 4.0;

        h /= 6.0;

        s = max == 0 ? 0 : delta / max;

        v = max;
    }
}