using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Tesseract.Internal;
using Tesseract.Interop;

namespace Tesseract
{
    /// <summary>
    /// Represents an array of <see cref="Pix"/>.
    /// </summary>
    public sealed class PixArray : DisposableBase, IEnumerable<Pix>
    {
        #region Constructor
        private PixArray(IntPtr handle)
        {
            _handle = new HandleRef(this, handle);
            version = 1;

            _count = LeptonicaApi.Native.pixaGetCount(_handle);
        }
        #endregion Constructor

        #region Properties

        /// <summary>
        /// Gets the number of <see cref="Pix"/> contained in the array.
        /// </summary>
        public int Count
        {
            get
            {
                VerifyNotDisposed();
                return _count;
            }
        }
        #endregion Properties

        #region Enumerator implementation

        /// <summary>
        /// Handles enumerating through the <see cref="Pix"/> in the PixArray.
        /// </summary>
        private class PixArrayEnumerator : DisposableBase, IEnumerator<Pix>
        {
            #region Constructor
            public PixArrayEnumerator(PixArray array)
            {
                this.array = array;
                version = array.version;
                items = new Pix[array.Count];
                index = 0;
                current = null;
            }
            #endregion Constructor

            #region Disposal
            protected override void Dispose(bool disposing)
            {
                if(disposing)
                {
                    for(int i = 0; i < items.Length; i++)
                    {
                        if(items[i] != null)
                        {
                            items[i].Dispose();
                            items[i] = null;
                        }
                    }
                }
            }
            #endregion Disposal

            #region Fields
            private readonly PixArray array;

            private readonly Pix[] items;

            private readonly int version;

            private Pix current;

            private int index;
            #endregion Fields

            #region Enumerator Implementation

            /// <inheritdoc/>
            public Pix Current
            {
                get
                {
                    VerifyArrayUnchanged();
                    VerifyNotDisposed();

                    return current;
                }
            }

            /// <inheritdoc/>
            object IEnumerator.Current => index == 0 || index == items.Length + 1 ? throw new InvalidOperationException("The enumerator is positioned either before the first item or after the last item .") : (object)Current;

            /// <inheritdoc/>
            public bool MoveNext()
            {
                VerifyArrayUnchanged();
                VerifyNotDisposed();

                if(index < items.Length)
                {
                    if(items[index] == null)
                    {
                        items[index] = array.GetPix(index);
                    }

                    current = items[index];
                    index++;
                    return true;
                }

                index = items.Length + 1;
                current = null;
                return false;
            }

            /// <inheritdoc/>
            void IEnumerator.Reset()
            {
                VerifyArrayUnchanged();
                VerifyNotDisposed();

                index = 0;
                current = null;
            }

            /// <inheritdoc/>
            private void VerifyArrayUnchanged()
            {
                if(version != array.version)
                {
                    throw new InvalidOperationException("PixArray was modified; enumeration operation may not execute.");
                }
            }
            #endregion Enumerator Implementation
        }
        #endregion Enumerator implementation

        #region Static Constructors
        public static PixArray Create(int n)
        {
            IntPtr pixaHandle = LeptonicaApi.Native.pixaCreate(n);
            return pixaHandle == IntPtr.Zero ? throw new IOException("Failed to create PixArray") : new PixArray(pixaHandle);
        }

        /// <summary>
        /// Loads the multi-page tiff located at <paramref name="filename"/>.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static PixArray LoadMultiPageTiffFromFile(string filename)
        {
            IntPtr pixaHandle = LeptonicaApi.Native.pixaReadMultipageTiff(filename);
            return pixaHandle == IntPtr.Zero ? throw new IOException($"Failed to load image '{filename}'.") : new PixArray(pixaHandle);
        }
        #endregion Static Constructors

        #region Fields
        private readonly int version;

        private int _count;

        /// <summary>
        /// Gets the handle to the underlying PixA structure.
        /// </summary>
        private HandleRef _handle;
        #endregion Fields

        #region Methods

        /// <summary>
        /// Add the specified pix to the end of the pix array.
        /// </summary>
        /// <remarks>
        /// PixArrayAccessType.Insert is not supported as the managed Pix object will attempt to release the pix when it
        /// goes out of scope creating an access exception.
        /// </remarks>
        /// <param name="pix">The pix to add.</param>
        /// <param name="copyflag">Determines if a clone or copy of the pix is inserted into the array.</param>
        /// <returns></returns>
        public bool Add(Pix pix, PixArrayAccessType copyflag = PixArrayAccessType.Clone)
        {
            Guard.RequireNotNull("pix", pix);
            Guard.Require(nameof(copyflag), copyflag == PixArrayAccessType.Clone || copyflag == PixArrayAccessType.Copy, "Copy flag must be either copy or clone but was {0}.", copyflag);

            int result = LeptonicaApi.Native.pixaAddPix(_handle, pix.Handle, copyflag);
            if(result == 0)
            {
                _count = LeptonicaApi.Native.pixaGetCount(_handle);
            }

            return result == 0;
        }

        /// <summary>
        /// Destroys ever pix in the array.
        /// </summary>
        public void Clear()
        {
            VerifyNotDisposed();
            if(LeptonicaApi.Native.pixaClear(_handle) == 0)
            {
                _count = LeptonicaApi.Native.pixaGetCount(_handle);
            }
        }

        /// <summary>
        /// Returns a <see cref="IEnumerator{Pix}"/> that iterates the the array of <see cref="Pix"/>.
        /// </summary>
        /// <remarks>
        /// When done with the enumerator you must call <see cref="Dispose"/> to release any unmanaged resources.
        /// However if your using the enumerator in a foreach loop, this is done for you automatically by .Net. This
        /// also means that any <see cref="Pix"/> returned from the enumerator cannot safely be used outside a foreach
        /// loop (or after Dispose has been called on the enumerator). If you do indeed need the pix after the
        /// enumerator has been disposed of you must clone it using <see cref="Pix.Clone()"/>.
        /// </remarks>
        /// <returns>A <see cref="IEnumerator{Pix}"/> that iterates the the array of <see cref="Pix"/>.</returns>
        public IEnumerator<Pix> GetEnumerator() { return new PixArrayEnumerator(this); }

        /// <summary>
        /// Gets the <see cref="Pix"/> located at <paramref name="index"/> using the specified <paramref
        /// name="accessType"/> .
        /// </summary>
        /// <param name="index">The index of the pix (zero based).</param>
        /// <param name="accessType">
        /// The <see cref="PixArrayAccessType"/> used to retrieve the <see cref="Pix"/>, only Clone or Copy are allowed.
        /// </param>
        /// <returns>The retrieved <see cref="Pix"/>.</returns>
        public Pix GetPix(int index, PixArrayAccessType accessType = PixArrayAccessType.Clone)
        {
            Guard.Require(nameof(accessType), accessType == PixArrayAccessType.Clone || accessType == PixArrayAccessType.Copy, "Access type must be either copy or clone but was {0}.", accessType);
            Guard.Require(nameof(index), index >= 0 && index < Count, "The index {0} must be between 0 and {1}.", index, Count);

            VerifyNotDisposed();

            IntPtr pixHandle = LeptonicaApi.Native.pixaGetPix(_handle, index, accessType);
            return pixHandle == IntPtr.Zero ? throw new InvalidOperationException($"Failed to retrieve pix {pixHandle}.") : Pix.Create(pixHandle);
        }

        /// <summary>
        /// Removes the pix located at index.
        /// </summary>
        /// <remarks>
        /// Notes: * This shifts pixa[i] --> pixa[i - 1] for all i > index. * Do not use on large arrays as the
        /// functionality is O(n). * The corresponding box is removed as well, if it exists.
        /// </remarks>
        /// <param name="index">The index of the pix to remove.</param>
        public void Remove(int index)
        {
            Guard.Require(nameof(index), index >= 0 && index < Count, "The index {0} must be between 0 and {1}.", index, Count);

            VerifyNotDisposed();
            if(LeptonicaApi.Native.pixaRemovePix(_handle, index) == 0)
            {
                _count = LeptonicaApi.Native.pixaGetCount(_handle);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() { return new PixArrayEnumerator(this); }

        protected override void Dispose(bool disposing)
        {
            IntPtr handle = _handle.Handle;
            LeptonicaApi.Native.pixaDestroy(ref handle);
            _handle = new HandleRef(this, handle);
        }
        #endregion Methods
    }
}