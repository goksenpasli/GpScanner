using System;
using System.Runtime.InteropServices;
using TwainWpf.Win32;

namespace TwainWpf.TwainNative
{
    /// <summary>
    /// /* DAT_CAPABILITY. Used by application to get/set capability from/in a data source. */ typedef struct {
    /// TW_UINT16  Cap; /* id of capability to set or get, e.g. CAP_BRIGHTNESS */ TW_UINT16  ConType; /* TWON_ONEVALUE,
    /// _RANGE, _ENUMERATION or _ARRAY   */ TW_HANDLE  hContainer; /* Handle to container of type Dat              */ }
    /// TW_CAPABILITY, FAR * pTW_CAPABILITY;
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public class TwainCapability : IDisposable
    {
        private readonly Capabilities _capabilities;
        private readonly ContainerType _containerType;
        private readonly IntPtr _handle;
        private readonly object _value;

        protected TwainCapability(Capabilities capabilities, ContainerType containerType, object value)
        {
            _capabilities = capabilities;
            _containerType = containerType;
            _value = value;

            _handle = Kernel32Native.GlobalAlloc(GlobalAllocFlags.Handle, Marshal.SizeOf(value));

            IntPtr p = Kernel32Native.GlobalLock(_handle);

            try
            {
                Marshal.StructureToPtr(value, p, false);
            } finally
            {
                _ = Kernel32Native.GlobalUnlock(_handle);
            }
        }

        ~TwainCapability() { Dispose(false); }

        public static TwainCapability From<TValue>(Capabilities capabilities, TValue value)
        {
            ContainerType containerType;
            Type structType = typeof(TValue);

            containerType = structType == typeof(CapabilityOneValue) ? ContainerType.One : throw new NotSupportedException($"Unsupported type: {structType}");

            return new TwainCapability(capabilities, containerType, value);
        }

        public void Dispose() { Dispose(true); }

        public void ReadBackValue()
        {
            IntPtr p = Kernel32Native.GlobalLock(_handle);

            try
            {
                Marshal.PtrToStructure(p, _value);
            } finally
            {
                _ = Kernel32Native.GlobalUnlock(_handle);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if(_handle != IntPtr.Zero)
            {
                _ = Kernel32Native.GlobalFree(_handle);
            }
        }
    }
}