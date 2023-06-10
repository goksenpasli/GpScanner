using System.Runtime.InteropServices;

namespace TwainWpf.Win32
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public class BitmapInfoHeader
    {
        public int Size;

        public int Width;

        public int Height;

        public short Planes;

        public short BitCount;

        public int Compression;

        public int SizeImage;

        public int XPelsPerMeter;

        public int YPelsPerMeter;

        public int ClrUsed;

        public int ClrImportant;

        public override string ToString()
        {
            return $"s:{Size} w:{Width} h:{Height} p:{Planes} bc:{BitCount} c:{Compression} si:{SizeImage} xpels:{XPelsPerMeter} ypels:{YPelsPerMeter} cu:{ClrUsed} ci:{ClrImportant}";
        }
    }
}