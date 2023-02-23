using System;
using System.Runtime.InteropServices;

namespace TwainWpf.TwainNative
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public class Fix32
    {
        public short Whole;

        public ushort Frac;

        public Fix32(float f)
        {
            int val = (int)(f * 65536.0F);
            Whole = Convert.ToInt16(val >> 16);    // most significant 16 bits
            Frac = Convert.ToUInt16(val & 0xFFFF); // least
        }

        public float ToFloat()
        {
            float frac = Convert.ToSingle(Frac);
            return Whole + (frac / 65536.0F);
        }
    }
}