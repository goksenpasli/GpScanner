using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Color = System.Drawing.Color;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Size = System.Windows.Size;

namespace Extensions;

public static class ExtensionMethods
{
    private static readonly Random _random = new();

    public enum Format
    {
        Tiff = 0,

        TiffRenkli = 1,

        Jpg = 2,

        Png = 3
    }

    public static Bitmap BitmapChangeFormat(this Bitmap bitmap, PixelFormat format)
    {
        Rectangle rect = new(0, 0, bitmap.Width, bitmap.Height);
        return bitmap.Clone(rect, format);
    }

    public static bool Contains(this string source, string toCheck, StringComparison comp) => source?.IndexOf(toCheck, comp) >= 0;

    public static Bitmap ConvertBlackAndWhite(this Bitmap bitmap, int bWthreshold = 160, bool grayscale = false)
    {
        unsafe
        {
    BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
    if (bitmapData == null)
    {
        return bitmap;
    }
    int bytesPerPixel = Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;
    int heightInPixels = bitmapData.Height;
    int widthInBytes = bitmapData.Width * bytesPerPixel;
    byte* ptrFirstPixel = (byte*)bitmapData.Scan0;
    _ = Parallel.For(
        0,
        heightInPixels,
        y =>
        {
            byte* currentLine = ptrFirstPixel + (y * bitmapData.Stride);
            for (int x = 0; x < widthInBytes; x += bytesPerPixel)
            {
                byte gray = (byte)((currentLine[x] * 0.299) + (currentLine[x + 1] * 0.587) + (currentLine[x + 2] * 0.114));
                if (grayscale)
                {
                    currentLine[x] = gray;
                    currentLine[x + 1] = gray;
                    currentLine[x + 2] = gray;
                }
                else
                {
                    currentLine[x] = (byte)(gray < bWthreshold ? 0 : 255);
                    currentLine[x + 1] = (byte)(gray < bWthreshold ? 0 : 255);
                    currentLine[x + 2] = (byte)(gray < bWthreshold ? 0 : 255);
                }
            }
        });
    bitmap.UnlockBits(bitmapData);
    bitmapData = null;
    return bitmap;
        }
    }

    public static Brush ConvertToBrush(this Color color)
    {
        System.Windows.Media.Color convertedcolor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
        return new SolidColorBrush(convertedcolor);
    }

    public static Color ConvertToColor(this Brush color)
    {
        SolidColorBrush sb = (SolidColorBrush)color;
        return Color.FromArgb(sb.Color.A, sb.Color.R, sb.Color.G, sb.Color.B);
    }

    public static ConcurrentBag<string> DirSearch(this string path, string pattern = "*.*")
    {
        ConcurrentBag<string> filesNames = [];

        ConcurrentBag<string> pendingQueue = [path];

        _ = Parallel.ForEach(
            pendingQueue,
            currentPath =>
            {
                try
                {
                    List<string> files = Directory.EnumerateFiles(currentPath, pattern).ToList();
                    _ = Parallel.ForEach(files, fileName => filesNames.Add(fileName));

                    List<string> directories = Directory.EnumerateDirectories(currentPath).ToList();
                    _ = Parallel.ForEach(directories, directory => pendingQueue.Add(directory));
                }
                catch (UnauthorizedAccessException)
                {
                }
            });

        return filesNames;
    }

    public static IEnumerable<string> FilterFiles(this string path, params string[] exts) => exts.SelectMany(ext => Directory.EnumerateFiles(path, ext, SearchOption.TopDirectoryOnly));

    public static bool IsEmptyPage(this Bitmap bitmap, double emptythreshold = 25)
    {
        double total = 0, totalVariance = 0;
        int count = 0;
        double stdDev = 0;
        BitmapData bmData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
        if (bmData != null)
        {
            int stride = bmData.Stride;
            unsafe
            {
    int bytesPerPixel = Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;
    byte* p = (byte*)(void*)bmData.Scan0;
    int nOffset = stride - (bitmap.Width * 3);
    int widthInBytes = bmData.Width * bytesPerPixel;
    for (int y = 0; y < bmData.Height; ++y)
    {
        for (int x = 0; x < widthInBytes; x += bytesPerPixel)
        {
            count++;
            byte blue = p[0];
            byte green = p[1];
            byte red = p[2];

            int pixelValue = red + green + blue;
            total += pixelValue;
            double avg = total / count;
            totalVariance += Math.Pow(pixelValue - avg, 2);
            stdDev = Math.Sqrt(totalVariance / count);

            p += 3;
        }

        p += nOffset;
    }
            }

            bitmap.UnlockBits(bmData);
            bmData = null;
        }

        bitmap = null;
        return stdDev < emptythreshold;
    }

    public static void OpenFolderAndSelectItem(string folderPath, string file)
    {
        Helpers.SHParseDisplayName(folderPath, IntPtr.Zero, out IntPtr nativeFolder, 0, out _);

        if (nativeFolder == IntPtr.Zero)
        {
            return;
        }

        Helpers.SHParseDisplayName(Path.Combine(folderPath, file), IntPtr.Zero, out IntPtr nativeFile, 0, out _);

        IntPtr[] fileArray = nativeFile == IntPtr.Zero ? [] : [nativeFile];
        _ = Helpers.SHOpenFolderAndSelectItems(nativeFolder, (uint)fileArray.Length, fileArray, 0);

        Marshal.FreeCoTaskMem(nativeFolder);
        if (nativeFile != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(nativeFile);
        }
    }

    public static void PopupOpened(DependencyPropertyChangedEventArgs f, Popup popup)
    {
        popup.Opened += (s, e) =>
                        {
                            IntPtr hwnd = ((HwndSource)PresentationSource.FromVisual(popup.Child)).Handle;

                            if (Helpers.GetWindowRect(hwnd, out Helpers.RECT rect))
                            {
                                _ = Helpers.SetWindowPos(hwnd, (bool)f.NewValue ? -1 : -2, rect.Left, rect.Top, (int)popup.Width, (int)popup.Height, 0);
                            }
                        };
    }

    public static Brush RandomColor() => new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)_random.Next(0, 256), (byte)_random.Next(0, 256), (byte)_random.Next(0, 256)));

    public static BitmapSource Resize(this BitmapSource bfPhoto, double oran)
    {
        if (bfPhoto is null)
        {
            return null;
        }

        ScaleTransform newTransform = new(oran, oran);
        newTransform.Freeze();
        TransformedBitmap tb = new();
        tb.BeginInit();
        tb.Source = bfPhoto;
        tb.Transform = newTransform;
        tb.EndInit();
        tb.Freeze();
        return tb;
    }

    public static BitmapSource Resize(this BitmapSource bfPhoto, double nWidth, double nHeight, double? rotate = null, int dpiX = 96, int dpiY = 96)
    {
        if (bfPhoto is null)
        {
            return null;
        }

        TransformGroup transformGroup = new();
        if (rotate.HasValue)
        {
            RotateTransform rotateTransform = new(rotate.Value);
            transformGroup.Children.Add(rotateTransform);
        }

        double scaleX = nWidth / bfPhoto.PixelWidth * dpiX / 96;
        double scaleY = nHeight / bfPhoto.PixelHeight * dpiY / 96;
        ScaleTransform scaleTransform = new(scaleX, scaleY);
        transformGroup.Children.Add(scaleTransform);
        TransformedBitmap tb = new(bfPhoto, transformGroup);
        tb.Freeze();
        return tb;
    }

    public static async Task<BitmapSource> ResizeAsync(this BitmapSource bfPhoto, double oran)
    {
        return bfPhoto is null
               ? null
               : await Task.Run(
            () =>
            {
                ScaleTransform newTransform = new(oran, oran);
                newTransform.Freeze();

                TransformedBitmap tb = new();
                tb.BeginInit();
                tb.Source = bfPhoto;
                tb.Transform = newTransform;
                tb.EndInit();
                tb.Freeze();

                return tb;
            });
    }

    public static string SetUniqueFile(this string path, string file, string extension, string seperator = "_")
    {
        if (seperator.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            seperator = "_";
        }

        int i;
        for (i = 1; File.Exists($@"{path}\{file}{seperator}{i}.{extension}"); i++)
        {
            _ = i + 1;
        }

        return $@"{path}\{file}{seperator}{i}.{extension}";
    }

    public static BitmapImage ToBitmapImage(this byte[] imageData)
    {
        if (imageData is null)
        {
            return null;
        }

        MemoryStream memoryStream = new(imageData);
        BitmapImage bmp = new();
        memoryStream.Position = 0;
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.None;
        bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        bmp.StreamSource = memoryStream;
        bmp.EndInit();
        if (!bmp.IsFrozen && bmp.CanFreeze)
        {
            bmp.Freeze();
        }
        return bmp;
    }

    public static BitmapImage ToBitmapImage(this BitmapSource bitmapsource, double decodeheight = 0)
    {
        if (bitmapsource is null)
        {
            return null;
        }
        JpegBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmapsource));
        MemoryStream memoryStream = new();
        encoder.Save(memoryStream);
        memoryStream.Position = 0;
        BitmapImage bitmapImage = new();
        bitmapImage.BeginInit();
        bitmapImage.CacheOption = BitmapCacheOption.None;
        bitmapImage.DecodePixelHeight = (int)decodeheight;
        bitmapImage.StreamSource = memoryStream;
        bitmapImage.EndInit();
        bitmapImage.Freeze();
        return bitmapImage;
    }

    public static BitmapImage ToBitmapImage(this Image bitmap, ImageFormat format, double decodeheight = 0)
    {
        if (bitmap is null)
        {
            return null;
        }

        MemoryStream memoryStream = new();
        bitmap.Save(memoryStream, format);
        memoryStream.Position = 0;
        BitmapImage image = new();
        image.BeginInit();
        image.DecodePixelHeight = (int)decodeheight;
        image.CacheOption = BitmapCacheOption.None;
        image.StreamSource = memoryStream;
        image.EndInit();
        bitmap.Dispose();
        if (!image.IsFrozen && image.CanFreeze)
        {
            image.Freeze();
        }

        return image;
    }

    public static RenderTargetBitmap ToRenderTargetBitmap(this UIElement uiElement, double resolution = 96)
    {
        double scale = resolution / 96d;
        uiElement.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        uiElement.Arrange(new Rect(uiElement.DesiredSize));
        RenderTargetBitmap bmp = new((int)(scale * uiElement.RenderSize.Width), (int)(scale * uiElement.RenderSize.Height), scale * 96, scale * 96, PixelFormats.Pbgra32);
        bmp.Render(uiElement);
        bmp.Freeze();
        return bmp;
    }

    public static RenderTargetBitmap ToRenderTargetBitmap(this UIElement uiElement, double width, double height, double resolution = 96)
    {
        double scale = resolution / 96d;
        RenderTargetBitmap bmp = new((int)(scale * width), (int)(scale * height), scale * 96, scale * 96, PixelFormats.Pbgra32);
        bmp.Render(uiElement);
        bmp.Freeze();
        return bmp;
    }

    public static byte[] ToTiffJpegByteArray(this ImageSource bitmapsource, Format format, int jpegquality = 80)
    {
        try
        {
            using MemoryStream outStream = new();
            BitmapFrame frame = BitmapFrame.Create((BitmapSource)bitmapsource);
            frame.Freeze();
            switch (format)
            {
                case Format.TiffRenkli:
                    TiffBitmapEncoder tifzipencoder = new() { Compression = TiffCompressOption.Zip };
                    tifzipencoder.Frames.Add(frame);
                    tifzipencoder.Save(outStream);
                    return outStream.ToArray();

                case Format.Tiff:
                    TiffBitmapEncoder tifccittencoder = new() { Compression = TiffCompressOption.Ccitt4 };
                    tifccittencoder.Frames.Add(frame);
                    tifccittencoder.Save(outStream);
                    return outStream.ToArray();

                case Format.Jpg:
                    JpegBitmapEncoder jpgencoder = new() { QualityLevel = jpegquality };
                    BitmapFrame item = frame;
                    jpgencoder.Frames.Add(item);
                    jpgencoder.Save(outStream);
                    return outStream.ToArray();

                case Format.Png:
                    PngBitmapEncoder pngencoder = new();
                    pngencoder.Frames.Add(frame);
                    pngencoder.Save(outStream);
                    return outStream.ToArray();

                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }
        catch (Exception ex)
        {
            throw new ArgumentException(ex?.Message);
        }
    }
}