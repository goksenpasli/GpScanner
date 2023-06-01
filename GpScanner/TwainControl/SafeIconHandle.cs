using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;

namespace TwainControl
{
    public partial class DrawControl
    {
        public class SafeIconHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            private SafeIconHandle() : base(true)
            {
            }

            public SafeIconHandle(IntPtr hIcon) : base(true) { SetHandle(hIcon); }

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool DestroyIcon([In] IntPtr hIcon);

            protected override bool ReleaseHandle() { return DestroyIcon(handle); }
        }
    }
}
