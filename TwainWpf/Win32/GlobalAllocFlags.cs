using System;

namespace TwainWpf.Win32
{
    [Flags]
    public enum GlobalAllocFlags : uint
    {
        MemFixed = 0,

        MemMoveable = 0x2,

        ZeroInit = 0x40,

        Handle = MemMoveable | ZeroInit
    }
}