using System.Runtime.InteropServices;

namespace TwainWpf.Win32
{
    public static class User32Native
    {
        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern int GetMessagePos();

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern int GetMessageTime();
    }
}