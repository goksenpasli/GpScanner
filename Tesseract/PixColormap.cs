using System;
using System.Runtime.InteropServices;

namespace Tesseract
{
    /// <summary>
    /// Represents a colormap.
    /// </summary>
    /// <remarks>
    /// Once the colormap is assigned to a pix it is owned by that pix and will be disposed off automatically
    /// when the pix is disposed off.
    /// </remarks>
    public sealed class PixColormap : IDisposable
    {
        public int Count => Interop.LeptonicaApi.Native.pixcmapGetCount(handle);

        public int Depth => Interop.LeptonicaApi.Native.pixcmapGetDepth(handle);

        public int FreeCount => Interop.LeptonicaApi.Native.pixcmapGetFreeCount(handle);

        public PixColor this[int index]
        {
            get => Interop.LeptonicaApi.Native.pixcmapGetColor32(handle, index, out int color) == 0
                    ? PixColor.FromRgb((uint)color)
                    : throw new InvalidOperationException("Failed to retrieve color.");

            set
            {
                if (Interop.LeptonicaApi.Native.pixcmapResetColor(handle, index, value.Red, value.Green, value.Blue) != 0)
                {
                    throw new InvalidOperationException("Failed to reset color.");
                }
            }
        }

        public static PixColormap Create(int depth)
        {
            if (!(depth == 1 || depth == 2 || depth == 4 || depth == 8))
            {
                throw new ArgumentOutOfRangeException(nameof(depth), "Depth must be 1, 2, 4, or 8 bpp.");
            }

            IntPtr handle = Interop.LeptonicaApi.Native.pixcmapCreate(depth);
            return handle == IntPtr.Zero ? throw new InvalidOperationException("Failed to create colormap.") : new PixColormap(handle);
        }

        public static PixColormap CreateLinear(int depth, int levels)
        {
            if (!(depth == 1 || depth == 2 || depth == 4 || depth == 8))
            {
                throw new ArgumentOutOfRangeException(nameof(depth), "Depth must be 1, 2, 4, or 8 bpp.");
            }
            if (levels < 2 || levels > (2 << depth))
            {
                throw new ArgumentOutOfRangeException(nameof(levels), "Depth must be 2 and 2^depth (inclusive).");
            }

            IntPtr handle = Interop.LeptonicaApi.Native.pixcmapCreateLinear(depth, levels);
            return handle == IntPtr.Zero ? throw new InvalidOperationException("Failed to create colormap.") : new PixColormap(handle);
        }

        public static PixColormap CreateLinear(int depth, bool firstIsBlack, bool lastIsWhite)
        {
            if (!(depth == 1 || depth == 2 || depth == 4 || depth == 8))
            {
                throw new ArgumentOutOfRangeException(nameof(depth), "Depth must be 1, 2, 4, or 8 bpp.");
            }

            IntPtr handle = Interop.LeptonicaApi.Native.pixcmapCreateRandom(depth, firstIsBlack ? 1 : 0, lastIsWhite ? 1 : 0);
            return handle == IntPtr.Zero ? throw new InvalidOperationException("Failed to create colormap.") : new PixColormap(handle);
        }

        public bool AddBlackOrWhite(int color, out int index)
        {
            return Interop.LeptonicaApi.Native.pixcmapAddBlackOrWhite(handle, color, out index) == 0;
        }

        public bool AddColor(PixColor color)
        {
            return Interop.LeptonicaApi.Native.pixcmapAddColor(handle, color.Red, color.Green, color.Blue) == 0;
        }

        public bool AddNearestColor(PixColor color, out int index)
        {
            return Interop.LeptonicaApi.Native.pixcmapAddNearestColor(handle, color.Red, color.Green, color.Blue, out index) == 0;
        }

        public bool AddNewColor(PixColor color, out int index)
        {
            return Interop.LeptonicaApi.Native.pixcmapAddNewColor(handle, color.Red, color.Green, color.Blue, out index) == 0;
        }

        public void Clear()
        {
            if (Interop.LeptonicaApi.Native.pixcmapClear(handle) != 0)
            {
                throw new InvalidOperationException("Failed to clear color map.");
            }
        }

        public void Dispose()
        {
            IntPtr tmpHandle = Handle.Handle;
            Interop.LeptonicaApi.Native.pixcmapDestroy(ref tmpHandle);
            handle = new HandleRef(this, IntPtr.Zero);
        }

        public bool IsUsableColor(PixColor color)
        {
            return Interop.LeptonicaApi.Native.pixcmapUsableColor(handle, color.Red, color.Green, color.Blue, out int usable) == 0
                ? usable == 1
                : throw new InvalidOperationException("Failed to detect if color was usable or not.");
        }

        public bool SetBlackOrWhite(bool setBlack, bool setWhite)
        {
            return Interop.LeptonicaApi.Native.pixcmapSetBlackAndWhite(handle, setBlack ? 1 : 0, setWhite ? 1 : 0) == 0;
        }

        internal PixColormap(IntPtr handle)
        {
            this.handle = new HandleRef(this, handle);
        }

        internal HandleRef Handle => handle;

        private HandleRef handle;
    }
}