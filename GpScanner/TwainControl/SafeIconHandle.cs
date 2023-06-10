using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TwainControl
{
    public partial class DrawControl
    {
        public class SafeIconHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public SafeIconHandle(IntPtr hIcon) : base(true)
            {
                SetHandle(hIcon);
            }

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool DestroyIcon([In] IntPtr hIcon);

            protected override bool ReleaseHandle() => DestroyIcon(handle);

            private SafeIconHandle() : base(true)
            {
            }
        }
    }
}