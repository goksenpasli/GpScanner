using System.Runtime.InteropServices;

namespace TwainWpf.TwainNative
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public class Frame
    {
        public Fix32 Left;
        public Fix32 Top;
        public Fix32 Right;
        public Fix32 Bottom;
    }
}