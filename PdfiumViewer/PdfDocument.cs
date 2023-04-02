﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;

namespace PdfiumViewer
{
    /// <summary>
    /// Provides functionality to render a PDF document.
    /// </summary>
    public class PdfDocument : IPdfDocument
    {
        /// <summary>
        /// Bookmarks stored in this PdfFile
        /// </summary>
        public PdfBookmarkCollection Bookmarks => _file.Bookmarks;

        /// <summary>
        /// Number of pages in the PDF document.
        /// </summary>
        public int PageCount => PageSizes.Count;

        /// <summary>
        /// Size of each page in the PDF document.
        /// </summary>
        public IList<SizeF> PageSizes { get; private set; }

        /// <summary>
        /// Initializes a new instance of the PdfDocument class with the provided path.
        /// </summary>
        /// <param name="path">Path to the PDF document.</param>
        public static PdfDocument Load(string path)
        {
            return Load(path, null);
        }

        /// <summary>
        /// Initializes a new instance of the PdfDocument class with the provided path.
        /// </summary>
        /// <param name="path">Path to the PDF document.</param>
        /// <param name="password">Password for the PDF document.</param>
        public static PdfDocument Load(string path, string password)
        {
            return path == null ? throw new ArgumentNullException(nameof(path)) : Load(File.OpenRead(path), password);
        }

        /// <summary>
        /// Initializes a new instance of the PdfDocument class with the provided path.
        /// </summary>
        /// <param name="owner">Window to show any UI for.</param>
        /// <param name="path">Path to the PDF document.</param>
        public static PdfDocument Load(IWin32Window owner, string path)
        {
            return owner == null
                ? throw new ArgumentNullException(nameof(owner))
                : path == null ? throw new ArgumentNullException(nameof(path)) : Load(owner, File.OpenRead(path), null);
        }

        /// <summary>
        /// Initializes a new instance of the PdfDocument class with the provided path.
        /// </summary>
        /// <param name="owner">Window to show any UI for.</param>
        /// <param name="stream">Stream for the PDF document.</param>
        public static PdfDocument Load(IWin32Window owner, Stream stream)
        {
            return owner == null
                ? throw new ArgumentNullException(nameof(owner))
                : stream == null ? throw new ArgumentNullException(nameof(stream)) : Load(owner, stream, null);
        }

        /// <summary>
        /// Initializes a new instance of the PdfDocument class with the provided stream.
        /// </summary>
        /// <param name="stream">Stream for the PDF document.</param>
        public static PdfDocument Load(Stream stream)
        {
            return Load(stream, null);
        }

        /// <summary>
        /// Initializes a new instance of the PdfDocument class with the provided stream.
        /// </summary>
        /// <param name="stream">Stream for the PDF document.</param>
        /// <param name="password">Password for the PDF document.</param>
        public static PdfDocument Load(Stream stream, string password)
        {
            return stream == null ? throw new ArgumentNullException(nameof(stream)) : new PdfDocument(stream, password);
        }

        /// <summary>
        /// Creates a <see cref="PrintDocument"/> for the PDF document.
        /// </summary>
        /// <returns></returns>
        public PrintDocument CreatePrintDocument()
        {
            return CreatePrintDocument(PdfPrintMode.CutMargin);
        }

        /// <summary>
        /// Creates a <see cref="PrintDocument"/> for the PDF document.
        /// </summary>
        /// <param name="printMode">Specifies the mode for printing. The default
        /// value for this parameter is CutMargin.</param>
        /// <returns></returns>
        public PrintDocument CreatePrintDocument(PdfPrintMode printMode)
        {
            return CreatePrintDocument(new PdfPrintSettings(printMode, null));
        }

        /// <summary>
        /// Creates a <see cref="PrintDocument"/> for the PDF document.
        /// </summary>
        /// <param name="settings">The settings used to configure the print document.</param>
        /// <returns></returns>
        public PrintDocument CreatePrintDocument(PdfPrintSettings settings)
        {
            return new PdfPrintDocument(this, settings);
        }

        /// <summary>
        /// Delete the page from the PDF document.
        /// </summary>
        /// <param name="page">The page to delete.</param>
        public void DeletePage(int page)
        {
            _file.DeletePage(page);
            _pageSizes.RemoveAt(page);
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Get metadata information from the PDF document.
        /// </summary>
        /// <returns>The PDF metadata.</returns>
        public PdfInformation GetInformation()
        {
            return _file.GetInformation();
        }

        /// <summary>
        /// Returns all links on the PDF page.
        /// </summary>
        /// <param name="page">The page to get the links for.</param>
        /// <param name="size">The size of the page.</param>
        /// <returns>A collection with the links on the page.</returns>
        public PdfPageLinks GetPageLinks(int page, Size size)
        {
            return _file.GetPageLinks(page, size);
        }

        /// <summary>
        /// Get all text on the page.
        /// </summary>
        /// <param name="page">The page to get the text for.</param>
        /// <returns>The text on the page.</returns>
        public string GetPdfText(int page)
        {
            return _file.GetPdfText(page);
        }

        /// <summary>
        /// Get all text matching the text span.
        /// </summary>
        /// <param name="textSpan">The span to get the text for.</param>
        /// <returns>The text matching the span.</returns>
        public string GetPdfText(PdfTextSpan textSpan)
        {
            return _file.GetPdfText(textSpan);
        }

        /// <summary>
        /// Get all bounding rectangles for the text span.
        /// </summary>
        /// <description>
        /// The algorithm used to get the bounding rectangles tries to join
        /// adjacent character bounds into larger rectangles.
        /// </description>
        /// <param name="textSpan">The span to get the bounding rectangles for.</param>
        /// <returns>The bounding rectangles.</returns>
        public IList<PdfRectangle> GetTextBounds(PdfTextSpan textSpan)
        {
            return _file.GetTextBounds(textSpan);
        }

        /// <summary>
        /// Convert a point from page coordinates to device coordinates.
        /// </summary>
        /// <param name="page">The page number where the point is from.</param>
        /// <param name="point">The point to convert.</param>
        /// <returns>The converted point.</returns>
        public Point PointFromPdf(int page, PointF point)
        {
            return _file.PointFromPdf(page, point);
        }

        /// <summary>
        /// Convert a point from device coordinates to page coordinates.
        /// </summary>
        /// <param name="page">The page number where the point is from.</param>
        /// <param name="point">The point to convert.</param>
        /// <returns>The converted point.</returns>
        public PointF PointToPdf(int page, Point point)
        {
            return _file.PointToPdf(page, point);
        }

        /// <summary>
        /// Convert a rectangle from page coordinates to device coordinates.
        /// </summary>
        /// <param name="page">The page where the rectangle is from.</param>
        /// <param name="rect">The rectangle to convert.</param>
        /// <returns>The converted rectangle.</returns>
        public Rectangle RectangleFromPdf(int page, RectangleF rect)
        {
            return _file.RectangleFromPdf(page, rect);
        }

        /// <summary>
        /// Convert a rectangle from device coordinates to page coordinates.
        /// </summary>
        /// <param name="page">The page where the rectangle is from.</param>
        /// <param name="rect">The rectangle to convert.</param>
        /// <returns>The converted rectangle.</returns>
        public RectangleF RectangleToPdf(int page, Rectangle rect)
        {
            return _file.RectangleToPdf(page, rect);
        }

        /// <summary>
        /// Renders a page of the PDF document to the provided graphics instance.
        /// </summary>
        /// <param name="page">Number of the page to render.</param>
        /// <param name="graphics">Graphics instance to render the page on.</param>
        /// <param name="dpiX">Horizontal DPI.</param>
        /// <param name="dpiY">Vertical DPI.</param>
        /// <param name="bounds">Bounds to render the page in.</param>
        /// <param name="forPrinting">Render the page for printing.</param>
        public void Render(int page, Graphics graphics, float dpiX, float dpiY, Rectangle bounds, bool forPrinting)
        {
            Render(page, graphics, dpiX, dpiY, bounds, forPrinting ? PdfRenderFlags.ForPrinting : PdfRenderFlags.None);
        }

        /// <summary>
        /// Renders a page of the PDF document to the provided graphics instance.
        /// </summary>
        /// <param name="page">Number of the page to render.</param>
        /// <param name="graphics">Graphics instance to render the page on.</param>
        /// <param name="dpiX">Horizontal DPI.</param>
        /// <param name="dpiY">Vertical DPI.</param>
        /// <param name="bounds">Bounds to render the page in.</param>
        /// <param name="flags">Flags used to influence the rendering.</param>
        public void Render(int page, Graphics graphics, float dpiX, float dpiY, Rectangle bounds, PdfRenderFlags flags)
        {
            if (graphics == null)
            {
                throw new ArgumentNullException(nameof(graphics));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            float graphicsDpiX = graphics.DpiX;
            float graphicsDpiY = graphics.DpiY;

            IntPtr dc = graphics.GetHdc();

            try
            {
                if ((int)graphicsDpiX != (int)dpiX || (int)graphicsDpiY != (int)dpiY)
                {
                    NativeMethods.XFORM transform = new NativeMethods.XFORM
                    {
                        eM11 = graphicsDpiX / dpiX,
                        eM22 = graphicsDpiY / dpiY
                    };

                    _ = NativeMethods.SetGraphicsMode(dc, NativeMethods.GM_ADVANCED);
                    _ = NativeMethods.ModifyWorldTransform(dc, ref transform, NativeMethods.MWT_LEFTMULTIPLY);
                }

                NativeMethods.POINT point = new NativeMethods.POINT();
                _ = NativeMethods.SetViewportOrgEx(dc, bounds.X, bounds.Y, out point);

                bool success = _file.RenderPDFPageToDC(
                    page,
                    dc,
                    (int)dpiX, (int)dpiY,
                    0, 0, bounds.Width, bounds.Height,
                    FlagsToFPDFFlags(flags)
                );

                _ = NativeMethods.SetViewportOrgEx(dc, point.X, point.Y, out point);

                if (!success)
                {
                    throw new Win32Exception();
                }
            }
            finally
            {
                graphics.ReleaseHdc(dc);
            }
        }

        /// <summary>
        /// Renders a page of the PDF document to an image.
        /// </summary>
        /// <param name="page">Number of the page to render.</param>
        /// <param name="dpiX">Horizontal DPI.</param>
        /// <param name="dpiY">Vertical DPI.</param>
        /// <param name="forPrinting">Render the page for printing.</param>
        /// <returns>The rendered image.</returns>
        public Image Render(int page, float dpiX, float dpiY, bool forPrinting)
        {
            SizeF size = PageSizes[page];

            return Render(page, (int)size.Width, (int)size.Height, dpiX, dpiY, forPrinting);
        }

        /// <summary>
        /// Renders a page of the PDF document to an image.
        /// </summary>
        /// <param name="page">Number of the page to render.</param>
        /// <param name="dpiX">Horizontal DPI.</param>
        /// <param name="dpiY">Vertical DPI.</param>
        /// <param name="flags">Flags used to influence the rendering.</param>
        /// <returns>The rendered image.</returns>
        public Image Render(int page, float dpiX, float dpiY, PdfRenderFlags flags)
        {
            SizeF size = PageSizes[page];

            return Render(page, (int)size.Width, (int)size.Height, dpiX, dpiY, flags);
        }

        /// <summary>
        /// Renders a page of the PDF document to an image.
        /// </summary>
        /// <param name="page">Number of the page to render.</param>
        /// <param name="width">Width of the rendered image.</param>
        /// <param name="height">Height of the rendered image.</param>
        /// <param name="dpiX">Horizontal DPI.</param>
        /// <param name="dpiY">Vertical DPI.</param>
        /// <param name="forPrinting">Render the page for printing.</param>
        /// <returns>The rendered image.</returns>
        public Image Render(int page, int width, int height, float dpiX, float dpiY, bool forPrinting)
        {
            return Render(page, width, height, dpiX, dpiY, forPrinting ? PdfRenderFlags.ForPrinting : PdfRenderFlags.None);
        }

        /// <summary>
        /// Renders a page of the PDF document to an image.
        /// </summary>
        /// <param name="page">Number of the page to render.</param>
        /// <param name="width">Width of the rendered image.</param>
        /// <param name="height">Height of the rendered image.</param>
        /// <param name="dpiX">Horizontal DPI.</param>
        /// <param name="dpiY">Vertical DPI.</param>
        /// <param name="flags">Flags used to influence the rendering.</param>
        /// <returns>The rendered image.</returns>
        public Image Render(int page, int width, int height, float dpiX, float dpiY, PdfRenderFlags flags)
        {
            return Render(page, width, height, dpiX, dpiY, 0, flags);
        }

        /// <summary>
        /// Renders a page of the PDF document to an image.
        /// </summary>
        /// <param name="page">Number of the page to render.</param>
        /// <param name="width">Width of the rendered image.</param>
        /// <param name="height">Height of the rendered image.</param>
        /// <param name="dpiX">Horizontal DPI.</param>
        /// <param name="dpiY">Vertical DPI.</param>
        /// <param name="rotate">Rotation.</param>
        /// <param name="flags">Flags used to influence the rendering.</param>
        /// <returns>The rendered image.</returns>
        public Image Render(int page, int width, int height, float dpiX, float dpiY, PdfRotation rotate, PdfRenderFlags flags)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            if ((flags & PdfRenderFlags.CorrectFromDpi) != 0)
            {
                width = width * (int)dpiX / 72;
                height = height * (int)dpiY / 72;
            }

            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            bitmap.SetResolution(dpiX, dpiY);

            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, bitmap.PixelFormat);

            try
            {
                IntPtr handle = NativeMethods.FPDFBitmap_CreateEx(width, height, 4, data.Scan0, width * 4);

                try
                {
                    uint background = (flags & PdfRenderFlags.Transparent) == 0 ? 0xFFFFFFFF : 0x00FFFFFF;

                    NativeMethods.FPDFBitmap_FillRect(handle, 0, 0, width, height, background);

                    bool success = _file.RenderPDFPageToBitmap(
                        page,
                        handle,
                        (int)dpiX, (int)dpiY,
                        0, 0, width, height,
                        (int)rotate,
                        FlagsToFPDFFlags(flags),
                        (flags & PdfRenderFlags.Annotations) != 0
                    );

                    if (!success)
                    {
                        throw new Win32Exception();
                    }
                }
                finally
                {
                    _ = NativeMethods.FPDFBitmap_Destroy(handle);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return bitmap;
        }

        /// <summary>
        /// Rotate the page.
        /// </summary>
        /// <param name="page">The page to rotate.</param>
        /// <param name="rotation">How to rotate the page.</param>
        public void RotatePage(int page, PdfRotation rotation)
        {
            _file.RotatePage(page, rotation);
            _pageSizes[page] = _file.GetPDFDocInfo(page);
        }

        /// <summary>
        /// Save the PDF document to the specified location.
        /// </summary>
        /// <param name="path">Path to save the PDF document to.</param>
        public void Save(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            using (FileStream stream = File.Create(path))
            {
                Save(stream);
            }
        }

        /// <summary>
        /// Save the PDF document to the specified location.
        /// </summary>
        /// <param name="stream">Stream to save the PDF document to.</param>
        public void Save(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            _file.Save(stream);
        }

        /// <summary>
        /// Finds all occurences of text.
        /// </summary>
        /// <param name="text">The text to search for.</param>
        /// <param name="matchCase">Whether to match case.</param>
        /// <param name="wholeWord">Whether to match whole words only.</param>
        /// <returns>All matches.</returns>
        public PdfMatches Search(string text, bool matchCase, bool wholeWord)
        {
            return Search(text, matchCase, wholeWord, 0, PageCount - 1);
        }

        /// <summary>
        /// Finds all occurences of text.
        /// </summary>
        /// <param name="text">The text to search for.</param>
        /// <param name="matchCase">Whether to match case.</param>
        /// <param name="wholeWord">Whether to match whole words only.</param>
        /// <param name="page">The page to search on.</param>
        /// <returns>All matches.</returns>
        public PdfMatches Search(string text, bool matchCase, bool wholeWord, int page)
        {
            return Search(text, matchCase, wholeWord, page, page);
        }

        /// <summary>
        /// Finds all occurences of text.
        /// </summary>
        /// <param name="text">The text to search for.</param>
        /// <param name="matchCase">Whether to match case.</param>
        /// <param name="wholeWord">Whether to match whole words only.</param>
        /// <param name="startPage">The page to start searching.</param>
        /// <param name="endPage">The page to end searching.</param>
        /// <returns>All matches.</returns>
        public PdfMatches Search(string text, bool matchCase, bool wholeWord, int startPage, int endPage)
        {
            return _file.Search(text, matchCase, wholeWord, startPage, endPage);
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        /// <param name="disposing">Whether this method is called from Dispose.</param>
        protected void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                if (_file != null)
                {
                    _file.Dispose();
                    _file = null;
                }

                _disposed = true;
            }
        }

        private readonly List<SizeF> _pageSizes;

        private bool _disposed;

        private PdfFile _file;

        private PdfDocument(Stream stream, string password)
        {
            _file = new PdfFile(stream, password);

            _pageSizes = _file.GetPDFDocInfo();
            if (_pageSizes == null)
            {
                throw new Win32Exception();
            }

            PageSizes = new ReadOnlyCollection<SizeF>(_pageSizes);
        }

        private static PdfDocument Load(IWin32Window owner, Stream stream, string password)
        {
            try
            {
                while (true)
                {
                    try
                    {
                        return new PdfDocument(stream, password);
                    }
                    catch (PdfException ex)
                    {
                        if (owner != null && ex.Error == PdfError.PasswordProtected)
                        {
                            using (PasswordForm form = new PasswordForm())
                            {
                                if (form.ShowDialog(owner) == DialogResult.OK)
                                {
                                    password = form.Password;
                                    continue;
                                }
                            }
                        }

                        throw;
                    }
                }
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        private NativeMethods.FPDF FlagsToFPDFFlags(PdfRenderFlags flags)
        {
            return (NativeMethods.FPDF)(flags & ~(PdfRenderFlags.Transparent | PdfRenderFlags.CorrectFromDpi));
        }
    }
}