using System;
using System.Runtime.InteropServices;

namespace TwainWpf.TwainNative
{
    /// <summary>
    /// DAT_USERINTERFACE. Coordinates UI between application and data source.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public class UserInterface
    {
        /// <summary>
        /// TRUE if DS should bring up its UI
        /// </summary>
        public short ShowUI;

        /// <summary>
        /// For Mac only - true if the DS's UI is modal
        /// </summary>
        public short ModalUI;

        /// <summary>
        /// For windows only - Application window handle
        /// </summary>
        public IntPtr ParentHand;
    }
}