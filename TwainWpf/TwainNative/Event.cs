using System;
using System.Runtime.InteropServices;

namespace TwainWpf.TwainNative {
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public struct Event {
        public IntPtr EventPtr;

        public Message Message;
    }
}