using Extensions;
using Microsoft.Win32.SafeHandles;
using System;

namespace TwainControl;

public partial class DrawControl
{
    public class SafeIconHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeIconHandle(IntPtr hIcon) : base(true) { SetHandle(hIcon); }
        private SafeIconHandle() : base(true)
        {
        }

        protected override bool ReleaseHandle() { return handle.DestroyIcon(); }
    }
}