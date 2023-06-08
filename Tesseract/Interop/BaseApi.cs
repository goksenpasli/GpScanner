﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Tesseract.Internal;
using Tesseract.Internal.InteropDotNet;

namespace Tesseract.Interop
{
    /// <summary>
    /// The exported tesseract api signatures.
    /// </summary>
    /// <remarks>
    /// Please note this is only public for technical reasons (you can't proxy a internal interface). It should be
    /// considered an internal interface and is NOT part of the public api and may have breaking changes between
    /// releases.
    /// </remarks>
    public interface ITessApiSignatures
    {
        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIAnalyseLayout")]
        IntPtr BaseAPIAnalyseLayout(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIClear")]
        void BaseAPIClear(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIGetComponentImages")]
        IntPtr BaseAPIGetComponentImages(HandleRef handle, PageIteratorLevel level, int text_only, IntPtr pixa, IntPtr blockids);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIGetDatapath")]
        string BaseAPIGetDatapath(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIGetPageSegMode")]
        PageSegMode BaseAPIGetPageSegMode(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIGetThresholdedImage")]
        IntPtr BaseAPIGetThresholdedImage(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIGetUTF8Text")]
        IntPtr BaseAPIGetUTF8TextInternal(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIMeanTextConf")]
        int BaseAPIMeanTextConf(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPISetInputName")]
        void BaseAPISetInputName(HandleRef handle, string name);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPISetOutputName")]
        void BaseAPISetOutputName(HandleRef handle, string name);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPISetPageSegMode")]
        void BaseAPISetPageSegMode(HandleRef handle, PageSegMode mode);

        /// <summary>
        /// Creates a new BaseAPI instance
        /// </summary>
        /// <returns></returns>
        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPICreate")]
        IntPtr BaseApiCreate();

        /// <summary>
        /// Deletes a base api instance.
        /// </summary>
        /// <returns></returns>
        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIDelete")]
        void BaseApiDelete(HandleRef ptr);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIGetAltoText")]
        IntPtr BaseApiGetAltoTextInternal(HandleRef handle, int pageNum);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIGetBoolVariable")]
        int BaseApiGetBoolVariable(HandleRef handle, string name, out int value);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIGetBoxText")]
        IntPtr BaseApiGetBoxTextInternal(HandleRef handle, int pageNum);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIGetDoubleVariable")]
        int BaseApiGetDoubleVariable(HandleRef handle, string name, out double value);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIGetHOCRText")]
        IntPtr BaseApiGetHOCRTextInternal(HandleRef handle, int pageNum);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIGetIntVariable")]
        int BaseApiGetIntVariable(HandleRef handle, string name, out int value);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIGetIterator")]
        IntPtr BaseApiGetIterator(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIGetLSTMBoxText")]
        IntPtr BaseApiGetLSTMBoxTextInternal(HandleRef handle, int pageNum);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIGetStringVariable")]
        IntPtr BaseApiGetStringVariableInternal(HandleRef handle, string name);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIGetTsvText")]
        IntPtr BaseApiGetTsvTextInternal(HandleRef handle, int pageNum);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIGetUNLVText")]
        IntPtr BaseApiGetUNLVTextInternal(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIGetWordStrBoxText")]
        IntPtr BaseApiGetWordStrBoxTextInternal(HandleRef handle, int pageNum);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIInit4")]
        int BaseApiInit(
            HandleRef handle,
            string datapath,
            string language,
            int mode,
            string[] configs,
            int configs_size,
            string[] vars_vec,
            string[] vars_values,
            UIntPtr vars_vec_size,
            bool set_only_non_debug_params);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIPrintVariablesToFile")]
        int BaseApiPrintVariablesToFile(HandleRef handle, string filename);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIRecognize")]
        int BaseApiRecognize(HandleRef handle, HandleRef monitor);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPISetDebugVariable")]
        int BaseApiSetDebugVariable(HandleRef handle, string name, IntPtr valPtr);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPISetImage2")]
        void BaseApiSetImage(HandleRef handle, HandleRef pixHandle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPISetInputName")]
        void BaseApiSetInputName(HandleRef handle, string value);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPISetRectangle")]
        void BaseApiSetRectangle(HandleRef handle, int left, int top, int width, int height);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPISetVariable")]
        int BaseApiSetVariable(HandleRef handle, string name, IntPtr valPtr);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessDeleteIntArray")]
        void DeleteIntArray(IntPtr arr);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessDeleteText")]
        void DeleteText(IntPtr textPtr);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessDeleteTextArray")]
        void DeleteTextArray(IntPtr arr);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessVersion")]
        IntPtr GetVersion();

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessPageIteratorBaseline")]
        int PageIteratorBaseline(HandleRef handle, PageIteratorLevel level, out int x1, out int y1, out int x2, out int y2);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessPageIteratorBegin")]
        void PageIteratorBegin(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessPageIteratorBlockType")]
        PolyBlockType PageIteratorBlockType(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessPageIteratorBoundingBox")]
        int PageIteratorBoundingBox(HandleRef handle, PageIteratorLevel level, out int left, out int top, out int right, out int bottom);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessPageIteratorCopy")]
        IntPtr PageIteratorCopy(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessPageIteratorDelete")]
        void PageIteratorDelete(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessPageIteratorGetBinaryImage")]
        IntPtr PageIteratorGetBinaryImage(HandleRef handle, PageIteratorLevel level);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessPageIteratorGetImage")]
        IntPtr PageIteratorGetImage(HandleRef handle, PageIteratorLevel level, int padding, HandleRef originalImage, out int left, out int top);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessPageIteratorIsAtBeginningOf")]
        int PageIteratorIsAtBeginningOf(HandleRef handle, PageIteratorLevel level);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessPageIteratorIsAtFinalElement")]
        int PageIteratorIsAtFinalElement(HandleRef handle, PageIteratorLevel level, PageIteratorLevel element);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessPageIteratorNext")]
        int PageIteratorNext(HandleRef handle, PageIteratorLevel level);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessPageIteratorOrientation")]
        void PageIteratorOrientation(HandleRef handle, out Orientation orientation, out WritingDirection writing_direction, out TextLineOrder textLineOrder, out float deskew_angle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultIteratorCopy")]
        IntPtr ResultIteratorCopy(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultIteratorDelete")]
        void ResultIteratorDelete(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultIteratorConfidence")]
        float ResultIteratorGetConfidence(HandleRef handle, PageIteratorLevel level);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultIteratorGetPageIterator")]
        IntPtr ResultIteratorGetPageIterator(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultIteratorGetUTF8Text")]
        IntPtr ResultIteratorGetUTF8TextInternal(HandleRef handle, PageIteratorLevel level);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultIteratorSymbolIsDropcap")]
        bool ResultIteratorSymbolIsDropcap(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultIteratorSymbolIsSubscript")]
        bool ResultIteratorSymbolIsSubscript(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultIteratorSymbolIsSuperscript")]
        bool ResultIteratorSymbolIsSuperscript(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultIteratorWordFontAttributes")]
        IntPtr ResultIteratorWordFontAttributes(
            HandleRef handle,
            out bool isBold,
            out bool isItalic,
            out bool isUnderlined,
            out bool isMonospace,
            out bool isSerif,
            out bool isSmallCaps,
            out int pointSize,
            out int fontId);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultIteratorWordIsFromDictionary")]
        bool ResultIteratorWordIsFromDictionary(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultIteratorWordIsNumeric")]
        bool ResultIteratorWordIsNumeric(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultIteratorWordRecognitionLanguage")]
        IntPtr ResultIteratorWordRecognitionLanguageInternal(HandleRef handle);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBaseAPIDetectOrientationScript")]
        int TessBaseAPIDetectOrientationScript(HandleRef handle, out int orient_deg, out float orient_conf, out IntPtr script_name, out float script_conf);

        #region Choice Iterator

        /// <summary>
        /// Native API call to TessChoiceIteratorDelete
        /// </summary>
        /// <param name="handle"></param>
        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessChoiceIteratorDelete")]
        void ChoiceIteratorDelete(HandleRef handle);

        /// <summary>
        /// Native API call to TessChoiceIteratorConfidence
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessChoiceIteratorConfidence")]
        float ChoiceIteratorGetConfidence(HandleRef handle);

        /// <summary>
        /// Native API call to TessChoiceIteratorGetUTF8Text
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessChoiceIteratorGetUTF8Text")]
        IntPtr ChoiceIteratorGetUTF8TextInternal(HandleRef handle);

        /// <summary>
        /// Native API call to TessChoiceIteratorNext
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessChoiceIteratorNext")]
        int ChoiceIteratorNext(HandleRef handle);

        /// <summary>
        /// Native API call to TessResultIteratorGetChoiceIterator
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultIteratorGetChoiceIterator")]
        IntPtr ResultIteratorGetChoiceIterator(HandleRef handle);
        #endregion Choice Iterator

        #region Renderer API
        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessAltoRendererCreate")]
        IntPtr AltoRendererCreate(string outputbase);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessBoxTextRendererCreate")]
        IntPtr BoxTextRendererCreate(string outputbase);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessDeleteResultRenderer")]
        void DeleteResultRenderer(HandleRef renderer);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessHOcrRendererCreate")]
        IntPtr HOcrRendererCreate(string outputbase);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessHOcrRendererCreate2")]
        IntPtr HOcrRendererCreate2(string outputbase, int font_info);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessLSTMBoxRendererCreate")]
        IntPtr LSTMBoxRendererCreate(string outputbase);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessPDFRendererCreate")]
        IntPtr PDFRendererCreate(string outputbase, IntPtr datadir, int textonly);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultRendererAddImage")]
        int ResultRendererAddImage(HandleRef renderer, HandleRef api);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultRendererBeginDocument")]
        int ResultRendererBeginDocument(HandleRef renderer, IntPtr titlePtr);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultRendererEndDocument")]
        int ResultRendererEndDocument(HandleRef renderer);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultRendererExtention")]
        IntPtr ResultRendererExtention(HandleRef renderer);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultRendererImageNum")]
        int ResultRendererImageNum(HandleRef renderer);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultRendererInsert")]
        void ResultRendererInsert(HandleRef renderer, HandleRef next);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultRendererNext")]
        IntPtr ResultRendererNext(HandleRef renderer);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessResultRendererTitle")]
        IntPtr ResultRendererTitle(HandleRef renderer);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessTextRendererCreate")]
        IntPtr TextRendererCreate(string outputbase);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessTsvRendererCreate")]
        IntPtr TsvRendererCreate(string outputbase);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessUnlvRendererCreate")]
        IntPtr UnlvRendererCreate(string outputbase);

        [RuntimeDllImport(Constants.TesseractDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TessWordStrBoxRendererCreate")]
        IntPtr WordStrBoxRendererCreate(string outputbase);
    #endregion Renderer API
    }

    internal static class TessApi
    {
        public const string htmlBeginTag =
            "<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 4.01 Transitional//EN\"" +
            " \"http://www.w3.org/TR/html4/loose.dtd\">\n" +
            "<html>\n<head>\n<title></title>\n" +
            "<meta http-equiv=\"Content-Type\" content=\"text/html;" +
            "charset=utf-8\" />\n<meta name='ocr-system' content='tesseract'/>\n" +
            "</head>\n<body>\n";

        public const string htmlEndTag = "</body>\n</html>\n";

        public const string xhtmlBeginTag =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Transitional//EN\"\n" +
            "    \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd\">\n" +
            "<html xmlns=\"http://www.w3.org/1999/xhtml\" xml:lang=\"en\" " +
            "lang=\"en\">\n <head>\n  <title></title>\n" +
            "<meta http-equiv=\"Content-Type\" content=\"text/html;" +
            "charset=utf-8\" />\n" +
            "  <meta name='ocr-system' content='tesseract' />\n" +
            "  <meta name='ocr-capabilities' content='ocr_page ocr_carea ocr_par" +
            " ocr_line ocrx_word" +
            "'/>\n" +
            "</head>\n<body>\n";

        public const string xhtmlEndTag = " </body>\n</html>\n";

        /// <summary>
        /// Returns the null terminated UTF-8 encoded text string for the current choice
        /// </summary>
        /// <remarks>
        /// NOTE: Unlike LTRResultIterator::GetUTF8Text, the return points to an internal structure and should NOT be
        /// delete[]ed to free after use.
        /// </remarks>
        /// <param name="choiceIteratorHandle"></param>
        /// <returns>string</returns>
        internal static string ChoiceIteratorGetUTF8Text(HandleRef choiceIteratorHandle)
        {
            Guard.Require(nameof(choiceIteratorHandle), choiceIteratorHandle.Handle != IntPtr.Zero, "ChoiceIterator Handle cannot be a null IntPtr and is required");
            IntPtr txtChoiceHandle = Native.ChoiceIteratorGetUTF8TextInternal(choiceIteratorHandle);
            return MarshalHelper.PtrToString(txtChoiceHandle, Encoding.UTF8);
        }

        public static string BaseAPIGetAltoText(HandleRef handle, int pageNum)
        {
            IntPtr txtHandle = Native.BaseApiGetAltoTextInternal(handle, pageNum);
            if(txtHandle != IntPtr.Zero)
            {
                string result = MarshalHelper.PtrToString(txtHandle, Encoding.UTF8);
                Native.DeleteText(txtHandle);
                return result;
            }

            return null;
        }

        public static string BaseAPIGetBoxText(HandleRef handle, int pageNum)
        {
            IntPtr txtHandle = Native.BaseApiGetBoxTextInternal(handle, pageNum);
            if(txtHandle != IntPtr.Zero)
            {
                string result = MarshalHelper.PtrToString(txtHandle, Encoding.UTF8);
                Native.DeleteText(txtHandle);
                return result;
            }

            return null;
        }

        public static string BaseAPIGetHOCRText(HandleRef handle, int pageNum)
        {
            IntPtr txtHandle = Native.BaseApiGetHOCRTextInternal(handle, pageNum);
            if(txtHandle != IntPtr.Zero)
            {
                string result = MarshalHelper.PtrToString(txtHandle, Encoding.UTF8);
                Native.DeleteText(txtHandle);
                return $"{htmlBeginTag}{result}{htmlEndTag}";
            }

            return null;
        }

        public static string BaseAPIGetHOCRText2(HandleRef handle, int pageNum)
        {
            IntPtr txtHandle = Native.BaseApiGetHOCRTextInternal(handle, pageNum);
            if(txtHandle != IntPtr.Zero)
            {
                string result = MarshalHelper.PtrToString(txtHandle, Encoding.UTF8);
                Native.DeleteText(txtHandle);
                return $"{xhtmlBeginTag}{result}{xhtmlEndTag}";
            }

            return null;
        }

        public static string BaseAPIGetLSTMBoxText(HandleRef handle, int pageNum)
        {
            IntPtr txtHandle = Native.BaseApiGetLSTMBoxTextInternal(handle, pageNum);
            if(txtHandle != IntPtr.Zero)
            {
                string result = MarshalHelper.PtrToString(txtHandle, Encoding.UTF8);
                Native.DeleteText(txtHandle);
                return result;
            }

            return null;
        }

        public static string BaseAPIGetTsvText(HandleRef handle, int pageNum)
        {
            IntPtr txtHandle = Native.BaseApiGetTsvTextInternal(handle, pageNum);
            if(txtHandle != IntPtr.Zero)
            {
                string result = MarshalHelper.PtrToString(txtHandle, Encoding.UTF8);
                Native.DeleteText(txtHandle);
                return result;
            }

            return null;
        }

        public static string BaseAPIGetUNLVText(HandleRef handle)
        {
            IntPtr txtHandle = Native.BaseApiGetUNLVTextInternal(handle);
            if(txtHandle != IntPtr.Zero)
            {
                string result = MarshalHelper.PtrToString(txtHandle, Encoding.UTF8);
                Native.DeleteText(txtHandle);
                return result;
            }

            return null;
        }

        public static string BaseAPIGetUTF8Text(HandleRef handle)
        {
            IntPtr txtHandle = Native.BaseAPIGetUTF8TextInternal(handle);
            if(txtHandle != IntPtr.Zero)
            {
                string result = MarshalHelper.PtrToString(txtHandle, Encoding.UTF8);
                Native.DeleteText(txtHandle);
                return result;
            }

            return null;
        }

        public static string BaseAPIGetWordStrBoxText(HandleRef handle, int pageNum)
        {
            IntPtr txtHandle = Native.BaseApiGetWordStrBoxTextInternal(handle, pageNum);
            if(txtHandle != IntPtr.Zero)
            {
                string result = MarshalHelper.PtrToString(txtHandle, Encoding.UTF8);
                Native.DeleteText(txtHandle);
                return result;
            }

            return null;
        }

        public static string BaseApiGetStringVariable(HandleRef handle, string name)
        {
            IntPtr resultHandle = Native.BaseApiGetStringVariableInternal(handle, name);
            return resultHandle != IntPtr.Zero ? MarshalHelper.PtrToString(resultHandle, Encoding.UTF8) : null;
        }

        public static string BaseApiGetVersion()
        {
            IntPtr versionHandle = Native.GetVersion();
            return versionHandle != IntPtr.Zero ? MarshalHelper.PtrToString(versionHandle, Encoding.UTF8) : null;
        }

        public static int BaseApiInit(
            HandleRef handle,
            string datapath,
            string language,
            int mode,
            IEnumerable<string> configFiles,
            IDictionary<string, object> initialValues,
            bool setOnlyNonDebugParams)
        {
            Guard.Require(nameof(handle), handle.Handle != IntPtr.Zero, "Handle for BaseApi, created through BaseApiCreate is required.");
            Guard.RequireNotNullOrEmpty(nameof(language), language);
            Guard.RequireNotNull("configFiles", configFiles);
            Guard.RequireNotNull("initialValues", initialValues);

            string[] configFilesArray = new List<string>(configFiles).ToArray();

            string[] varNames = new string[initialValues.Count];
            string[] varValues = new string[initialValues.Count];
            int i = 0;
            foreach(KeyValuePair<string, object> pair in initialValues)
            {
                Guard.Require(nameof(initialValues), !string.IsNullOrEmpty(pair.Key), "Variable must have a name.");

                Guard.Require(nameof(initialValues), pair.Value != null, "Variable '{0}': The type '{1}' is not supported.", pair.Key, pair.Value.GetType());
                varNames[i] = pair.Key;
                varValues[i] = TessConvert.TryToString(pair.Value, out string varValue)
                    ? varValue
                    : throw new ArgumentException($"Variable '{pair.Key}': The type '{pair.Value.GetType()}' is not supported.", nameof(initialValues));
                i++;
            }

            return Native.BaseApiInit(
                handle,
                datapath,
                language,
                mode,
                configFilesArray,
                configFilesArray.Length,
                varNames,
                varValues,
                new UIntPtr((uint)varNames.Length),
                setOnlyNonDebugParams);
        }

        public static int BaseApiSetDebugVariable(HandleRef handle, string name, string value)
        {
            IntPtr valuePtr = IntPtr.Zero;
            try
            {
                valuePtr = MarshalHelper.StringToPtr(value, Encoding.UTF8);
                return Native.BaseApiSetDebugVariable(handle, name, valuePtr);
            }
            finally
            {
                if(valuePtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(valuePtr);
                }
            }
        }

        public static int BaseApiSetVariable(HandleRef handle, string name, string value)
        {
            IntPtr valuePtr = IntPtr.Zero;
            try
            {
                valuePtr = MarshalHelper.StringToPtr(value, Encoding.UTF8);
                return Native.BaseApiSetVariable(handle, name, valuePtr);
            }
            finally
            {
                if(valuePtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(valuePtr);
                }
            }
        }

        public static void Initialize()
        {
            if(native == null)
            {
                LeptonicaApi.Initialize();
                native = InteropRuntimeImplementer.CreateInstance<ITessApiSignatures>();
            }
        }

        public static string ResultIteratorGetUTF8Text(HandleRef handle, PageIteratorLevel level)
        {
            IntPtr txtHandle = Native.ResultIteratorGetUTF8TextInternal(handle, level);
            if(txtHandle != IntPtr.Zero)
            {
                string result = MarshalHelper.PtrToString(txtHandle, Encoding.UTF8);
                Native.DeleteText(txtHandle);
                return result;
            }

            return null;
        }

        public static string ResultIteratorWordRecognitionLanguage(HandleRef handle)
        {
            IntPtr txtHandle =
                Native.ResultIteratorWordRecognitionLanguageInternal(handle);

            return txtHandle != IntPtr.Zero ? MarshalHelper.PtrToString(txtHandle, Encoding.UTF8) : null;
        }

        public static ITessApiSignatures Native
        {
            get
            {
                if(native == null)
                {
                    Initialize();
                }

                return native;
            }
        }

        private static ITessApiSignatures native;
    }
}