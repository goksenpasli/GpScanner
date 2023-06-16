using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Tesseract.Internal;
using Tesseract.Interop;

namespace Tesseract
{
    public sealed class Page : DisposableBase
    {
        public TesseractEngine Engine { get; }

        /// <summary>
        /// Gets the <see cref="Pix"/> that is being ocr'd.
        /// </summary>
        public Pix Image { get; }

        /// <summary>
        /// Gets the name of the image being ocr'd.
        /// </summary>
        /// <remarks>
        /// This is also used for some of the more advanced functionality such as identifying the associated UZN file if
        /// present.
        /// </remarks>
        public string ImageName { get; }

        /// <summary>
        /// Gets the page segmentation mode used to OCR the specified image.
        /// </summary>
        public PageSegMode PageSegmentMode { get; }

        /// <summary>
        /// The current region of interest being parsed.
        /// </summary>
        public Rect RegionOfInterest
        {
            get => regionOfInterest;

            set
            {
                if(value.X1 < 0 || value.Y1 < 0 || value.X2 > Image.Width || value.Y2 > Image.Height)
                {
                    throw new ArgumentException("The region of interest to be processed must be within the image bounds.", nameof(value));
                }

                if(regionOfInterest != value)
                {
                    regionOfInterest = value;

                    TessApi.Native.BaseApiSetRectangle(Engine.Handle, regionOfInterest.X1, regionOfInterest.Y1, regionOfInterest.Width, regionOfInterest.Height);

                    runRecognitionPhase = false;
                }
            }
        }

        /// <summary>
        /// Creates a <see cref="PageIterator"/> object that is used to iterate over the page's layout as defined by the
        /// current <see cref="RegionOfInterest"/>.
        /// </summary>
        /// <returns></returns>
        public PageIterator AnalyseLayout()
        {
            Guard.Verify(
                PageSegmentMode != PageSegMode.OsdOnly,
                "Cannot analyse image layout when using OSD only page segmentation, please use DetectBestOrientation instead.");

            IntPtr resultIteratorHandle = TessApi.Native.BaseAPIAnalyseLayout(Engine.Handle);
            return new PageIterator(this, resultIteratorHandle);
        }

        /// <summary>
        /// Detects the page orientation, with corresponding confidence when using <see cref="PageSegMode.OsdOnly"/>.
        /// </summary>
        /// <remarks>
        /// If using full page segmentation mode (i.e. AutoOsd) then consider using <see cref="AnalyseLayout"/> instead
        /// as this also provides a deskew angle which isn't available when just performing orientation detection.
        /// </remarks>
        /// <param name="orientation">The page orientation.</param>
        /// <param name="confidence">The confidence level of the orientation (15 is reasonably confident).</param>
        [Obsolete("Use DetectBestOrientation(int orientationDegrees, float confidence) that returns orientation in degrees instead.")]
        public void DetectBestOrientation(out Orientation orientation, out float confidence)
        {
            DetectBestOrientation(out int orientationDegrees, out float orientationConfidence);

            orientationDegrees %= 360;
            if(orientationDegrees < 0)
            {
                orientationDegrees += 360;
            }

            orientation = orientationDegrees > 315 || orientationDegrees <= 45
                ? Orientation.PageUp
                : orientationDegrees > 45 && orientationDegrees <= 135
                    ? Orientation.PageRight
                    : orientationDegrees > 135 && orientationDegrees <= 225 ? Orientation.PageDown : Orientation.PageLeft;

            confidence = orientationConfidence;
        }

        /// <summary>
        /// Detects the page orientation, with corresponding confidence when using <see cref="PageSegMode.OsdOnly"/>.
        /// </summary>
        /// <remarks>
        /// If using full page segmentation mode (i.e. AutoOsd) then consider using <see cref="AnalyseLayout"/> instead
        /// as this also provides a deskew angle which isn't available when just performing orientation detection.
        /// </remarks>
        /// <param name="orientation">The detected clockwise page rotation in degrees (0, 90, 180, or 270).</param>
        /// <param name="confidence">The confidence level of the orientation (15 is reasonably confident).</param>
        public void DetectBestOrientation(out int orientation, out float confidence)
        { DetectBestOrientationAndScript(out orientation, out confidence, out _, out _); }

        /// <summary>
        ///     Detects the page orientation, with corresponding confidence when using <see cref="PageSegMode.OsdOnly" />.
        /// </summary>
        /// <remarks>
        ///     If using full page segmentation mode (i.e. AutoOsd) then consider using <see cref="AnalyseLayout" /> instead as
        ///     this also provides a
        ///     deskew angle which isn't available when just performing orientation detection.
        /// </remarks>
        /// <param name="orientation">The detected clockwise page rotation in degrees (0, 90, 180, or 270).</param>
        /// <param name="confidence">The confidence level of the orientation (15 is reasonably confident).</param>
        /// <param name="scriptName">
        ///     The name of the script (e.g. Latin)
        ///     <param>
        ///         <param name="scriptConfidence">The confidence level in the script</param>
        public void DetectBestOrientationAndScript(out int orientation, out float confidence, out string scriptName, out float scriptConfidence)
        {
            if(TessApi.Native
                    .TessBaseAPIDetectOrientationScript(
                        Engine.Handle,
                        out int orient_deg,
                        out float orient_conf,
                        out IntPtr script_nameHandle,
                        out float script_conf) !=
                0)
            {
                orientation = orient_deg;
                confidence = orient_conf;
                scriptName = script_nameHandle != IntPtr.Zero ? MarshalHelper.PtrToString(script_nameHandle, Encoding.ASCII) : null;
                scriptConfidence = script_conf;
            } else
            {
                throw new TesseractException("Failed to detect image orientation.");
            }
        }

        /// <summary>
        /// Gets the page's content as an Alto text.
        /// </summary>
        /// <param name="pageNum">The page number (zero based).</param>
        /// <returns>The OCR'd output as an Alto text string.</returns>
        public string GetAltoText(int pageNum)
        {
            Guard.Require(nameof(pageNum), pageNum >= 0, "Page number must be greater than or equal to zero (0).");
            Recognize();
            return TessApi.BaseAPIGetAltoText(Engine.Handle, pageNum);
        }

        /// <summary>
        /// Gets the page's content as a Box text.
        /// </summary>
        /// <param name="pageNum">The page number (zero based).</param>
        /// <returns>The OCR'd output as a Box text string.</returns>
        public string GetBoxText(int pageNum)
        {
            Guard.Require(nameof(pageNum), pageNum >= 0, "Page number must be greater than or equal to zero (0).");
            Recognize();
            return TessApi.BaseAPIGetBoxText(Engine.Handle, pageNum);
        }

        /// <summary>
        /// Gets the page's content as an HOCR text.
        /// </summary>
        /// <param name="pageNum">The page number (zero based).</param>
        /// <param name="useXHtml">True to use XHTML Output, False to HTML Output</param>
        /// <returns>The OCR'd output as an HOCR text string.</returns>
        public string GetHOCRText(int pageNum, bool useXHtml = false)
        {
            Guard.Require(nameof(pageNum), pageNum >= 0, "Page number must be greater than or equal to zero (0).");
            Recognize();
            return useXHtml ? TessApi.BaseAPIGetHOCRText2(Engine.Handle, pageNum) : TessApi.BaseAPIGetHOCRText(Engine.Handle, pageNum);
        }

        /// <summary>
        /// Creates a <see cref="ResultIterator"/> object that is used to iterate over the page as defined by the
        /// current <see cref="RegionOfInterest"/>.
        /// </summary>
        /// <returns></returns>
        public ResultIterator GetIterator()
        {
            Recognize();
            IntPtr resultIteratorHandle = TessApi.Native.BaseApiGetIterator(Engine.Handle);
            return new ResultIterator(this, resultIteratorHandle);
        }

        /// <summary>
        /// Gets the page's content as a LSTMBox text.
        /// </summary>
        /// <param name="pageNum">The page number (zero based).</param>
        /// <returns>The OCR'd output as a LSTMBox text string.</returns>
        public string GetLSTMBoxText(int pageNum)
        {
            Guard.Require(nameof(pageNum), pageNum >= 0, "Page number must be greater than or equal to zero (0).");
            Recognize();
            return TessApi.BaseAPIGetLSTMBoxText(Engine.Handle, pageNum);
        }

        /// <summary>
        /// Get's the mean confidence that as a percentage of the recognized text.
        /// </summary>
        /// <returns></returns>
        public float GetMeanConfidence()
        {
            Recognize();
            return TessApi.Native.BaseAPIMeanTextConf(Engine.Handle) / 100.0f;
        }

        /// <summary>
        /// Get segmented regions at specified page iterator level.
        /// </summary>
        /// <param name="pageIteratorLevel">PageIteratorLevel enum</param>
        /// <returns></returns>
        public List<Rectangle> GetSegmentedRegions(PageIteratorLevel pageIteratorLevel)
        {
            IntPtr boxArray = TessApi.Native.BaseAPIGetComponentImages(Engine.Handle, pageIteratorLevel, Constants.TRUE, IntPtr.Zero, IntPtr.Zero);
            int boxCount = LeptonicaApi.Native.boxaGetCount(new HandleRef(this, boxArray));

            List<Rectangle> boxList = new List<Rectangle>();

            for(int i = 0; i < boxCount; i++)
            {
                IntPtr box = LeptonicaApi.Native.boxaGetBox(new HandleRef(this, boxArray), i, PixArrayAccessType.Clone);
                if(box == IntPtr.Zero)
                {
                    continue;
                }

                _ = LeptonicaApi.Native.boxGetGeometry(new HandleRef(this, box), out int px, out int py, out int pw, out int ph);
                boxList.Add(new Rectangle(px, py, pw, ph));
                LeptonicaApi.Native.boxDestroy(ref box);
            }

            LeptonicaApi.Native.boxaDestroy(ref boxArray);

            return boxList;
        }

        /// <summary>
        /// Gets the page's content as plain text.
        /// </summary>
        /// <returns></returns>
        public string GetText()
        {
            Recognize();
            return TessApi.BaseAPIGetUTF8Text(Engine.Handle);
        }

        /// <summary>
        /// Gets the thresholded image that was OCR'd.
        /// </summary>
        /// <returns></returns>
        public Pix GetThresholdedImage()
        {
            Recognize();

            IntPtr pixHandle = TessApi.Native.BaseAPIGetThresholdedImage(Engine.Handle);
            return pixHandle == IntPtr.Zero ? throw new TesseractException("Failed to get thresholded image.") : Pix.Create(pixHandle);
        }

        /// <summary>
        /// Gets the page's content as a Tsv text.
        /// </summary>
        /// <param name="pageNum">The page number (zero based).</param>
        /// <returns>The OCR'd output as a Tsv text string.</returns>
        public string GetTsvText(int pageNum)
        {
            Guard.Require(nameof(pageNum), pageNum >= 0, "Page number must be greater than or equal to zero (0).");
            Recognize();
            return TessApi.BaseAPIGetTsvText(Engine.Handle, pageNum);
        }

        /// <summary>
        /// Gets the page's content as an UNLV text.
        /// </summary>
        /// <param name="pageNum">The page number (zero based).</param>
        /// <returns>The OCR'd output as an UNLV text string.</returns>
        public string GetUNLVText()
        {
            Recognize();
            return TessApi.BaseAPIGetUNLVText(Engine.Handle);
        }

        /// <summary>
        /// Gets the page's content as a WordStrBox text.
        /// </summary>
        /// <param name="pageNum">The page number (zero based).</param>
        /// <returns>The OCR'd output as a WordStrBox text string.</returns>
        public string GetWordStrBoxText(int pageNum)
        {
            Guard.Require(nameof(pageNum), pageNum >= 0, "Page number must be greater than or equal to zero (0).");
            Recognize();
            return TessApi.BaseAPIGetWordStrBoxText(Engine.Handle, pageNum);
        }

        internal Page(TesseractEngine engine, Pix image, string imageName, Rect regionOfInterest, PageSegMode pageSegmentMode)
        {
            Engine = engine;
            Image = image;
            ImageName = imageName;
            RegionOfInterest = regionOfInterest;
            PageSegmentMode = pageSegmentMode;
        }

        internal void Recognize()
        {
            Guard.Verify(
                PageSegmentMode != PageSegMode.OsdOnly,
                "Cannot OCR image when using OSD only page segmentation, please use DetectBestOrientation instead.");
            if(!runRecognitionPhase)
            {
                if(TessApi.Native.BaseApiRecognize(Engine.Handle, new HandleRef(this, IntPtr.Zero)) != 0)
                {
                    throw new InvalidOperationException("Recognition of image failed.");
                }

                runRecognitionPhase = true;

                if(Engine.TryGetBoolVariable("tessedit_write_images", out bool tesseditWriteImages) && tesseditWriteImages)
                {
                    using(Pix thresholdedImage = GetThresholdedImage())
                    {
                        string filePath = Path.Combine(Environment.CurrentDirectory, "tessinput.tif");
                        try
                        {
                            thresholdedImage.Save(filePath, ImageFormat.TiffG4);
                            trace.TraceEvent(TraceEventType.Information, 2, "Successfully saved the thresholded image to '{0}'", filePath);
                        } catch(Exception error)
                        {
                            trace.TraceEvent(TraceEventType.Error, 2, "Failed to save the thresholded image to '{0}'.\nError: {1}", filePath, error.Message);
                        }
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                TessApi.Native.BaseAPIClear(Engine.Handle);
            }
        }

        private static readonly TraceSource trace = new TraceSource("Tesseract");

        private Rect regionOfInterest;

        private bool runRecognitionPhase;
    }
}