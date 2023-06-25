using System;

namespace TwainWpf.TwainNative
{
    [Flags]
    public enum DataGroup
    {
        Control = 0x0001,

        Image = 0x0002,

        Audio = 0x0004,

        DsmMask = 0xFFFF,
    }
}