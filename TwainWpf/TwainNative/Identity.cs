using System.Runtime.InteropServices;

namespace TwainWpf.TwainNative {
    [StructLayout(LayoutKind.Sequential, Pack = 2, CharSet = CharSet.Ansi)]
    public class Identity {
        public int Id;

        public TwainVersion Version;

        public short ProtocolMajor;

        public short ProtocolMinor;

        public int SupportedGroups;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 34)]
        public string Manufacturer;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 34)]
        public string ProductFamily;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 34)]
        public string ProductName;

        public Identity Clone() {
            Identity id = (Identity)MemberwiseClone();
            id.Version = Version.Clone();
            return id;
        }
    }
}