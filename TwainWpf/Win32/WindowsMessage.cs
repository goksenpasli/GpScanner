using System;
using System.Runtime.InteropServices;

namespace TwainWpf.Win32 {
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct WindowsMessage {
        public IntPtr hwnd;

        public int message;

        public IntPtr wParam;

        public IntPtr lParam;

        public int time;

        public int x;

        public int y;
    }
}