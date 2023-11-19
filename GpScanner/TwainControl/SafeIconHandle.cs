using Microsoft.Win32.SafeHandles;
using System;
using static Extensions.ShellIcon;

namespace TwainControl;

public partial class DrawControl
{
    public class SafeIconHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeIconHandle(IntPtr hIcon) : base(true) { SetHandle(hIcon); }
        private SafeIconHandle() : base(true)
        {
        }

        protected override bool ReleaseHandle() => Win32.DestroyIcon(handle);
    }
}