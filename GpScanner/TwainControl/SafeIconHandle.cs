using Extensions;
using Microsoft.Win32.SafeHandles;
using System;

namespace TwainControl;

public partial class DrawControl
{
    public class SafeIconHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeIconHandle() : base(true)
        {
        }
        public SafeIconHandle(IntPtr hIcon) : base(true) { SetHandle(hIcon); }

        protected override bool ReleaseHandle() { return handle.DestroyIcon(); }
    }
}