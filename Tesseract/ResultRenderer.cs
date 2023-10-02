﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Tesseract.Internal;
using Tesseract.Interop;

namespace Tesseract
{
    /// <summary>
    /// Rendered formats supported by Tesseract.
    /// </summary>
    public enum RenderedFormat
    {
        TEXT,

        HOCR,

        PDF,

        PDF_TEXTONLY,

        UNLV,

        BOX,

        ALTO,

        TSV,

        LSTMBOX,

        WORDSTRBOX
    }

    public sealed class AltoResultRenderer : ResultRenderer
    {
        public AltoResultRenderer(string outputFilename)
        {
            IntPtr rendererHandle = TessApi.Native.AltoRendererCreate(outputFilename);
            Initialise(rendererHandle);
        }
    }

    public sealed class BoxResultRenderer : ResultRenderer
    {
        public BoxResultRenderer(string outputFilename)
        {
            IntPtr rendererHandle = TessApi.Native.BoxTextRendererCreate(outputFilename);
            Initialise(rendererHandle);
        }
    }

    public sealed class HOcrResultRenderer : ResultRenderer
    {
        public HOcrResultRenderer(string outputFilename, bool fontInfo = false)
        {
            IntPtr rendererHandle = TessApi.Native.HOcrRendererCreate2(outputFilename, fontInfo ? 1 : 0);
            Initialise(rendererHandle);
        }
    }

    public sealed class LSTMBoxResultRenderer : ResultRenderer
    {
        public LSTMBoxResultRenderer(string outputFilename)
        {
            IntPtr rendererHandle = TessApi.Native.LSTMBoxRendererCreate(outputFilename);
            Initialise(rendererHandle);
        }
    }

    public sealed class PdfResultRenderer : ResultRenderer
    {
        private IntPtr _fontDirectoryHandle;

        public PdfResultRenderer(string outputFilename, string fontDirectory, bool textonly)
        {
            IntPtr fontDirectoryHandle = Marshal.StringToHGlobalAnsi(fontDirectory);
            IntPtr rendererHandle =
                TessApi.Native.PDFRendererCreate(outputFilename, fontDirectoryHandle, textonly ? 1 : 0);

            Initialise(rendererHandle);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (_fontDirectoryHandle != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_fontDirectoryHandle);
                _fontDirectoryHandle = IntPtr.Zero;
            }
        }
    }

    /// <summary>
    /// Represents a native result renderer (e.g. text, pdf, etc).
    /// </summary>
    /// <remarks>
    /// Note that the ResultRenderer is explictly responsible for managing the renderer hierarchy. This gets around a
    /// number of difficult issues such as keeping track of what the next renderer is and how to manage the memory.
    /// </remarks>
    public abstract class ResultRenderer : DisposableBase, IResultRenderer
    {
        private IDisposable _currentDocumentHandle;

        protected ResultRenderer() { Handle = new HandleRef(this, IntPtr.Zero); }

        public int PageNumber
        {
            get
            {
                VerifyNotDisposed();

                return TessApi.Native.ResultRendererImageNum(Handle);
            }
        }

        protected HandleRef Handle { get; private set; }

        /// <summary>
        /// Add the page to the current document.
        /// </summary>
        /// <param name="page"></param>
        /// <returns><c>True</c> if the page was successfully added to the result renderer; otherwise false.</returns>
        public bool AddPage(Page page)
        {
            Guard.RequireNotNull("page", page);
            VerifyNotDisposed();

            // TODO: Force page to do a recognise run to ensure the underlying base api is full of state note if
            page.Recognize();

            return TessApi.Native.ResultRendererAddImage(Handle, page.Engine.Handle) != 0;
        }

        /// <summary>
        /// Begins a new document with the specified <paramref name="title"/>.
        /// </summary>
        /// <param name="title">The (ANSI) title of the new document.</param>
        /// <returns>A handle that when disposed of ends the current document.</returns>
        public IDisposable BeginDocument(string title)
        {
            Guard.RequireNotNull("title", title);
            VerifyNotDisposed();
            Guard.Verify(_currentDocumentHandle == null, "Cannot begin document \"{0}\" as another document is currently being processed which must be dispose off first.", title);

            IntPtr titlePtr = Marshal.StringToHGlobalAnsi(title);
            if (TessApi.Native.ResultRendererBeginDocument(Handle, titlePtr) == 0)
            {
                Marshal.FreeHGlobal(titlePtr);

                throw new InvalidOperationException($"Failed to begin document \"{title}\".");
            }

            _currentDocumentHandle = new EndDocumentOnDispose(this, titlePtr);
            return _currentDocumentHandle;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && _currentDocumentHandle != null)
                {
                    _currentDocumentHandle.Dispose();
                    _currentDocumentHandle = null;
                }
            }
            finally
            {
                if (Handle.Handle != IntPtr.Zero)
                {
                    TessApi.Native.DeleteResultRenderer(Handle);
                    Handle = new HandleRef(this, IntPtr.Zero);
                }
            }
        }

        /// <summary>
        /// Initialise the render to use the specified native result renderer.
        /// </summary>
        /// <param name="handle"></param>
        protected void Initialise(IntPtr handle)
        {
            Guard.Require(nameof(handle), handle != IntPtr.Zero, "handle must be initialised.");
            Guard.Verify(Handle.Handle == IntPtr.Zero, "Rensult renderer has already been initialised.");

            Handle = new HandleRef(this, handle);
        }

        /// <summary>
        /// Ensures the renderer's EndDocument when disposed off.
        /// </summary>
        private class EndDocumentOnDispose : DisposableBase
        {
            private readonly ResultRenderer _renderer;
            private IntPtr _titlePtr;

            public EndDocumentOnDispose(ResultRenderer renderer, IntPtr titlePtr)
            {
                _renderer = renderer;
                _titlePtr = titlePtr;
            }

            protected override void Dispose(bool disposing)
            {
                try
                {
                    if (disposing)
                    {
                        Guard.Verify(_renderer._currentDocumentHandle == this, "Expected the Result Render's active document to be this document.");

                        _ = TessApi.Native.ResultRendererEndDocument(_renderer.Handle);
                        _renderer._currentDocumentHandle = null;
                    }
                }
                finally
                {
                    if (_titlePtr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(_titlePtr);
                        _titlePtr = IntPtr.Zero;
                    }
                }
            }
        }

        #region Factory Methods

        /// <summary>
        /// Creates a <see cref="IResultRenderer">result renderer</see> that render that generates an Alto file from
        /// tesseract's output.
        /// </summary>
        /// <param name="outputFilename">The path to the Alto file to be created without the file extension.</param>
        /// <returns></returns>
        public static IResultRenderer CreateAltoRenderer(string outputFilename) => new AltoResultRenderer(outputFilename);

        /// <summary>
        /// Creates a <see cref="IResultRenderer">result renderer</see> that render that generates a box text file from
        /// tesseract's output.
        /// </summary>
        /// <param name="outputFilename">The path to the box file to be created without the file extension.</param>
        /// <returns></returns>
        public static IResultRenderer CreateBoxRenderer(string outputFilename) => new BoxResultRenderer(outputFilename);

        /// <summary>
        /// Creates a <see cref="IResultRenderer">result renderer</see> that render that generates a HOCR file from
        /// tesseract's output.
        /// </summary>
        /// <param name="outputFilename">The path to the hocr file to be generated without the file extension.</param>
        /// <param name="fontInfo">Determines if the generated HOCR file includes font information or not.</param>
        /// <returns></returns>
        public static IResultRenderer CreateHOcrRenderer(string outputFilename, bool fontInfo = false) => new HOcrResultRenderer(outputFilename, fontInfo);

        /// <summary>
        /// Creates a <see cref="IResultRenderer">result renderer</see> that render that generates a unlv file from
        /// tesseract's output.
        /// </summary>
        /// <param name="outputFilename">The path to the unlv file to be created without the file extension.</param>
        /// <returns></returns>
        public static IResultRenderer CreateLSTMBoxRenderer(string outputFilename) => new LSTMBoxResultRenderer(outputFilename);

        /// <summary>
        /// Creates a <see cref="IResultRenderer">result renderer</see> that render that generates a searchable pdf file
        /// from tesseract's output.
        /// </summary>
        /// <param name="outputFilename">The filename of the pdf file to be generated without the file extension.</param>
        /// <param name="fontDirectory">The directory containing the pdf font data, normally same as your tessdata directory.</param>
        /// <param name="textonly">skip images if set</param>
        /// <returns></returns>
        public static IResultRenderer CreatePdfRenderer(string outputFilename, string fontDirectory, bool textonly) => new PdfResultRenderer(outputFilename, fontDirectory, textonly);

        /// <summary>
        /// Creates renderers for specified output formats.
        /// </summary>
        /// <param name="outputbase"></param>
        /// <param name="dataPath">The directory containing the pdf font data, normally same as your tessdata directory.</param>
        /// <param name="outputFormats"></param>
        /// <returns></returns>
        public static IEnumerable<IResultRenderer> CreateRenderers(string outputbase, string dataPath, List<RenderedFormat> outputFormats)
        {
            List<IResultRenderer> renderers = new List<IResultRenderer>();

            foreach (RenderedFormat format in outputFormats)
            {
                IResultRenderer renderer = null;

                switch (format)
                {
                    case RenderedFormat.TEXT:
                        renderer = CreateTextRenderer(outputbase);
                        break;

                    case RenderedFormat.HOCR:
                        renderer = CreateHOcrRenderer(outputbase);
                        break;

                    case RenderedFormat.PDF:
                    case RenderedFormat.PDF_TEXTONLY:
                        bool textonly = format == RenderedFormat.PDF_TEXTONLY;
                        renderer = CreatePdfRenderer(outputbase, dataPath, textonly);
                        break;

                    case RenderedFormat.BOX:
                        renderer = CreateBoxRenderer(outputbase);
                        break;

                    case RenderedFormat.UNLV:
                        renderer = CreateUnlvRenderer(outputbase);
                        break;

                    case RenderedFormat.ALTO:
                        renderer = CreateAltoRenderer(outputbase);
                        break;

                    case RenderedFormat.TSV:
                        renderer = CreateTsvRenderer(outputbase);
                        break;

                    case RenderedFormat.LSTMBOX:
                        renderer = CreateLSTMBoxRenderer(outputbase);
                        break;

                    case RenderedFormat.WORDSTRBOX:
                        renderer = CreateWordStrBoxRenderer(outputbase);
                        break;
                }

                renderers.Add(renderer);
            }

            return renderers;
        }

        /// <summary>
        /// Creates a <see cref="IResultRenderer">result renderer</see> that render that generates UTF-8 encoded text
        /// file from tesseract's output.
        /// </summary>
        /// <param name="outputFilename">The path to the text file to be generated without the file extension.</param>
        /// <returns></returns>
        public static IResultRenderer CreateTextRenderer(string outputFilename) => new TextResultRenderer(outputFilename);

        /// <summary>
        /// Creates a <see cref="IResultRenderer">result renderer</see> that render that generates a Tsv file from
        /// tesseract's output.
        /// </summary>
        /// <param name="outputFilename">The path to the Tsv file to be created without the file extension.</param>
        /// <returns></returns>
        public static IResultRenderer CreateTsvRenderer(string outputFilename) => new TsvResultRenderer(outputFilename);

        /// <summary>
        /// Creates a <see cref="IResultRenderer">result renderer</see> that render that generates a unlv file from
        /// tesseract's output.
        /// </summary>
        /// <param name="outputFilename">The path to the unlv file to be created without the file extension.</param>
        /// <returns></returns>
        public static IResultRenderer CreateUnlvRenderer(string outputFilename) => new UnlvResultRenderer(outputFilename);

        /// <summary>
        /// Creates a <see cref="IResultRenderer">result renderer</see> that render that generates a unlv file from
        /// tesseract's output.
        /// </summary>
        /// <param name="outputFilename">The path to the unlv file to be created without the file extension.</param>
        /// <returns></returns>
        public static IResultRenderer CreateWordStrBoxRenderer(string outputFilename) => new WordStrBoxResultRenderer(outputFilename);
    #endregion Factory Methods
    }

    public sealed class TextResultRenderer : ResultRenderer
    {
        public TextResultRenderer(string outputFilename)
        {
            IntPtr rendererHandle = TessApi.Native.TextRendererCreate(outputFilename);
            Initialise(rendererHandle);
        }
    }

    public sealed class TsvResultRenderer : ResultRenderer
    {
        public TsvResultRenderer(string outputFilename)
        {
            IntPtr rendererHandle = TessApi.Native.TsvRendererCreate(outputFilename);
            Initialise(rendererHandle);
        }
    }

    public sealed class UnlvResultRenderer : ResultRenderer
    {
        public UnlvResultRenderer(string outputFilename)
        {
            IntPtr rendererHandle = TessApi.Native.UnlvRendererCreate(outputFilename);
            Initialise(rendererHandle);
        }
    }

    public sealed class WordStrBoxResultRenderer : ResultRenderer
    {
        public WordStrBoxResultRenderer(string outputFilename)
        {
            IntPtr rendererHandle = TessApi.Native.WordStrBoxRendererCreate(outputFilename);
            Initialise(rendererHandle);
        }
    }
}