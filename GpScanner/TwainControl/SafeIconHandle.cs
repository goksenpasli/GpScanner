using System;
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

            protected override bool ReleaseHandle()
            {
                return Extensions.Helpers.DestroyIcon(handle);
            }

            private SafeIconHandle() : base(true)
            {
            }
        }
    }
}