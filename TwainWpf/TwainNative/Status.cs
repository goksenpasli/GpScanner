using System.Runtime.InteropServices;

namespace TwainWpf.TwainNative
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public class Status
    {
        public ConditionCode ConditionCode;
        public short Reserved;
    }
}