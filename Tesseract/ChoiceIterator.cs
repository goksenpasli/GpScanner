using System;
using System.Runtime.InteropServices;
using Tesseract.Interop;

namespace Tesseract
{
    /// <summary>
    /// Class to iterate over the classifier choices for a single symbol.
    /// </summary>
    public sealed class ChoiceIterator : DisposableBase
    {
        private readonly HandleRef _handleRef;

        internal ChoiceIterator(IntPtr handle) { _handleRef = new HandleRef(this, handle); }

        protected override void Dispose(bool disposing)
        {
            if(_handleRef.Handle != IntPtr.Zero)
            {
                TessApi.Native.ChoiceIteratorDelete(_handleRef);
            }
        }

        /// <summary>
        /// Returns the confidence of the current choice.
        /// </summary>
        /// <remarks>
        /// The number should be interpreted as a percent probability. (0.0f-100.0f)
        /// </remarks>
        /// <returns>float</returns>
        public float GetConfidence()
        {
            VerifyNotDisposed();
            return _handleRef.Handle == IntPtr.Zero ? 0f : TessApi.Native.ChoiceIteratorGetConfidence(_handleRef);
        }

        /// <summary>
        /// Returns the text string for the current choice.
        /// </summary>
        /// <returns>string</returns>
        public string GetText()
        {
            VerifyNotDisposed();
            return _handleRef.Handle == IntPtr.Zero ? string.Empty : TessApi.ChoiceIteratorGetUTF8Text(_handleRef);
        }

        /// <summary>
        /// Moves to the next choice for the symbol and returns false if there are none left.
        /// </summary>
        /// <returns>true|false</returns>
        public bool Next()
        {
            VerifyNotDisposed();
            return _handleRef.Handle != IntPtr.Zero && TessApi.Native.ChoiceIteratorNext(_handleRef) != 0;
        }
    }
}