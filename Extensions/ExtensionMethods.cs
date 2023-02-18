using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Extensions
{
    public static class ExtensionMethods
    {
        public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

        public const uint SHGFI_DISPLAYNAME = 0x000000200;

        public const uint SHGFI_ICON = 0x000000100;

        public const uint SHGFI_LARGEICON = 0x000000000;

        public const uint SHGFI_OPENICON = 0x000000002;

        public const uint SHGFI_SMALLICON = 0x000000001;

        public const uint SHGFI_TYPENAME = 0x000000400;

        public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

        public enum FolderType
        {
            Closed = 0,

            Open = 1
        }

        public enum Format
        {
            Tiff = 0,

            TiffRenkli = 1,

            Jpg = 2,

            Png = 3
        }

        public enum IconSize
        {
            Large = 0,

            Small = 1
        }

        public static Bitmap BitmapChangeFormat(this Bitmap bitmap, System.Drawing.Imaging.PixelFormat format)
        {
            Rectangle rect = new(0, 0, bitmap.Width, bitmap.Height);
            return bitmap.Clone(rect, format);
        }

        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source?.IndexOf(toCheck, comp) >= 0;
        }

        public static Bitmap ConvertBlackAndWhite(this Bitmap bitmap, int bWthreshold = 160, bool grayscale = false)
        {
            unsafe
            {
                BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                int bytesPerPixel = Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;
                int heightInPixels = bitmapData.Height;
                int widthInBytes = bitmapData.Width * bytesPerPixel;
                byte* ptrFirstPixel = (byte*)bitmapData.Scan0;
                _ = Parallel.For(0, heightInPixels, y =>
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
            }

            return bitmap;
        }

        public static System.Windows.Media.Brush ConvertToBrush(this System.Drawing.Color color)
        {
            System.Windows.Media.Color convertedcolor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
            return new SolidColorBrush(convertedcolor);
        }

        public static System.Drawing.Color ConvertToColor(this System.Windows.Media.Brush color)
        {
            SolidColorBrush sb = (SolidColorBrush)color;
            return System.Drawing.Color.FromArgb(sb.Color.A, sb.Color.R, sb.Color.G, sb.Color.B);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool DestroyIcon(this IntPtr handle);

        public static ConcurrentBag<string> DirSearch(this string path, string pattern = "*.*")
        {
            ConcurrentQueue<string> pendingQueue = new();
            pendingQueue.Enqueue(path);

            ConcurrentBag<string> filesNames = new();
            while (pendingQueue.Count > 0)
            {
                try
                {
                    _ = pendingQueue.TryDequeue(out path);

                    string[] files = Directory.GetFiles(path, pattern);

                    _ = Parallel.ForEach(files, filesNames.Add);

                    string[] directories = Directory.GetDirectories(path);

                    _ = Parallel.ForEach(directories, pendingQueue.Enqueue);
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            return filesNames;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr ExtractIcon(this IntPtr hInst, string lpszExeFileName, int nIconIndex);

        public static IEnumerable<string> FilterFiles(this string path, params string[] exts)
        {
            return exts.Select(x => x).SelectMany(x => Directory.EnumerateFiles(path, x, SearchOption.TopDirectoryOnly));
        }

        public static string GetDisplayName(string path)
        {
            _ = new SHFILEINFO();
            return (SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, out SHFILEINFO shfi, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), SHGFI_DISPLAYNAME) != IntPtr.Zero) ? shfi.szDisplayName : null;
        }

        public static string GetFileType(this string filename)
        {
            SHFILEINFO shinfo = new();
            _ = SHGetFileInfo
                    (
                        filename,
                        FILE_ATTRIBUTE_NORMAL,
                        out shinfo, (uint)Marshal.SizeOf(shinfo),
                        SHGFI_TYPENAME |
                        SHGFI_USEFILEATTRIBUTES
                    );

            return shinfo.szTypeName;
        }

        public static BitmapSource IconCreate(this string path, IconSize size)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES;

                if (IconSize.Small == size)
                {
                    flags += SHGFI_SMALLICON;
                }
                else
                {
                    flags += SHGFI_LARGEICON;
                }

                SHFILEINFO shfi = new();

                IntPtr res = SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, out shfi, (uint)Marshal.SizeOf(shfi), flags);

                if (res == IntPtr.Zero)
                {
                    throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
                }

                _ = Icon.FromHandle(shfi.hIcon);
                using Icon icon = (Icon)Icon.FromHandle(shfi.hIcon).Clone();
                _ = DestroyIcon(shfi.hIcon);
                BitmapSource bitmapsource = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bitmapsource.Freeze();
                return bitmapsource;
            }
            return null;
        }

        public static BitmapSource IconCreate(this string filepath, int iconindex)
        {
            if (filepath != null)
            {
                IntPtr hIcon = hwnd.ExtractIcon(filepath, iconindex);
                if (hIcon != IntPtr.Zero)
                {
                    BitmapSource icon = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    _ = hIcon.DestroyIcon();
                    icon.Freeze();
                    return icon;
                }

                _ = hIcon.DestroyIcon();
            }

            return null;
        }

        public static bool IsEmptyPage(this Bitmap bitmap, double emptythreshold = 10)
        {
            double total = 0, totalVariance = 0;
            int count = 0;
            double stdDev = 0;
            BitmapData bmData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
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
            return stdDev < emptythreshold;
        }

        public static void OpenFolderAndSelectItem(string folderPath, string file)
        {
            SHParseDisplayName(folderPath, IntPtr.Zero, out IntPtr nativeFolder, 0, out _);

            if (nativeFolder == IntPtr.Zero)
            {
                // Log error, can't find folder
                return;
            }

            SHParseDisplayName(Path.Combine(folderPath, file), IntPtr.Zero, out IntPtr nativeFile, 0, out _);

            IntPtr[] fileArray;
            if (nativeFile == IntPtr.Zero)
            {
                // Open the folder without the file selected if we can't find the file
                fileArray = new IntPtr[0];
            }
            else
            {
                fileArray = new IntPtr[] { nativeFile };
            }

            _ = SHOpenFolderAndSelectItems(nativeFolder, (uint)fileArray.Length, fileArray, 0);

            Marshal.FreeCoTaskMem(nativeFolder);
            if (nativeFile != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(nativeFile);
            }
        }

        public static System.Windows.Media.Brush RandomColor()
        {
            Random rand = new(Guid.NewGuid().GetHashCode());
            SolidColorBrush brush = new(System.Windows.Media.Color.FromRgb((byte)rand.Next(0, 256), (byte)rand.Next(0, 256), (byte)rand.Next(0, 256)));
            brush.Freeze();
            return brush;
        }

        public static BitmapSource Resize(this BitmapSource bfPhoto, double nWidth, double nHeight, double rotate = 0, int dpiX = 96, int dpiY = 96)
        {
            RotateTransform rotateTransform = new(rotate);
            ScaleTransform scaleTransform = new(nWidth / 96 * dpiX / bfPhoto.PixelWidth, nHeight / 96 * dpiY / bfPhoto.PixelHeight, 0, 0);
            TransformGroup transformGroup = new();
            transformGroup.Children.Add(rotateTransform);
            transformGroup.Children.Add(scaleTransform);
            TransformedBitmap tb = new(bfPhoto, transformGroup);
            tb.Freeze();
            return tb;
        }

        public static BitmapSource Resize(this BitmapSource bfPhoto, double oran)
        {
            ScaleTransform newTransform = new(oran, oran, 0, 0);
            TransformedBitmap tb = new(bfPhoto, newTransform);
            tb.Freeze();
            return tb;
        }

        public static async Task<BitmapSource> ResizeAsync(this BitmapSource bfPhoto, double oran, double centerx = 0, double centery = 0)
        {
            return await Task.Run(() =>
            {
                ScaleTransform newTransform = new(oran, oran, centerx, centery);
                TransformedBitmap tb = new(bfPhoto, newTransform);
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
            for (i = 0; File.Exists($@"{path}\{file}{seperator}{i}.{extension}"); i++)
            {
                _ = i + 1;
            }

            return $@"{path}\{file}{seperator}{i}.{extension}";
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("shell32.dll", SetLastError = true)]
        public static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, uint cidl, [In, MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, uint dwFlags);

        [DllImport("shell32.dll", SetLastError = true)]
        public static extern void SHParseDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr bindingContext, [Out] out IntPtr pidl, uint sfgaoIn, [Out] out uint psfgaoOut);

        public static BitmapImage ToBitmapImage(this Image bitmap, ImageFormat format, double decodeheight = 0)
        {
            if (bitmap != null)
            {
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

            return null;
        }

        public static RenderTargetBitmap ToRenderTargetBitmap(this UIElement uiElement, double resolution = 96)
        {
            double scale = resolution / 96d;
            System.Windows.Size availableSize = new(double.PositiveInfinity, double.PositiveInfinity);
            uiElement.Measure(availableSize);
            System.Windows.Size sz = uiElement.DesiredSize;
            Rect rect = new(sz);
            uiElement.Arrange(rect);
            RenderTargetBitmap bmp = new((int)(scale * rect.Width), (int)(scale * rect.Height), scale * 96, scale * 96, PixelFormats.Default);
            if (bmp != null)
            {
                bmp.Render(uiElement);
                bmp.Freeze();
                return bmp;
            }

            return null;
        }

        public static byte[] ToTiffJpegByteArray(this ImageSource bitmapsource, Format format, int jpegquality = 80)
        {
            try
            {
                using MemoryStream outStream = new();
                BitmapFrame frame = BitmapFrame.Create((BitmapSource)bitmapsource);
                switch (format)
                {
                    case Format.TiffRenkli:
                        TiffBitmapEncoder tifzipencoder = new() { Compression = TiffCompressOption.Zip };
                        tifzipencoder.Frames.Add(frame);
                        tifzipencoder.Save(outStream);
                        tifzipencoder = null;
                        bitmapsource = null;
                        return outStream.ToArray();

                    case Format.Tiff:
                        TiffBitmapEncoder tifccittencoder = new() { Compression = TiffCompressOption.Ccitt4 };
                        tifccittencoder.Frames.Add(frame);
                        tifccittencoder.Save(outStream);
                        tifccittencoder = null;
                        bitmapsource = null;
                        return outStream.ToArray();

                    case Format.Jpg:
                        JpegBitmapEncoder jpgencoder = new() { QualityLevel = jpegquality };
                        BitmapFrame item = frame;
                        jpgencoder.Frames.Add(item);
                        jpgencoder.Save(outStream);
                        jpgencoder = null;
                        bitmapsource = null;
                        return outStream.ToArray();

                    case Format.Png:
                        PngBitmapEncoder pngencoder = new();
                        pngencoder.Frames.Add(frame);
                        pngencoder.Save(outStream);
                        pngencoder = null;
                        bitmapsource = null;
                        return outStream.ToArray();

                    default:
                        throw new ArgumentOutOfRangeException(nameof(format), format, null);
                }
            }
            catch (Exception ex)
            {
                bitmapsource = null;
                MessageBox.Show(ex.Message);
                return null;
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;

            public int iIcon;

            public uint dwAttributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private static readonly IntPtr hwnd = Process.GetCurrentProcess().Handle;
    }
}