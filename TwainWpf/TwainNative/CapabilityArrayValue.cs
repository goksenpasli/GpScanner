using System.Runtime.InteropServices;

namespace TwainWpf.TwainNative
{
    /// <summary>
    /// /* TWON_ARRAY. Container for array of values (a simplified TW_ENUMERATION) */ typedef struct { TW_UINT16 
    /// ItemType; TW_UINT32  NumItems;    /* How many items in ItemList           */ TW_UINT8   ItemList[1]; /* Array of
    /// ItemType values starts here */ } TW_ARRAY, FAR * pTW_ARRAY;
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public class CapabilityArrayValue
    {
        public int ItemCount { get; set; }

        public TwainType TwainType { get; set; }
    }
}