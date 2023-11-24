using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Extensions
{
    public static class ShellIcon
    {
        public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        public const int ILD_IMAGE = 0x00000020;
        public const int ILD_TRANSPARENT = 0x00000001;
        public const uint SHGFI_DISPLAYNAME = 0x000000200;
        public const uint SHGFI_TYPENAME = 0x000000400;
        public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        public const int SHIL_EXTRALARGE = 0x2;
        public const int SHIL_JUMBO = 0x4;
        public const int SHIL_LARGE = 0x0;
        public const int SHIL_LAST = 0x4;
        public const int SHIL_SMALL = 0x1;
        public const int SHIL_SYSSMALL = 0x3;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const string IID_IImageList2 = "192B9D83-50FC-457B-90A0-2B82A8B5DAE1";
        private static readonly IntPtr hwnd = Process.GetCurrentProcess().Handle;

        public enum SizeType
        {
            large,
            small,
            extraLarge,
            sysSmall,
            jumbo,
            last
        }

        [Flags]
        private enum SHGFI
        {
            LargeIcon = 0x000000000,
            SmallIcon = 0x000000001,
            OpenIcon = 0x000000002,
            ShellIconSize = 0x000000004,
            PIDL = 0x000000008,
            UseFileAttributes = 0x000000010,
            AddOverlays = 0x000000020,
            OverlayIndex = 0x000000040,
            Icon = 0x000000100,
            DisplayName = 0x000000200,
            TypeName = 0x000000400,
            Attributes = 0x000000800,
            IconLocation = 0x000001000,
            ExeType = 0x000002000,
            SysIconIndex = 0x000004000,
            LinkOverlay = 0x000008000,
            Selected = 0x000010000,
            Attr_Specified = 0x000020000,
        }

        [ComImport()]
        [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IImageList
        {
            [PreserveSig]
            int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);

            [PreserveSig]
            int ReplaceIcon(int i, IntPtr hicon, ref int pi);

            [PreserveSig]
            int SetOverlayImage(int iImage, int iOverlay);

            [PreserveSig]
            int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);

            [PreserveSig]
            int AddMasked(IntPtr hbmImage, int crMask, ref int pi);

            [PreserveSig]
            int Draw(ref IMAGELISTDRAWPARAMS pimldp);

            [PreserveSig]
            int Remove(int i);

            [PreserveSig]
            int GetIcon(int i, int flags, ref IntPtr picon);

            [PreserveSig]
            int GetImageInfo(int i, ref IMAGEINFO pImageInfo);

            [PreserveSig]
            int Copy(int iDst, IImageList punkSrc, int iSrc, int uFlags);

            [PreserveSig]
            int Merge(int i1, IImageList punk2, int i2, int dx, int dy, ref Guid riid, ref IntPtr ppv);

            [PreserveSig]
            int Clone(ref Guid riid, ref IntPtr ppv);

            [PreserveSig]
            int GetImageRect(int i, ref RECT prc);

            [PreserveSig]
            int GetIconSize(ref int cx, ref int cy);

            [PreserveSig]
            int SetIconSize(int cx, int cy);

            [PreserveSig]
            int GetImageCount(ref int pi);

            [PreserveSig]
            int SetImageCount(int uNewCount);

            [PreserveSig]
            int SetBkColor(int clrBk, ref int pclr);

            [PreserveSig]
            int GetBkColor(ref int pclr);

            [PreserveSig]
            int BeginDrag(int iTrack, int dxHotspot, int dyHotspot);

            [PreserveSig]
            int EndDrag();

            [PreserveSig]
            int DragEnter(IntPtr hwndLock, int x, int y);

            [PreserveSig]
            int DragLeave(IntPtr hwndLock);

            [PreserveSig]
            int DragMove(int x, int y);

            [PreserveSig]
            int SetDragCursorImage(ref IImageList punk, int iDrag, int dxHotspot, int dyHotspot);

            [PreserveSig]
            int DragShowNolock(int fShow);

            [PreserveSig]
            int GetDragImage(ref POINT ppt, ref POINT pptHotspot, ref Guid riid, ref IntPtr ppv);

            [PreserveSig]
            int GetItemFlags(int i, ref int dwFlags);

            [PreserveSig]
            int GetOverlayImage(int iOverlay, ref int piIndex);
        }

        public static string GetDisplayName(string path)
        {
            SHFILEINFO shfi = new();
            return Win32.SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, ref shfi, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), SHGFI_DISPLAYNAME) != IntPtr.Zero ? shfi.szDisplayName : null;
        }

        public static BitmapSource GetExtensionIconBySize(string ext, SizeType sizeType) => GetIconBySize(ext, sizeType, false, false);

        public static BitmapSource GetFileIconBySize(string path, SizeType sizeType) => GetIconBySize(path, sizeType, false);

        public static string GetFileType(string filename, SHFILEINFO shinfo)
        {
            _ = Win32.SHGetFileInfo(filename, FILE_ATTRIBUTE_NORMAL, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_TYPENAME | SHGFI_USEFILEATTRIBUTES);
            return shinfo.szTypeName;
        }

        public static BitmapSource GetFolderIconBySize(string path, SizeType sizeType) => GetIconBySize(path, sizeType, true);

        public static BitmapSource GetIconBySize(string path, SizeType sizeType, bool isFolder, bool useFileAttributes = true)
        {
            int shil = SHIL_LARGE;
            switch (sizeType)
            {
                case SizeType.extraLarge:
                    shil = SHIL_EXTRALARGE;
                    break;
                case SizeType.jumbo:
                    shil = SHIL_JUMBO;
                    break;
                case SizeType.large:
                case SizeType.last:
                    shil = SHIL_LARGE;
                    break;
                case SizeType.small:
                    shil = SHIL_SMALL;
                    break;
                case SizeType.sysSmall:
                    shil = SHIL_SYSSMALL;
                    break;
            }
            IntPtr iconIndex = GetIconIndex(path, isFolder, useFileAttributes);

            IImageList spiml = null;
            Guid guil = new(IID_IImageList2);

            _ = Win32.SHGetImageList(shil, ref guil, ref spiml);
            IntPtr hIcon = IntPtr.Zero;
            _ = spiml.GetIcon((int)iconIndex, ILD_TRANSPARENT | ILD_IMAGE, ref hIcon);
            Icon icon = (Icon)Icon.FromHandle(hIcon).Clone();
            BitmapSource bitmapsource = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bitmapsource.Freeze();
            _ = Win32.DestroyIcon(hIcon);
            return bitmapsource;
        }

        public static IntPtr GetIconIndex(string pszFile, bool isFolder = false, bool useFileAttributes = false)
        {
            SHFILEINFO sfi = new();
            uint uFlags = useFileAttributes ? (uint)(SHGFI.SysIconIndex | SHGFI.LargeIcon) : (uint)(SHGFI.SysIconIndex | SHGFI.LargeIcon | SHGFI.UseFileAttributes);
            _ = Win32.SHGetFileInfo(pszFile, isFolder ? FILE_ATTRIBUTE_DIRECTORY : 0, ref sfi, (uint)Marshal.SizeOf(sfi), uFlags);
            return sfi.iIcon;
        }

        public static BitmapSource IconCreate(string filepath, int iconindex)
        {
            if (string.IsNullOrWhiteSpace(filepath))
            {
                return null;
            }
            IntPtr hIcon = Win32.ExtractIcon(hwnd, filepath, iconindex);
            if (hIcon == IntPtr.Zero)
            {
                _ = Win32.DestroyIcon(hIcon);
                return null;
            }

            _ = Icon.FromHandle(hIcon);
            BitmapSource bitmapsource = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bitmapsource.Freeze();
            _ = Win32.DestroyIcon(hIcon);
            return bitmapsource;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGEINFO
        {
            public IntPtr hbmImage;
            public IntPtr hbmMask;
            public int Unused1;
            public int Unused2;
            public RECT rcImage;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGELISTDRAWPARAMS
        {
            public int cbSize;
            public IntPtr himl;
            public int i;
            public IntPtr hdcDst;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public int xBitmap;
            public int yBitmap;
            public int rgbBk;
            public int rgbFg;
            public int fStyle;
            public int dwRop;
            public int fState;
            public int Frame;
            public int crEffect;
        }

        [StructLayout(LayoutKind.Sequential)]
        public readonly struct POINT
        {
            private readonly int x;
            private readonly int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left, top, right, bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public IntPtr iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        public static class Win32
        {
            [DllImport("User32.dll")]
            public static extern bool DestroyIcon(IntPtr hIcon);

            [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

            [DllImport("shell32.dll")]
            public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

            [DllImport("shell32.dll")]
            public static extern uint SHGetIDListFromObject([MarshalAs(UnmanagedType.IUnknown)] object iUnknown, out IntPtr ppidl);

            [DllImport("shell32.dll", EntryPoint = "#727")]
            public static extern int SHGetImageList(int iImageList, ref Guid riid, ref IImageList ppv);
        }
    }
}