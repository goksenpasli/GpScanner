﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32.SafeHandles;

namespace TwainControl
{
    public static class CursorHelper
    {
        public static Cursor CreateCursor(UIElement element)
        {
            RenderTargetBitmap rtb = new((int)element.DesiredSize.Width, (int)element.DesiredSize.Height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(element);
            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using MemoryStream ms = new();
            encoder.Save(ms);
            using System.Drawing.Bitmap bmp = new(ms);
            return InternalCreateCursor(bmp);
        }

        private static Cursor InternalCreateCursor(System.Drawing.Bitmap bmp)
        {
            NativeMethods.IconInfo iconInfo = new();
            NativeMethods.GetIconInfo(bmp.GetHicon(), ref iconInfo);
            iconInfo.xHotspot = 0;
            iconInfo.yHotspot = 0;
            iconInfo.fIcon = false;
            SafeIconHandle cursorHandle = NativeMethods.CreateIconIndirect(ref iconInfo);
            return CursorInteropHelper.Create(cursorHandle);
        }

        private static class NativeMethods
        {
            public struct IconInfo
            {
                public bool fIcon;
                public int xHotspot;
                public int yHotspot;
                public IntPtr hbmMask;
                public IntPtr hbmColor;
            }

            [DllImport("user32.dll")]
            public static extern SafeIconHandle CreateIconIndirect(ref IconInfo icon);

            [DllImport("user32.dll")]
            public static extern bool DestroyIcon(IntPtr hIcon);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetIconInfo(IntPtr hIcon, ref IconInfo pIconInfo);
        }

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        private class SafeIconHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public SafeIconHandle() : base(true)
            {
            }

            protected override bool ReleaseHandle()
            {
                return NativeMethods.DestroyIcon(handle);
            }
        }
    }
}