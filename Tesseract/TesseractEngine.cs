﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Tesseract.Internal;
using Tesseract.Interop;

namespace Tesseract
{
    /// <summary>
    ///     The tesseract OCR engine.
    /// </summary>
    public class TesseractEngine : DisposableBase
    {
        /// <summary>
        ///     Creates a new instance of <see cref="TesseractEngine" /> using the <see cref="EngineMode.Default" /> mode.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The <paramref name="datapath" /> parameter should point to the directory that contains the 'tessdata' folder
        ///         for example if your tesseract language data is installed in <c>C:\Tesseract\tessdata</c> the value of datapath
        ///         should
        ///         be <c>C:\Tesseract</c>. Note that tesseract will use the value of the <c>TESSDATA_PREFIX</c> environment
        ///         variable if defined,
        ///         effectively ignoring the value of <paramref name="datapath" /> parameter.
        ///     </para>
        /// </remarks>
        /// <param name="datapath">
        ///     The path to the parent directory that contains the 'tessdata' directory, ignored if the
        ///     <c>TESSDATA_PREFIX</c> environment variable is defined.
        /// </param>
        /// <param name="language">The language to load, for example 'eng' for English.</param>
        public TesseractEngine(string datapath, string language)
            : this(datapath, language, EngineMode.Default, new string[0], new Dictionary<string, object>(), false)
        {
        }

        /// <summary>
        ///     Creates a new instance of <see cref="TesseractEngine" /> with the specified <paramref name="configFile" />
        ///     using the <see cref="EngineMode.Default">Default Engine Mode</see>.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The <paramref name="datapath" /> parameter should point to the directory that contains the 'tessdata' folder
        ///         for example if your tesseract language data is installed in <c>C:\Tesseract\tessdata</c> the value of datapath
        ///         should
        ///         be <c>C:\Tesseract</c>. Note that tesseract will use the value of the <c>TESSDATA_PREFIX</c> environment
        ///         variable if defined,
        ///         effectively ignoring the value of <paramref name="datapath" /> parameter.
        ///     </para>
        ///     <para>
        ///         Note: That the config files MUST be encoded without the BOM using unix end of line characters.
        ///     </para>
        /// </remarks>
        /// <param name="datapath">
        ///     The path to the parent directory that contains the 'tessdata' directory, ignored if the
        ///     <c>TESSDATA_PREFIX</c> environment variable is defined.
        /// </param>
        /// <param name="language">The language to load, for example 'eng' for English.</param>
        /// <param name="configFile">
        ///     An optional tesseract configuration file that is encoded using UTF8 without BOM
        ///     with Unix end of line characters you can use an advanced text editor such as Notepad++ to accomplish this.
        /// </param>
        public TesseractEngine(string datapath, string language, string configFile)
            : this(datapath, language, EngineMode.Default, configFile != null ? new[] { configFile } : new string[0],
                new Dictionary<string, object>(), false)
        {
        }

        /// <summary>
        ///     Creates a new instance of <see cref="TesseractEngine" /> with the specified <paramref name="configFiles" />
        ///     using the <see cref="EngineMode.Default">Default Engine Mode</see>.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The <paramref name="datapath" /> parameter should point to the directory that contains the 'tessdata' folder
        ///         for example if your tesseract language data is installed in <c>C:\Tesseract\tessdata</c> the value of datapath
        ///         should
        ///         be <c>C:\Tesseract</c>. Note that tesseract will use the value of the <c>TESSDATA_PREFIX</c> environment
        ///         variable if defined,
        ///         effectively ignoring the value of <paramref name="datapath" /> parameter.
        ///     </para>
        /// </remarks>
        /// <param name="datapath">
        ///     The path to the parent directory that contains the 'tessdata' directory, ignored if the
        ///     <c>TESSDATA_PREFIX</c> environment variable is defined.
        /// </param>
        /// <param name="language">The language to load, for example 'eng' for English.</param>
        /// <param name="configFiles">
        ///     An optional sequence of tesseract configuration files to load, encoded using UTF8 without BOM
        ///     with Unix end of line characters you can use an advanced text editor such as Notepad++ to accomplish this.
        /// </param>
        public TesseractEngine(string datapath, string language, IEnumerable<string> configFiles)
            : this(datapath, language, EngineMode.Default, configFiles, new Dictionary<string, object>(), false)
        {
        }

        /// <summary>
        ///     Creates a new instance of <see cref="TesseractEngine" /> with the specified <paramref name="engineMode" />.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The <paramref name="datapath" /> parameter should point to the directory that contains the 'tessdata' folder
        ///         for example if your tesseract language data is installed in <c>C:\Tesseract\tessdata</c> the value of datapath
        ///         should
        ///         be <c>C:\Tesseract</c>. Note that tesseract will use the value of the <c>TESSDATA_PREFIX</c> environment
        ///         variable if defined,
        ///         effectively ignoring the value of <paramref name="datapath" /> parameter.
        ///     </para>
        /// </remarks>
        /// <param name="datapath">
        ///     The path to the parent directory that contains the 'tessdata' directory, ignored if the
        ///     <c>TESSDATA_PREFIX</c> environment variable is defined.
        /// </param>
        /// <param name="language">The language to load, for example 'eng' for English.</param>
        /// <param name="engineMode">The <see cref="EngineMode" /> value to use when initialising the tesseract engine.</param>
        public TesseractEngine(string datapath, string language, EngineMode engineMode)
            : this(datapath, language, engineMode, new string[0], new Dictionary<string, object>(), false)
        {
        }

        /// <summary>
        ///     Creates a new instance of <see cref="TesseractEngine" /> with the specified <paramref name="engineMode" /> and
        ///     <paramref name="configFile" />.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The <paramref name="datapath" /> parameter should point to the directory that contains the 'tessdata' folder
        ///         for example if your tesseract language data is installed in <c>C:\Tesseract\tessdata</c> the value of datapath
        ///         should
        ///         be <c>C:\Tesseract</c>. Note that tesseract will use the value of the <c>TESSDATA_PREFIX</c> environment
        ///         variable if defined,
        ///         effectively ignoring the value of <paramref name="datapath" /> parameter.
        ///     </para>
        ///     <para>
        ///         Note: That the config files MUST be encoded without the BOM using unix end of line characters.
        ///     </para>
        /// </remarks>
        /// <param name="datapath">
        ///     The path to the parent directory that contains the 'tessdata' directory, ignored if the
        ///     <c>TESSDATA_PREFIX</c> environment variable is defined.
        /// </param>
        /// <param name="language">The language to load, for example 'eng' for English.</param>
        /// <param name="engineMode">The <see cref="EngineMode" /> value to use when initialising the tesseract engine.</param>
        /// <param name="configFile">
        ///     An optional tesseract configuration file that is encoded using UTF8 without BOM
        ///     with Unix end of line characters you can use an advanced text editor such as Notepad++ to accomplish this.
        /// </param>
        public TesseractEngine(string datapath, string language, EngineMode engineMode, string configFile)
            : this(datapath, language, engineMode, configFile != null ? new[] { configFile } : new string[0],
                new Dictionary<string, object>(), false)
        {
        }

        /// <summary>
        ///     Creates a new instance of <see cref="TesseractEngine" /> with the specified <paramref name="engineMode" /> and
        ///     <paramref name="configFiles" />.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The <paramref name="datapath" /> parameter should point to the directory that contains the 'tessdata' folder
        ///         for example if your tesseract language data is installed in <c>C:\Tesseract\tessdata</c> the value of datapath
        ///         should
        ///         be <c>C:\Tesseract</c>. Note that tesseract will use the value of the <c>TESSDATA_PREFIX</c> environment
        ///         variable if defined,
        ///         effectively ignoring the value of <paramref name="datapath" /> parameter.
        ///     </para>
        /// </remarks>
        /// <param name="datapath">
        ///     The path to the parent directory that contains the 'tessdata' directory, ignored if the
        ///     <c>TESSDATA_PREFIX</c> environment variable is defined.
        /// </param>
        /// <param name="language">The language to load, for example 'eng' for English.</param>
        /// <param name="engineMode">The <see cref="EngineMode" /> value to use when initialising the tesseract engine.</param>
        /// <param name="configFiles">
        ///     An optional sequence of tesseract configuration files to load, encoded using UTF8 without BOM
        ///     with Unix end of line characters you can use an advanced text editor such as Notepad++ to accomplish this.
        /// </param>
        public TesseractEngine(string datapath, string language, EngineMode engineMode, IEnumerable<string> configFiles)
            : this(datapath, language, engineMode, configFiles, new Dictionary<string, object>(), false)
        {
        }

        /// <summary>
        ///     Creates a new instance of <see cref="TesseractEngine" /> with the specified <paramref name="engineMode" /> and
        ///     <paramref name="configFiles" />.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The <paramref name="datapath" /> parameter should point to the directory that contains the 'tessdata' folder
        ///         for example if your tesseract language data is installed in <c>C:\Tesseract\tessdata</c> the value of datapath
        ///         should
        ///         be <c>C:\Tesseract</c>. Note that tesseract will use the value of the <c>TESSDATA_PREFIX</c> environment
        ///         variable if defined,
        ///         effectively ignoring the value of <paramref name="datapath" /> parameter.
        ///     </para>
        /// </remarks>
        /// <param name="datapath">
        ///     The path to the parent directory that contains the 'tessdata' directory, ignored if the
        ///     <c>TESSDATA_PREFIX</c> environment variable is defined.
        /// </param>
        /// <param name="language">The language to load, for example 'eng' for English.</param>
        /// <param name="engineMode">The <see cref="EngineMode" /> value to use when initialising the tesseract engine.</param>
        /// <param name="configFiles">
        ///     An optional sequence of tesseract configuration files to load, encoded using UTF8 without BOM
        ///     with Unix end of line characters you can use an advanced text editor such as Notepad++ to accomplish this.
        /// </param>
        public TesseractEngine(string datapath, string language, EngineMode engineMode, IEnumerable<string> configFiles,
            IDictionary<string, object> initialOptions, bool setOnlyNonDebugVariables)
        {
            Guard.RequireNotNullOrEmpty(nameof(language), language);

            DefaultPageSegMode = PageSegMode.Auto;
            handle = new HandleRef(this, TessApi.Native.BaseApiCreate());

            Initialise(datapath, language, engineMode, configFiles, initialOptions, setOnlyNonDebugVariables);
        }

        public string Version =>

            TessApi.BaseApiGetVersion();

        /// <summary>
        ///     Processes the specific image.
        /// </summary>
        /// <remarks>
        ///     You can only have one result iterator open at any one time.
        /// </remarks>
        /// <param name="image">The image to process.</param>
        /// <param name="pageSegMode">The page layout analyasis method to use.</param>
        public Page Process(Pix image, PageSegMode? pageSegMode = null)
        {
            return Process(image, null, new Rect(0, 0, image.Width, image.Height), pageSegMode);
        }

        /// <summary>
        ///     Processes a specified region in the image using the specified page layout analysis mode.
        /// </summary>
        /// <remarks>
        ///     You can only have one result iterator open at any one time.
        /// </remarks>
        /// <param name="image">The image to process.</param>
        /// <param name="region">The image region to process.</param>
        /// <param name="pageSegMode">The page layout analyasis method to use.</param>
        /// <returns>A result iterator</returns>
        public Page Process(Pix image, Rect region, PageSegMode? pageSegMode = null)
        {
            return Process(image, null, region, pageSegMode);
        }

        /// <summary>
        ///     Processes the specific image.
        /// </summary>
        /// <remarks>
        ///     You can only have one result iterator open at any one time.
        /// </remarks>
        /// <param name="image">The image to process.</param>
        /// <param name="inputName">Sets the input file's name, only needed for training or loading a uzn file.</param>
        /// <param name="pageSegMode">The page layout analyasis method to use.</param>
        public Page Process(Pix image, string inputName, PageSegMode? pageSegMode = null)
        {
            return Process(image, inputName, new Rect(0, 0, image.Width, image.Height), pageSegMode);
        }

        /// <summary>
        ///     Processes a specified region in the image using the specified page layout analysis mode.
        /// </summary>
        /// <remarks>
        ///     You can only have one result iterator open at any one time.
        /// </remarks>
        /// <param name="image">The image to process.</param>
        /// <param name="inputName">Sets the input file's name, only needed for training or loading a uzn file.</param>
        /// <param name="region">The image region to process.</param>
        /// <param name="pageSegMode">The page layout analyasis method to use.</param>
        /// <returns>A result iterator</returns>
        public Page Process(Pix image, string inputName, Rect region, PageSegMode? pageSegMode = null)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            if (region.X1 < 0 || region.Y1 < 0 || region.X2 > image.Width || region.Y2 > image.Height)
            {
                throw new ArgumentException("The image region to be processed must be within the image bounds.",
                    nameof(region));
            }

            if (processCount > 0)
            {
                throw new InvalidOperationException(
                    "Only one image can be processed at once. Please make sure you dispose of the page once your finished with it.");
            }

            processCount++;

            PageSegMode actualPageSegmentMode = pageSegMode ?? DefaultPageSegMode;
            TessApi.Native.BaseAPISetPageSegMode(handle, actualPageSegmentMode);
            TessApi.Native.BaseApiSetImage(handle, image.Handle);
            if (!string.IsNullOrEmpty(inputName))
            {
                TessApi.Native.BaseApiSetInputName(handle, inputName);
            }

            Page page = new Page(this, image, inputName, region, actualPageSegmentMode);
            page.Disposed += OnIteratorDisposed;
            return page;
        }

        /// <summary>
        ///     Ties the specified pix to the lifecycle of a page.
        /// </summary>
        public class PageDisposalHandle
        {
            public PageDisposalHandle(Page page, Pix pix)
            {
                this.page = page;
                this.pix = pix;
                page.Disposed += OnPageDisposed;
            }

            private readonly Page page;

            private readonly Pix pix;

            private void OnPageDisposed(object sender, EventArgs e)
            {
                page.Disposed -= OnPageDisposed;

                pix.Dispose();
            }
        }

        internal HandleRef Handle => handle;

        protected override void Dispose(bool disposing)
        {
            if (handle.Handle != IntPtr.Zero)
            {
                TessApi.Native.BaseApiDelete(handle);
                handle = new HandleRef(this, IntPtr.Zero);
            }
        }

        private HandleRef handle;

        private int processCount;

        private void Initialise(string datapath, string language, EngineMode engineMode,
            IEnumerable<string> configFiles, IDictionary<string, object> initialValues, bool setOnlyNonDebugVariables)
        {
            Guard.RequireNotNullOrEmpty(nameof(language), language);

            if (!string.IsNullOrEmpty(datapath))
            {
                datapath = datapath.Trim();

                if (datapath.EndsWith("\\", StringComparison.Ordinal) ||
                    datapath.EndsWith("/", StringComparison.Ordinal))
                {
                    datapath = datapath.Substring(0, datapath.Length - 1);
                }
            }

            if (TessApi.BaseApiInit(handle, datapath, language, (int)engineMode, configFiles ?? new List<string>(),
                    initialValues ?? new Dictionary<string, object>(), setOnlyNonDebugVariables) != 0)
            {
                handle = new HandleRef(this, IntPtr.Zero);
                GC.SuppressFinalize(this);

                throw new TesseractException(ErrorMessage.Format(1, "Failed to initialise tesseract engine."));
            }
        }

        #region Event Handlers

        private void OnIteratorDisposed(object sender, EventArgs e)
        {
            processCount--;
        }

        #endregion Event Handlers

        #region Config

        /// <summary>
        ///     Gets or sets default <see cref="PageSegMode" /> mode used by
        ///     <see cref="TesseractEngine.Process(Pix, Rect, PageSegMode?)" />.
        /// </summary>
        public PageSegMode DefaultPageSegMode { get; set; }

        public bool SetDebugVariable(string name, string value)
        {
            return TessApi.BaseApiSetDebugVariable(handle, name, value) != 0;
        }

        /// <summary>
        ///     Sets the value of a string variable.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="value">The new value of the variable.</param>
        /// <returns>Returns <c>True</c> if successful; otherwise <c>False</c>.</returns>
        public bool SetVariable(string name, string value)
        {
            return TessApi.BaseApiSetVariable(handle, name, value) != 0;
        }

        /// <summary>
        ///     Sets the value of a boolean variable.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="value">The new value of the variable.</param>
        /// <returns>Returns <c>True</c> if successful; otherwise <c>False</c>.</returns>
        public bool SetVariable(string name, bool value)
        {
            string strEncodedValue = value ? "TRUE" : "FALSE";
            return TessApi.BaseApiSetVariable(handle, name, strEncodedValue) != 0;
        }

        /// <summary>
        ///     Sets the value of a integer variable.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="value">The new value of the variable.</param>
        /// <returns>Returns <c>True</c> if successful; otherwise <c>False</c>.</returns>
        public bool SetVariable(string name, int value)
        {
            string strEncodedValue = value.ToString("D", CultureInfo.InvariantCulture.NumberFormat);
            return TessApi.BaseApiSetVariable(handle, name, strEncodedValue) != 0;
        }

        /// <summary>
        ///     Sets the value of a double variable.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="value">The new value of the variable.</param>
        /// <returns>Returns <c>True</c> if successful; otherwise <c>False</c>.</returns>
        public bool SetVariable(string name, double value)
        {
            string strEncodedValue = value.ToString("R", CultureInfo.InvariantCulture.NumberFormat);
            return TessApi.BaseApiSetVariable(handle, name, strEncodedValue) != 0;
        }

        /// <summary>
        ///     Attempts to retrieve the value for a boolean variable.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="value">The current value of the variable.</param>
        /// <returns>Returns <c>True</c> if successful; otherwise <c>False</c>.</returns>
        public bool TryGetBoolVariable(string name, out bool value)
        {
            if (TessApi.Native.BaseApiGetBoolVariable(handle, name, out int val) != 0)
            {
                value = val != 0;
                return true;
            }

            value = false;
            return false;
        }

        /// <summary>
        ///     Attempts to retrieve the value for a double variable.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="value">The current value of the variable.</param>
        /// <returns>Returns <c>True</c> if successful; otherwise <c>False</c>.</returns>
        public bool TryGetDoubleVariable(string name, out double value)
        {
            return TessApi.Native.BaseApiGetDoubleVariable(handle, name, out value) != 0;
        }

        /// <summary>
        ///     Attempts to retrieve the value for an integer variable.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="value">The current value of the variable.</param>
        /// <returns>Returns <c>True</c> if successful; otherwise <c>False</c>.</returns>
        public bool TryGetIntVariable(string name, out int value)
        {
            return TessApi.Native.BaseApiGetIntVariable(handle, name, out value) != 0;
        }

        /// <summary>
        ///     Attempts to retrieve the value for a string variable.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="value">The current value of the variable.</param>
        /// <returns>Returns <c>True</c> if successful; otherwise <c>False</c>.</returns>
        public bool TryGetStringVariable(string name, out string value)
        {
            value = TessApi.BaseApiGetStringVariable(handle, name);
            return value != null;
        }

        /// <summary>
        ///     Attempts to print the variables to the file.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public bool TryPrintVariablesToFile(string filename)
        {
            return TessApi.Native.BaseApiPrintVariablesToFile(handle, filename) != 0;
        }

        #endregion Config
    }
}