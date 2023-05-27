﻿using System;
using System.Runtime.InteropServices;
using Tesseract.Interop;

namespace Tesseract
{
    /// <summary>
    /// Represents an object that can iterate over tesseract's page structure.
    /// </summary>
    /// <remarks>
    /// The iterator points to tesseract's internal page structure and is only valid while the Engine instance that
    /// created it exists and has not been subjected to a call to Recognize since the iterator was created.
    /// </remarks>
    public class PageIterator : DisposableBase
    {
        public PolyBlockType BlockType {
            get {
                VerifyNotDisposed();

                return handle.Handle == IntPtr.Zero ? PolyBlockType.Unknown : TessApi.Native.PageIteratorBlockType(handle);
            }
        }

        /// <summary>
        /// Moves the iterator to the start of the page.
        /// </summary>
        public void Begin()
        {
            VerifyNotDisposed();
            if (handle.Handle != IntPtr.Zero)
            {
                TessApi.Native.PageIteratorBegin(handle);
            }
        }

        public Pix GetBinaryImage(PageIteratorLevel level)
        {
            VerifyNotDisposed();
            return handle.Handle == IntPtr.Zero ? null : Pix.Create(TessApi.Native.PageIteratorGetBinaryImage(handle, level));
        }

        public Pix GetImage(PageIteratorLevel level, int padding, out int x, out int y)
        {
            VerifyNotDisposed();
            if (handle.Handle == IntPtr.Zero)
            {
                x = 0;
                y = 0;

                return null;
            }

            return Pix.Create(TessApi.Native.PageIteratorGetImage(handle, level, padding, page.Image.Handle, out x, out y));
        }

        /// <summary>
        /// Gets the element orientation information that the iterator currently points too.
        /// </summary>
        public ElementProperties GetProperties()
        {
            VerifyNotDisposed();
            if (handle.Handle == IntPtr.Zero)
            {
                return new ElementProperties(Orientation.PageUp, TextLineOrder.TopToBottom, WritingDirection.LeftToRight, 0f);
            }

            TessApi.Native
                .PageIteratorOrientation(
                    handle,
                    out Orientation orientation,
                    out WritingDirection writing_direction,
                    out TextLineOrder textLineOrder,
                    out float deskew_angle);

            return new ElementProperties(orientation, textLineOrder, writing_direction, deskew_angle);
        }

        /// <summary>
        /// Returns <c>True</c> if the iterator is at the first element at the given level.
        /// </summary>
        /// <remarks>
        /// A possible use is to determin if a call to next(word) moved to the start of a new paragraph.
        /// </remarks>
        /// <param name="level"></param>
        /// <returns></returns>
        public bool IsAtBeginningOf(PageIteratorLevel level)
        {
            VerifyNotDisposed();

            return handle.Handle != IntPtr.Zero && TessApi.Native.PageIteratorIsAtBeginningOf(handle, level) != 0;
        }

        /// <summary>
        /// Returns <c>True</c> if the iterator is positioned at the last element at the given level.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="element"></param>
        /// <returns></returns>
        public bool IsAtFinalOf(PageIteratorLevel level, PageIteratorLevel element)
        {
            VerifyNotDisposed();

            return handle.Handle != IntPtr.Zero && TessApi.Native.PageIteratorIsAtFinalElement(handle, level, element) != 0;
        }

        /// <summary>
        /// Moves to the start of the next element at the given level.
        /// </summary>
        /// <remarks>
        ///
        /// </remarks>
        /// <param name="level"></param>
        /// <returns></returns>
        public bool Next(PageIteratorLevel level)
        {
            VerifyNotDisposed();
            return handle.Handle != IntPtr.Zero && TessApi.Native.PageIteratorNext(handle, level) != 0;
        }

        /// <summary>
        /// Moves the iterator to the next <paramref name="element"/> iff the iterator is not currently pointing to the
        /// last <paramref name="element"/> in the specified <paramref name="level"/> (i.e. the last word in the
        /// paragraph).
        /// </summary>
        /// <param name="level">The iterator level.</param>
        /// <param name="element">The page level.</param>
        /// <returns>
        /// <c>True</c> iff there is another <paramref name="element"/> to advance too and the current element is not
        /// the last element at the given level; otherwise returns <c>False</c>.
        /// </returns>
        public bool Next(PageIteratorLevel level, PageIteratorLevel element)
        {
            VerifyNotDisposed();

            bool isAtFinalElement = IsAtFinalOf(level, element);
            return !isAtFinalElement && Next(element);
        }

        /// <summary>
        /// Gets the baseline of the current element at the given level.
        /// </summary>
        /// <remarks>
        /// The baseline is the line that passes through (x1, y1) and (x2, y2). WARNING: with vertical text, baselines
        /// may be vertical! Returns false if there is no baseline at the current position.
        /// </remarks>
        /// <param name="level"></param>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public bool TryGetBaseline(PageIteratorLevel level, out Rect bounds)
        {
            VerifyNotDisposed();
            if (handle.Handle != IntPtr.Zero && TessApi.Native.PageIteratorBaseline(handle, level, out int x1, out int y1, out int x2, out int y2) != 0)
            {
                bounds = Rect.FromCoords(x1, y1, x2, y2);
                return true;
            }

            bounds = Rect.Empty;
            return false;
        }

        /// <summary>
        /// Gets the bounding rectangle of the current element at the given level.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public bool TryGetBoundingBox(PageIteratorLevel level, out Rect bounds)
        {
            VerifyNotDisposed();
            if (handle.Handle != IntPtr.Zero && TessApi.Native.PageIteratorBoundingBox(handle, level, out int x1, out int y1, out int x2, out int y2) != 0)
            {
                bounds = Rect.FromCoords(x1, y1, x2, y2);
                return true;
            }

            bounds = Rect.Empty;
            return false;
        }

        internal PageIterator(Page page, IntPtr handle)
        {
            this.page = page;
            this.handle = new HandleRef(this, handle);
        }

        protected readonly HandleRef handle;

        protected readonly Page page;

        protected override void Dispose(bool disposing)
        {
            if (handle.Handle != IntPtr.Zero)
            {
                TessApi.Native.PageIteratorDelete(handle);
            }
        }
    }
}