using System.Runtime.InteropServices;

namespace TwainWpf.TwainNative
{
    /// <summary>
    /// TW_VERSION
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 2, CharSet = CharSet.Ansi)]
    public struct TwainVersion
    {
        public short MajorNum;

        public short MinorNum;

        public Language Language;

        public Country Country;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 34)]
        public string Info;

        public TwainVersion Clone()
        {
            return (TwainVersion)MemberwiseClone();
        }
    }
}