using System;
using Extensions;
using Microsoft.Win32.SafeHandles;

namespace TwainControl;

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
            return Helpers.DestroyIcon(handle);
        }

        private SafeIconHandle() : base(true)
        {
        }
    }
}