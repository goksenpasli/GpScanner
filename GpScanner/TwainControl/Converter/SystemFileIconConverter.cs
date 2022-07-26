using Extensions;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace TwainControl.Converter
{
    public sealed class SystemFileIconConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is string systemfilename && File.Exists($@"{Environment.SystemDirectory}\{systemfilename}") && values[1] is string index)
            {
                try
                {
                    return IconCreate($@"{Environment.SystemDirectory}\{systemfilename}", System.Convert.ToInt32(index));
                }
                catch (Exception)
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public BitmapSource IconCreate(string filepath, int iconindex)
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

        private static readonly IntPtr hwnd = Process.GetCurrentProcess().Handle;
    }
}