using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Extensions;
using Ocr;
using TwainControl.Properties;
using static Extensions.ExtensionMethods;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace TwainControl
{
    public static class BitmapMethods
    {
        public static Bitmap AdjustBrightness(this Bitmap bitmap, int brightness)
        {
            BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
            IntPtr ptr = bmpData.Scan0;
            int bytes = bmpData.Stride * bmpData.Height;
            byte[] rgb = new byte[bytes];
            Marshal.Copy(ptr, rgb, 0, bytes);
            int step = System.Drawing.Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;
            _ = Parallel.ForEach(SteppedRange(0, bytes, step), k =>
            {
                int b = rgb[k];
                int g = rgb[k + 1];
                int r = rgb[k + 2];
                b += brightness;
                g += brightness;
                r += brightness;

                switch (r)
                {
                    case > 255:
                        r = 255;
                        break;

                    case < 0:
                        r = 0;
                        break;
                }

                switch (g)
                {
                    case > 255:
                        g = 255;
                        break;

                    case < 0:
                        g = 0;
                        break;
                }

                switch (b)
                {
                    case > 255:
                        b = 255;
                        break;

                    case < 0:
                        b = 0;
                        break;
                }

                rgb[k] = (byte)b;
                rgb[k + 1] = (byte)g;
                rgb[k + 2] = (byte)r;
            });
            Marshal.Copy(rgb, 0, ptr, bytes);
            bitmap.UnlockBits(bmpData);
            rgb = null;
            return bitmap;
        }

        public static Bitmap BitmapSourceToBitmap(this BitmapSource bitmapsource)
        {
            FormatConvertedBitmap src = new();
            src.BeginInit();
            src.Source = bitmapsource;
            src.DestinationFormat = PixelFormats.Bgra32;
            src.EndInit();
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

                double widthmultiply = bitmapFrame.PixelWidth / (double)((scrollviewer.ExtentWidth < scrollviewer.ViewportWidth) ? scrollviewer.ViewportWidth : scrollviewer.ExtentWidth);
                double heightmultiply = bitmapFrame.PixelHeight / (double)((scrollviewer.ExtentHeight < scrollviewer.ViewportHeight) ? scrollviewer.ViewportHeight : scrollviewer.ExtentHeight);

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

        public static ObservableCollection<Chart> GenerateHistogram(this Bitmap b, System.Windows.Media.Brush color)
        {
            int[] histogram = new int[256];
            BitmapData bmData = null;
            ObservableCollection<Chart> chart = new();
            try
            {
                bmData = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int scanline = bmData.Stride;
                IntPtr Scan0 = bmData.Scan0;
                unsafe
                {
                    byte* p = (byte*)(void*)Scan0;
                    int nWidth = b.Width;
                    int nHeight = b.Height;
                    for (int y = 0; y < nHeight; y++)
                    {
                        for (int x = 0; x < nWidth; x++)
                        {
                            long Temp = 0;
                            if (color == System.Windows.Media.Brushes.Red)
                            {
                                histogram[Temp += p[2]]++;
                            }
                            if (color == System.Windows.Media.Brushes.Green)
                            {
                                histogram[Temp += p[1]]++;
                            }
                            if (color == System.Windows.Media.Brushes.Blue)
                            {
                                histogram[Temp += p[0]]++;
                            }
                            p += 4;
                        }
                    }
                }
                b.UnlockBits(bmData);
                foreach (int item in histogram.Take(255))
                {
                    chart.Add(new Chart() { ChartBrush = color, ChartValue = item });
                }
                bmData = null;
                return chart;
            }
            catch
            {
            }
            return null;
        }

        public static BitmapFrame GenerateImageDocumentBitmapFrame(Uri item, int decodeheight, int defaultpictureresizeratio = 100)
        {
            BitmapImage image = new();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.None;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache | BitmapCreateOptions.DelayCreation;
            image.UriSource = item;
            image.EndInit();
            image.Freeze();

            BitmapImage thumbimage = new();
            thumbimage.BeginInit();
            thumbimage.DecodePixelHeight = decodeheight / 10;
            thumbimage.CacheOption = BitmapCacheOption.None;
            thumbimage.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache | BitmapCreateOptions.DelayCreation;
            thumbimage.UriSource = item;
            thumbimage.EndInit();
            thumbimage.Freeze();
            BitmapFrame bitmapFrame = defaultpictureresizeratio != 100 ? BitmapFrame.Create(image.Resize(defaultpictureresizeratio / 100d), thumbimage) : BitmapFrame.Create(image, thumbimage);
            bitmapFrame.Freeze();
            return bitmapFrame;
        }

        public static BitmapFrame GenerateImageDocumentBitmapFrame(MemoryStream ms, Paper paper, bool deskew = false)
        {
            BitmapImage image = new();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.None;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.DelayCreation;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            BitmapFrame bitmapFrame;

            if (deskew)
            {
                RenderTargetBitmap skewedimage = image.RotateImage((double)ToolBox.GetDeskewAngle(image, true));
                skewedimage.Freeze();
                if (image.PixelWidth < image.PixelHeight)
                {
                    bitmapFrame = BitmapFrame.Create(skewedimage, image.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / paper.Width * paper.Height).BitmapSourceToBitmap().ToBitmapImage(ImageFormat.Jpeg, Settings.Default.PreviewWidth));
                    bitmapFrame.Freeze();
                    return bitmapFrame;
                }
                bitmapFrame = BitmapFrame.Create(skewedimage, image.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / paper.Height * paper.Width).BitmapSourceToBitmap().ToBitmapImage(ImageFormat.Jpeg, Settings.Default.PreviewWidth));
                bitmapFrame.Freeze();
                return bitmapFrame;
            }

            if (image.PixelWidth < image.PixelHeight)
            {
                bitmapFrame = BitmapFrame.Create(image, image.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / paper.Width * paper.Height).BitmapSourceToBitmap().ToBitmapImage(ImageFormat.Jpeg, Settings.Default.PreviewWidth));
                bitmapFrame.Freeze();
                return bitmapFrame;
            }
            bitmapFrame = BitmapFrame.Create(image, image.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / paper.Height * paper.Width).BitmapSourceToBitmap().ToBitmapImage(ImageFormat.Jpeg, Settings.Default.PreviewWidth));
            bitmapFrame.Freeze();
            return bitmapFrame;
        }

        public static ObservableCollection<Paper> GetPapers()
        {
            return new ObservableCollection<Paper>
            {
                new Paper() { Height = 84.1, PaperType = "A1", Width = 59.5 },
                new Paper() { Height = 59.5, PaperType = "A2", Width = 42 },
                new Paper() { Height = 42, PaperType = "A3", Width = 29.7 },
                new Paper() { Height = 29.7, PaperType = "A4", Width = 21 },
                new Paper() { Height = 21, PaperType = "A5", Width = 14.8 },
                new Paper() { Height = 100, PaperType = "B1", Width = 70.7 },
                new Paper() { Height = 70.7, PaperType = "B2", Width = 50 },
                new Paper() { Height = 50, PaperType = "B3", Width = 35.3 },
                new Paper() { Height = 35.3, PaperType = "B4", Width = 25 },
                new Paper() { Height = 25, PaperType = "B5", Width = 17.6},
                new Paper() { Height = 27.9, PaperType = "Letter", Width = 21.6 },
                new Paper() { Height = 35.6, PaperType = "Legal", Width = 21.6 },
                new Paper() { Height = 26.7, PaperType = "Executive", Width = 18.4 },
            };
        }

        public static unsafe Bitmap ReplaceColor(this Bitmap source, System.Windows.Media.Color toReplace, System.Windows.Media.Color replacement, int threshold)
        {
            const int pixelSize = 4; // 32 bits per pixel

            Bitmap target = new(source.Width, source.Height, PixelFormat.Format32bppArgb);

            BitmapData sourceData = null, targetData = null;

            try
            {
                sourceData = source.LockBits(new Rectangle(0, 0, source.Width, source.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                targetData = target.LockBits(new Rectangle(0, 0, target.Width, target.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                for (int y = 0; y < source.Height; ++y)
                {
                    byte* sourceRow = (byte*)sourceData.Scan0 + (y * sourceData.Stride);
                    byte* targetRow = (byte*)targetData.Scan0 + (y * targetData.Stride);

                    _ = Parallel.For(0, source.Width, x =>
                    {
                        byte b = sourceRow[(x * pixelSize) + 0];
                        byte g = sourceRow[(x * pixelSize) + 1];
                        byte r = sourceRow[(x * pixelSize) + 2];
                        byte a = sourceRow[(x * pixelSize) + 3];

                        if (toReplace.R + threshold >= r && toReplace.R - threshold <= r && toReplace.G + threshold >= g && toReplace.G - threshold <= g && toReplace.B + threshold >= b && toReplace.B - threshold <= b)
                        {
                            r = replacement.R;
                            g = replacement.G;
                            b = replacement.B;
                        }

                        targetRow[(x * pixelSize) + 0] = b;
                        targetRow[(x * pixelSize) + 1] = g;
                        targetRow[(x * pixelSize) + 2] = r;
                        targetRow[(x * pixelSize) + 3] = a;
                    });
                }
            }
            finally
            {
                if (sourceData != null)
                {
                    source.UnlockBits(sourceData);
                }

                if (targetData != null)
                {
                    target.UnlockBits(targetData);
                }
            }

            return target;
        }

        public static RenderTargetBitmap RotateImage(this ImageSource Source, double angle)
        {
            try
            {
                DrawingVisual dv = new();
                using (DrawingContext dc = dv.RenderOpen())
                {
                    dc.PushTransform(new RotateTransform(angle));
                    dc.DrawImage(Source, new Rect(0, 0, ((BitmapSource)Source).PixelWidth, ((BitmapSource)Source).PixelHeight));
                }
                RenderTargetBitmap rtb = new(((BitmapSource)Source).PixelWidth, ((BitmapSource)Source).PixelHeight, 96, 96, PixelFormats.Default);
                rtb.Render(dv);
                rtb.Freeze();
                Source = null;
                dv = null;
                return rtb;
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message);
                return null;
            }
        }

        public static async Task<BitmapFrame> RotateImageAsync(this BitmapFrame bitmapFrame, double angle, double thumbresizeratio)
        {
            TransformedBitmap transformedBitmap = new(bitmapFrame, new RotateTransform(angle * 90));
            transformedBitmap.Freeze();
            return await Task.Run(async () =>
            {
                BitmapSource thumbnail = await transformedBitmap.ResizeAsync(thumbresizeratio);
                BitmapFrame frame = BitmapFrame.Create(transformedBitmap, thumbnail);
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

        public static RenderTargetBitmap ÜstüneResimÇiz(this ImageSource Source, System.Windows.Point konum, System.Windows.Media.Brush brushes, double emSize = 64, string metin = null, double angle = 315, string font = "Arial")
        {
            FormattedText formattedText = new(metin, CultureInfo.GetCultureInfo("tr-TR"), FlowDirection.LeftToRight, new Typeface(font), emSize, brushes) { TextAlignment = TextAlignment.Center };
            DrawingVisual dv = new();
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawImage(Source, new Rect(0, 0, ((BitmapSource)Source).Width, ((BitmapSource)Source).Height));
                dc.PushTransform(new RotateTransform(angle, konum.X, konum.Y));
                dc.DrawText(formattedText, new System.Windows.Point(konum.X, konum.Y - (formattedText.Height / 2)));
            }

            RenderTargetBitmap rtb = new((int)((BitmapSource)Source).Width, (int)((BitmapSource)Source).Height, 96, 96, PixelFormats.Default);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }
    }
}