using System.Runtime.InteropServices;

namespace TwainWpf.TwainNative
{
    /// <summary>
    /// /* TWON_ONEVALUE. Container for one value. */ typedef struct { TW_UINT16  ItemType; TW_UINT32  Item; }
    /// TW_ONEVALUE, FAR * pTW_ONEVALUE;
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public class CapabilityOneValue
    {
        public CapabilityOneValue(TwainType twainType, int value)
        {
            Value = value;
            TwainType = twainType;
        }

        public TwainType TwainType { get; set; }

        public int Value { get; set; }
    }
}