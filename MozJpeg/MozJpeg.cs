using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;

namespace MozJpeg
{
    /// <summary>
    /// JPEG colorspaces
    /// </summary>
    public enum TJColorSpaces
    {
        /// <summary>
        /// RGB colorspace.  When compressing the JPEG image, the R, G, and B components in the source image are
        /// reordered into image planes, but no colorspace conversion or subsampling is performed.  RGB JPEG images can
        /// be decompressed to any of the extended RGB pixel formats or grayscale, but they cannot be decompressed to
        /// YUV images.
        /// </summary>
        TJCS_RGB = 0,

        /// <summary>
        /// YCbCr colorspace.  YCbCr is not an absolute colorspace but rather a mathematical transformation of RGB
        /// designed solely for storage and transmission.  YCbCr images must be converted to RGB before they can
        /// actually be displayed.  In the YCbCr colorspace, the Y (luminance) component represents the black-and-white
        /// portion of the original image, and the Cb and Cr (chrominance) components represent the color portion of the
        /// original image.  Originally, the analog equivalent of this transformation allowed the same signal to drive
        /// both black-and-white and color televisions, but JPEG images use YCbCr primarily because it allows the color
        /// data to be optionally subsampled for the purposes of reducing bandwidth or disk space.  YCbCr is the most
        /// common JPEG colorspace, and YCbCr JPEG images can be compressed from and decompressed to any of the extended
        /// RGB pixel formats or grayscale, or they can be decompressed to YUV planar images.
        /// </summary>
        TJCS_YCbCr,

        /// <summary>
        /// Grayscale colorspace.  The JPEG image retains only the luminance data (Y component), and any color data from
        /// the source image is discarded. Grayscale JPEG images can be compressed from and decompressed to any of the
        /// extended RGB pixel formats or grayscale, or they can be decompressed to YUV planar images.
        /// </summary>
        TJCS_GRAY,

        /// <summary>
        /// CMYK colorspace.  When compressing the JPEG image, the C, M, Y, and K components in the source image are
        /// reordered into image planes, but no colorspace conversion or subsampling is performed.  CMYK JPEG images can
        /// only be decompressed to CMYK pixels.
        /// </summary>
        TJCS_CMYK,

        /// <summary>
        /// YCCK colorspace.  YCCK (AKA "YCbCrK") is not an absolute colorspace but rather a mathematical transformation
        /// of CMYK designed solely for storage and transmission.  It is to CMYK as YCbCr is to RGB.  CMYK pixels can be
        /// reversibly transformed into YCCK, and as with YCbCr, the chrominance components in the YCCK pixels can be
        /// subsampled without incurring major perceptual loss.  YCCK JPEG images can only be compressed from and
        /// decompressed to CMYK pixels.
        /// </summary>
        TJCS_YCCK
    }

    /// <summary>
    /// Flags for turbo jpeg
    /// </summary>
    [Flags]
    public enum TJFlags
    {
        /// <summary>
        /// Flags not set
        /// </summary>
        NONE = 0,

        /// <summary>
        /// The uncompressed source/destination image is stored in bottom-up (Windows, OpenGL) order, not top-down (X11)
        /// order.
        /// </summary>
        BOTTOMUP = 2,

        /// <summary>
        /// Use arithmetic coding
        /// </summary>
        ARITHMETIC = 8,

        /// <summary>
        /// Optimize between one scan for all components and one scan for 1st component
        /// </summary>
        DC_SCAN_OPT2 = 16,

        /// <summary>
        /// Use predefined quantization table tuned for MS-SSIM
        /// </summary>
        TUNE_MS_SSIM = 32,

        /// <summary>
        /// Create baseline JPEG file (disable progressive coding)
        /// </summary>
        BASELINE = 128,

        /// <summary>
        /// When decompressing an image that was compressed using chrominance subsampling, use the fastest chrominance
        /// upsampling algorithm available in the underlying codec. The default is to use smooth upsampling, which
        /// creates a smooth transition between neighboring chrominance components in order to reduce upsampling
        /// artifacts in the decompressed image.
        /// </summary>
        FASTUPSAMPLE = 256,

        /// <summary>
        /// Disable buffer (re)allocation.  If passed to <see cref="TurboJpegImport.tjCompress2"/> or #tjTransform(),
        /// this flag will cause those functions to generate an error if the JPEG image buffer is invalid or too small
        /// rather than attempting to allocate or reallocate that buffer. This reproduces the behavior of earlier
        /// versions of TurboJPEG.
        /// </summary>
        NOREALLOC = 1024,

        /// <summary>
        /// Use the fastest DCT/IDCT algorithm available in the underlying codec.  The default if this flag is not
        /// specified is implementation-specific.  For example, the implementation of TurboJPEG for libjpeg[-turbo] uses
        /// the fast algorithm by default when compressing, because this has been shown to have only a very slight
        /// effect on accuracy, but it uses the accurate algorithm when decompressing, because this has been shown to
        /// have a larger effect.
        /// </summary>
        FASTDCT = 2048,

        /// <summary>
        /// Use the most accurate DCT/IDCT algorithm available in the underlying codec. The default if this flag is not
        /// specified is implementation-specific.  For example, the implementation of TurboJPEG for libjpeg[-turbo] uses
        /// the fast algorithm by default when compressing, because this has been shown to have only a very slight
        /// effect on accuracy, but it uses the accurate algorithm when decompressing, because this has been shown to
        /// have a larger effect.
        /// </summary>
        ACCURATEDCT = 4096
    }

    /// <summary>
    /// Pixel formats
    /// </summary>
    public enum TJPixelFormats
    {
        /// <summary>
        /// RGB pixel format.  The red, green, and blue components in the image are stored in 3-byte pixels in the order
        /// R, G, B from lowest to highest byte address within each pixel.
        /// </summary>
        TJPF_RGB = 0,

        /// <summary>
        /// BGR pixel format.  The red, green, and blue components in the image are stored in 3-byte pixels in the order
        /// B, G, R from lowest to highest byte address within each pixel.
        /// </summary>
        TJPF_BGR,

        /// <summary>
        /// RGBX pixel format.  The red, green, and blue components in the image are stored in 4-byte pixels in the
        /// order R, G, B from lowest to highest byte address within each pixel.  The X component is ignored when
        /// compressing and undefined when decompressing.
        /// </summary>
        TJPF_RGBX,

        /// <summary>
        /// BGRX pixel format.  The red, green, and blue components in the image are stored in 4-byte pixels in the
        /// order B, G, R from lowest to highest byte address within each pixel.  The X component is ignored when
        /// compressing and undefined when decompressing.
        /// </summary>
        TJPF_BGRX,

        /// <summary>
        /// XBGR pixel format.  The red, green, and blue components in the image are stored in 4-byte pixels in the
        /// order R, G, B from highest to lowest byte address within each pixel.  The X component is ignored when
        /// compressing and undefined when decompressing.
        /// </summary>
        TJPF_XBGR,

        /// <summary>
        /// XRGB pixel format.  The red, green, and blue components in the image are stored in 4-byte pixels in the
        /// order B, G, R from highest to lowest byte address within each pixel.  The X component is ignored when
        /// compressing and undefined when decompressing.
        /// </summary>
        TJPF_XRGB,

        /// <summary>
        /// Grayscale pixel format.  Each 1-byte pixel represents a luminance (brightness) level from 0 to 255.
        /// </summary>
        TJPF_GRAY,

        /// <summary>
        /// RGBA pixel format.  This is the same as <see cref="TJPF_RGBX"/>, except that when decompressing, the X
        /// component is guaranteed to be 0xFF, which can be interpreted as an opaque alpha channel.
        /// </summary>
        TJPF_RGBA,

        /// <summary>
        /// BGRA pixel format.  This is the same as <see cref="TJPF_BGRX"/>, except that when decompressing, the X
        /// component is guaranteed to be 0xFF, which can be interpreted as an opaque alpha channel.
        /// </summary>
        TJPF_BGRA,

        /// <summary>
        /// ABGR pixel format.  This is the same as <see cref="TJPF_XBGR"/>, except that when decompressing, the X
        /// component is guaranteed to be 0xFF, which can be interpreted as an opaque alpha channel.
        /// </summary>
        TJPF_ABGR,

        /// <summary>
        /// ARGB pixel format.  This is the same as <see cref="TJPF_XRGB"/>, except that when decompressing, the X
        /// component is guaranteed to be 0xFF, which can be interpreted as an opaque alpha channel.
        /// </summary>
        TJPF_ARGB,

        /// <summary>
        /// CMYK pixel format.  Unlike RGB, which is an additive color model used primarily for display, CMYK
        /// (Cyan/Magenta/Yellow/Key) is a subtractive color model used primarily for printing.  In the CMYK color
        /// model, the value of each color component typically corresponds to an amount of cyan, magenta, yellow, or
        /// black ink that is applied to a white background.  In order to convert between CMYK and RGB, it is necessary
        /// to use a color management system (CMS.)  A CMS will attempt to map colors within the printer's gamut to
        /// perceptually similar colors in the display's gamut and vice versa, but the mapping is typically not 1:1 or
        /// reversible, nor can it be defined with a simple formula.  Thus, such a conversion is out of scope for a
        /// codec library.  However, the TurboJPEG API allows for compressing CMYK pixels into a YCCK JPEG image (see
        /// #TJCS_YCCK) and decompressing YCCK JPEG images into CMYK pixels.
        /// </summary>
        TJPF_CMYK
    }

    /// <summary>
    /// Chrominance subsampling options. <para>When pixels are converted from RGB to YCbCr (see #TJCS_YCbCr) or from
    /// CMYK to YCCK (see #TJCS_YCCK) as part of the JPEG compression process, some of the Cb and Cr (chrominance)
    /// components can be discarded or averaged together to produce a smaller image with little perceptible loss of
    /// image clarity (the human eye is more sensitive to small changes in brightness than to small changes in color.)
    /// This is called "chrominance subsampling".</para>
    /// </summary>
    public enum TJSubsamplingOptions
    {
        /// <summary>
        /// 4:4:4 chrominance subsampling (no chrominance subsampling). The JPEG or * YUV image will contain one
        /// chrominance component for every pixel in the source image.
        /// </summary>
        TJSAMP_444 = 0,

        /// <summary>
        /// 4:2:2 chrominance subsampling. The JPEG or YUV image will contain one chrominance component for every 2x1
        /// block of pixels in the source image.
        /// </summary>
        TJSAMP_422,

        /// <summary>
        /// 4:2:0 chrominance subsampling. The JPEG or YUV image will contain one chrominance component for every 2x2
        /// block of pixels in the source image.
        /// </summary>
        TJSAMP_420,

        /// <summary>
        /// Grayscale.  The JPEG or YUV image will contain no chrominance components.
        /// </summary>
        TJSAMP_GRAY,

        /// <summary>
        /// 4:4:0 chrominance subsampling.  The JPEG or YUV image will contain one chrominance component for every 1x2
        /// block of pixels in the source image.
        /// </summary>
        /// <remarks>4:4:0 subsampling is not fully accelerated in libjpeg-turbo.</remarks>
        TJSAMP_440,

        /// <summary>
        /// 4:1:1 chrominance subsampling.  The JPEG or YUV image will contain one chrominance component for every 4x1
        /// block of pixels in the source image. JPEG images compressed with 4:1:1 subsampling will be almost exactly
        /// the same size as those compressed with 4:2:0 subsampling, and in the aggregate, both subsampling methods
        /// produce approximately the same perceptual quality.  However, 4:1:1 is better able to reproduce sharp
        /// horizontal features.
        /// </summary>
        /// <remarks>4:1:1 subsampling is not fully accelerated in libjpeg-turbo.</remarks>
        TJSAMP_411
    }

    public class MozJpeg : IDisposable
    {
        private readonly object _lock = new object();
        private IntPtr _compressorHandle = IntPtr.Zero;
        private IntPtr _decompressHandle = IntPtr.Zero;
        private bool _isDisposed;

        public static bool MozJpegDllExists { get; } = Environment.Is64BitProcess ? File.Exists("turbojpeg_x64.dll") : File.Exists("turbojpeg_x86.dll");

        #region | Destruction |

        /// <summary>
        /// Releases resources
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            lock (_lock)
            {
                if (_isDisposed)
                {
                    return;
                }

                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~MozJpeg() { Dispose(false); }

        private void Dispose(bool callFromUserCode)
        {
            if (callFromUserCode)
            {
                _isDisposed = true;
            }

            if (_decompressHandle != IntPtr.Zero)
            {
                _ = UnsafeNativeMethods.TjDestroy(_decompressHandle);
            }

            if (_compressorHandle != IntPtr.Zero)
            {
                _ = UnsafeNativeMethods.TjDestroy(_compressorHandle);
            }
        }
        #endregion | Destruction |

        #region | Public Decompress Functions |

        /// <summary>
        /// Decompress a JPEG image to an 24bppRgb Bitmap.
        /// </summary>
        /// <param name="pathFileName">Full path and filename of JPEG file</param>
        /// <param name="flags">The bitwise OR of one or more of the <see cref="TJFlags"/> "flags"</param>
        /// <returns>Bitmap with image</returns>
        public Bitmap Decode(byte[] rawJpeg, TJFlags flags = TJFlags.NONE)
        {
            Bitmap bmp = null;
            BitmapData bmpData = null;
            GCHandle pinnedRawJpeg = GCHandle.Alloc(rawJpeg, GCHandleType.Pinned);

            if (_decompressHandle == IntPtr.Zero)
            {
                _decompressHandle = UnsafeNativeMethods.TjInitDecompress();
                if (_decompressHandle == IntPtr.Zero)
                {
                    throw new Exception("Can`t load dll");
                }
            }

            try
            {
                IntPtr rawJpegPtr = pinnedRawJpeg.AddrOfPinnedObject();

                if (UnsafeNativeMethods.TjDecompressHeader(_decompressHandle, rawJpegPtr, (ulong)rawJpeg.Length, out int width, out int height, out _, out _) == -1)
                {
                    throw new Exception("Can`t decode JPEG. Bad o unknow format.");
                }

                bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

                if (UnsafeNativeMethods.TjDecompress(_decompressHandle, rawJpegPtr, (ulong)rawJpeg.Length, bmpData.Scan0, width, bmpData.Stride, height, (int)TJPixelFormats.TJPF_BGR, (int)flags) == -1)
                {
                    throw new Exception("Can`t decode JPEG. Bad o unknow format.");
                }

                GetPixelsPerInch(ref rawJpeg, out float horizontalResolution, out float verticalResolution);
                bmp.SetResolution(horizontalResolution, verticalResolution);

                return bmp;
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message}\r\nIn MozJpeg.Decode");
            }
            finally
            {
                if (bmpData != null)
                {
                    bmp.UnlockBits(bmpData);
                }

                if (pinnedRawJpeg.IsAllocated)
                {
                    pinnedRawJpeg.Free();
                }
            }
        }

        /// <summary>
        /// Decompress a JPEG image to an 24bppRgb Bitmap.
        /// </summary>
        /// <param name="pathFileName">Full path and filename of JPEG file</param>
        /// <param name="flags">The bitwise OR of one or more of the <see cref="TJFlags"/> "flags"</param>
        /// <returns>Bitmap with image</returns>
        public void GetInfo(
            byte[] rawJpeg,
            out int width,
            out int height,
            out float horizontalResolution,
            out float verticalResolution,
            out TJSubsamplingOptions subsampl,
            out TJColorSpaces colorspace)
        {
            horizontalResolution = 0;
            verticalResolution = 0;
            GCHandle pinnedRawJpeg = GCHandle.Alloc(rawJpeg, GCHandleType.Pinned);

            if (_decompressHandle == IntPtr.Zero)
            {
                _decompressHandle = UnsafeNativeMethods.TjInitDecompress();
                if (_decompressHandle == IntPtr.Zero)
                {
                    throw new Exception("Can`t load dll");
                }
            }

            try
            {
                IntPtr rawJpegPtr = pinnedRawJpeg.AddrOfPinnedObject();

                if (UnsafeNativeMethods.TjDecompressHeader(_decompressHandle, rawJpegPtr, (ulong)rawJpeg.Length, out width, out height, out subsampl, out colorspace) == -1)
                {
                    throw new Exception("Can`t decode JPEG. Bad o unknow format.");
                }

                GetPixelsPerInch(ref rawJpeg, out horizontalResolution, out verticalResolution);
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message}\r\nIn MozJpeg.GetInfo");
            }
            finally
            {
                if (pinnedRawJpeg.IsAllocated)
                {
                    pinnedRawJpeg.Free();
                }
            }
        }

        /// <summary>
        /// Read a JPEG file
        /// </summary>
        /// <param name="pathFileName">JPEG file to load</param>
        /// <returns>Bitmap with the JPEG image</returns>
        public Bitmap Load(string pathFileName)
        {
            byte[] rawJpeg;
            try
            {
                rawJpeg = File.ReadAllBytes(pathFileName);

                return Decode(rawJpeg);
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message}\r\nIn MozJpeg.Load");
            }
        }
        #endregion | Public Decompress Functions |

        #region | Public Compress Functions |

        /// <summary>
        /// Compresses input image to the jpeg format with specified quality
        /// </summary>
        /// <param name="bmp">Source image to be converted</param>
        /// <param name="quality">The image quality of the generated JPEG image (1 = worst, 100 = best)</param>
        /// <param name="jfif">Do not put the JFIF field</param>
        /// <param name="flags">The bitwise OR of one or more of the <see cref="TJFlags"/> "flags"</param>
        /// <param name="subSamp">
        /// The level of chrominance subsampling to be used when generating the JPEG image (see <see
        /// cref="TJSubsamplingOptions"/> "Chrominance subsampling options".)
        /// </param>
        /// <returns>Byte array with the jpeg data</returns>
        public byte[] Encode(Bitmap bmp, int quality = 75, bool jfif = true, TJFlags flags = TJFlags.NONE, TJSubsamplingOptions subSamp = TJSubsamplingOptions.TJSAMP_420)
        {
            BitmapData bmpData = null;
            IntPtr buf = IntPtr.Zero;

            try
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException("this");
                }

                if (_compressorHandle == IntPtr.Zero)
                {
                    _compressorHandle = UnsafeNativeMethods.TjInitCompress();
                    if (_compressorHandle == IntPtr.Zero)
                    {
                        throw new Exception("Can`t load dll");
                    }
                }

                TJPixelFormats tjPixelFormat = ConvertPixelFormat(bmp.PixelFormat);
                if (tjPixelFormat == TJPixelFormats.TJPF_GRAY && subSamp != TJSubsamplingOptions.TJSAMP_GRAY)
                {
                    throw new NotSupportedException("Subsampling differ from {TJSubsamplingOptions.TJSAMP_GRAY} for pixel format {TJPixelFormats.TJPF_GRAY} is not supported");
                }

                bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, bmp.PixelFormat);

                ulong bufSize = 0;
                if (UnsafeNativeMethods.TjCompress2(
                        _compressorHandle,
                        bmpData.Scan0,
                        bmp.Width,
                        bmpData.Stride,
                        bmp.Height,
                        (int)tjPixelFormat,
                        ref buf,
                        ref bufSize,
                        (int)subSamp,
                        quality,
                        (int)flags) ==
                    -1)
                {
                    throw new Exception("Can`t encode JPEG.");
                }

                byte[] rawJpeg = new byte[bufSize];
                Marshal.Copy(buf, rawJpeg, 0, (int)bufSize);

                if (!jfif && rawJpeg[2] == 0xff && rawJpeg[3] == 0xe0)
                {
                    int jfifLength = (rawJpeg[4] << 8) | rawJpeg[5];
                    byte[] tempData = new byte[rawJpeg.Length - jfifLength - 2];
                    Array.Copy(rawJpeg, jfifLength + 2, tempData, 0, rawJpeg.Length - jfifLength - 2);
                    tempData[0] = 0xff;
                    tempData[1] = 0xd8;
                }
                else
                {
                    if (rawJpeg[2] == 0xff && rawJpeg[3] == 0xe0)
                    {
                        rawJpeg[0x0d] = 0x01;
                        rawJpeg[0x0e] = (byte)(bmp.HorizontalResolution / 256);
                        rawJpeg[0x0f] = (byte)(bmp.HorizontalResolution % 256);
                        rawJpeg[0x10] = (byte)(bmp.VerticalResolution / 256);
                        rawJpeg[0x11] = (byte)(bmp.VerticalResolution % 256);
                    }
                }

                return rawJpeg;
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message}\r\nIn MozJpeg.Encode");
            }
            finally
            {
                bmp.UnlockBits(bmpData);
                UnsafeNativeMethods.TjFree(buf);
            }
        }

        /// <summary>
        /// Save bitmap to file in WebP format
        /// </summary>
        /// <param name="bmp">Bitmap with the WebP image</param>
        /// <param name="pathFileName">The file to write</param>
        /// <param name="quality">Between 0 (lower quality, lowest file size) and 100 (highest quality, higher file size)</param>
        public void Save(Bitmap bmp, string pathFileName, int quality = 75)
        {
            byte[] rawJpeg;

            try
            {
                rawJpeg = Encode(bmp, quality);

                File.WriteAllBytes(pathFileName, rawJpeg);
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message}\r\nIn MozJpeg.Save");
            }
        }
        #endregion | Public Compress Functions |

        #region | Private Functions |

        /// <summary>
        /// Converts pixel format from <see cref="PixelFormat"/> to <see cref="TJPixelFormats"/>
        /// </summary>
        /// <param name="pixelFormat">Pixel format to convert</param>
        /// <returns>Converted value of pixel format or exception if convertion is impossible</returns>
        /// <exception cref="NotSupportedException">Convertion can not be performed</exception>
        private TJPixelFormats ConvertPixelFormat(PixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case PixelFormat.Format32bppArgb:
                    return TJPixelFormats.TJPF_BGRA;

                case PixelFormat.Format24bppRgb:
                    return TJPixelFormats.TJPF_BGR;

                case PixelFormat.Format16bppGrayScale:
                    return TJPixelFormats.TJPF_GRAY;

                default:
                    throw new NotSupportedException("Provided pixel format \"{pixelFormat}\" is not supported");
            }
        }

        private void GetPixelsPerInch(ref byte[] rawJpeg, out float horizontalResolution, out float verticalResolution)
        {
            try
            {
                horizontalResolution = 96;
                verticalResolution = 96;

                int[] jfif = ClsArray.Locate(ref rawJpeg, new byte[] { 0x4a, 0x46, 0x49, 0x46, 0x00 }).ToArray();
                if (jfif.Length == 1)
                {
                    switch (rawJpeg[jfif[0] + 7])
                    {
                        case 0x01:
                            horizontalResolution = (rawJpeg[jfif[0] + 8] * 256) + rawJpeg[jfif[0] + 9];
                            verticalResolution = (rawJpeg[jfif[0] + 10] * 256) + rawJpeg[jfif[0] + 11];
                            break;

                        case 0x02:
                            horizontalResolution = ((rawJpeg[jfif[0] + 8] * 256) + rawJpeg[jfif[0] + 9]) * 2.54F;
                            verticalResolution = ((rawJpeg[jfif[0] + 10] * 256) + rawJpeg[jfif[0] + 11]) * 2.54F;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message}\r\nIn MozJpeg.GetPixelsPerInch");
            }
        }
        #endregion | Private Functions |
    }

    internal static class ClsArray
    {
        public static List<int> Locate(ref byte[] self, byte[] candidate)
        {
            try
            {
                List<int> list = new List<int>();

                if (IsEmptyLocate(self, candidate))
                {
                    return list;
                }

                for (int i = 0; i < self.Length; i++)
                {
                    if (!IsMatch(self, i, candidate))
                    {
                        continue;
                    }

                    list.Add(i);
                }

                return list;
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message}\r\nIn clsArray.Locate");
            }
        }

        private static bool IsEmptyLocate(byte[] array, byte[] candidate)
        {
            try
            {
                return array == null || candidate == null || array.Length == 0 || candidate.Length == 0 || candidate.Length > array.Length;
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message}\r\nIn clsArray.IsEmptyLocate");
            }
        }

        private static bool IsMatch(byte[] array, int position, byte[] candidate)
        {
            try
            {
                if (candidate.Length > array.Length - position)
                {
                    return false;
                }

                for (int i = 0; i < candidate.Length; i++)
                {
                    if (array[position + i] != candidate[i])
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message}\r\nIn clsArray.IsMatch");
            }
        }
    }

    [SuppressUnmanagedCodeSecurity]
    internal sealed class UnsafeNativeMethods
    {
        /// <summary>
        /// Allocate an image buffer for use with TurboJPEG.  You should always use this function to allocate the JPEG
        /// destination buffer(s) for <see cref="TjCompress2"/> and <see cref="tjTransform"/> unless you are disabling
        /// automatic buffer (re)allocation (by setting <see cref="TJFlags.NOREALLOC"/>.)
        /// </summary>
        /// <param name="bytes">The number of bytes to allocate</param>
        /// <returns>A pointer to a newly-allocated buffer with the specified number of bytes</returns>
        /// <seealso cref="TjFree"/>
        public static IntPtr TjAlloc(int bytes)
        {
            switch (IntPtr.Size)
            {
                case 4:
                    return tjAlloc_x86(bytes);

                case 8:
                    return tjAlloc_x64(bytes);

                default:
                    throw new InvalidOperationException("Invalid platform. Can not find proper function");
            }
        }

        [DllImport("turbojpeg_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjAlloc")]
        public static extern IntPtr tjAlloc_x64(int bytes);

        [DllImport("turbojpeg_x86.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjAlloc")]
        public static extern IntPtr tjAlloc_x86(int bytes);

        /// <summary>
        /// Compress an RGB, grayscale, or CMYK image into a JPEG image.
        /// </summary>
        /// <param name="handle">A handle to a TurboJPEG compressor or transformer instance</param>
        /// <param name="srcBuf">
        /// Pointer to an image buffer containing RGB, grayscale, or CMYK pixels to be compressed. This buffer is not
        /// modified.
        /// </param>
        /// <param name="width">Width (in pixels) of the source image</param>
        /// <param name="stride">Bytes per line in the source image.</param>
        /// <param name="height">Height (in pixels) of the source image</param>
        /// <param name="pixelFormat">Pixel format of the source image (see <see cref="TJPixelFormats"/> "Pixel formats")</param>
        /// <param name="jpegBuf">
        /// Address of a pointer to an image buffer that will receive the JPEG image. TurboJPEG has the ability to
        /// reallocate the JPEG buffer to accommodate the size of the JPEG image.  Thus, you can choose to: <list
        /// type="number"><item><description>pre-allocate the JPEG buffer with an arbitrary size using<see
        /// cref="TjAlloc"/> and let TurboJPEG grow the buffer as needed</description></item><item><description>
        /// set<paramref name="jpegBuf"/> to NULL to tell TurboJPEG to allocate the buffer for
        /// you</description></item><item><description>pre-allocate the buffer to a "worst case" size determined by
        /// calling <see cref="tjBufSize"/>. This should ensure that the buffer never has to be re- allocated
        /// (setting<see cref="TJFlags.NOREALLOC"/> guarantees this.).</description></item></list> If you choose option
        /// 1, <paramref name="jpegSize"/> should be set to the size of your pre-allocated buffer. In any case, unless
        /// you have set <see cref="TJFlags.NOREALLOC"/>, you should always check <paramref name="jpegBuf"/> upon return
        /// from this function, as it may have changed.
        /// </param>
        /// <param name="jpegSize">
        /// Pointer to an unsigned long variable that holds the size of the JPEG image buffer. If <paramref
        /// name="jpegBuf"/> points to a pre-allocated buffer, then <paramref name="jpegSize"/> should be set to the
        /// size of the buffer. Upon return, <paramref name="jpegSize"/> will contain the size of the JPEG image (in
        /// bytes.) If <paramref name="jpegBuf"/> points to a JPEG image buffer that is being reused from a previous
        /// call to one of the JPEG compression functions, then <paramref name="jpegSize"/> is ignored.
        /// </param>
        /// <param name="jpegSubsamp">
        /// The level of chrominance subsampling to be used when generating the JPEG image (see <see
        /// cref="TJSubsamplingOptions"/> "Chrominance subsampling options".)
        /// </param>
        /// <param name="jpegQual">The image quality of the generated JPEG image (1 = worst, 100 = best)</param>
        /// <param name="flags">The bitwise OR of one or more of the <see cref="TJFlags"/> "flags"</param>
        /// <returns>0 if successful, or -1 if an error occurred (see <see cref="tjGetErrorStr"/>)</returns>
        public static int TjCompress2(
            IntPtr handle,
            IntPtr srcBuf,
            int width,
            int stride,
            int height,
            int pixelFormat,
            ref IntPtr jpegBuf,
            ref ulong jpegSize,
            int jpegSubsamp,
            int jpegQual,
            int flags)
        {
            switch (IntPtr.Size)
            {
                case 4:
                    return tjCompress2_x86(handle, srcBuf, width, stride, height, pixelFormat, ref jpegBuf, ref jpegSize, jpegSubsamp, jpegQual, flags);

                case 8:
                    return tjCompress2_x64(handle, srcBuf, width, stride, height, pixelFormat, ref jpegBuf, ref jpegSize, jpegSubsamp, jpegQual, flags);

                default:
                    throw new InvalidOperationException("Invalid platform. Can not find proper function");
            }
        }

        /// <summary>
        /// Decompress a JPEG image to an RGB, grayscale, or CMYK image.
        /// </summary>
        /// <param name="handle">A handle to a TurboJPEG decompressor or transformer instance</param>
        /// <param name="jpegBuf">Pointer to a buffer containing the JPEG image to decompress. This buffer is not modified.</param>
        /// <param name="jpegSize">Size of the JPEG image (in bytes)</param>
        /// <param name="dstBuf">
        /// Pointer to an image buffer that will receive the decompressed image. This buffer should normally be <c>
        /// pitch * scaledHeight</c> bytes in size, where <c>scaledHeight</c> can be determined by calling <see
        /// cref="TJSCALED"/> with the JPEG image height and one of the scaling factors returned by <see
        /// cref="tjGetScalingFactors"/> . The <paramref name="dstBuf"/> pointer may also be used to decompress into a
        /// specific region of a larger buffer.
        /// </param>
        /// <param name="width">
        /// Desired width (in pixels) of the destination image. If this is different than the width of the JPEG image
        /// being decompressed, then TurboJPEG will use scaling in the JPEG decompressor to generate the largest
        /// possible image that will fit within the desired width. If <paramref name="width"/> is set to 0, then only
        /// the height will be considered when determining the scaled image size.
        /// </param>
        /// <param name="stride">
        /// Bytes per line in the destination image.  Normally, this is <c>scaledWidth* tjPixelSize[pixelFormat]</c> if
        /// the decompressed image is unpadded, else <c>TJPAD(scaledWidth * tjPixelSize[pixelFormat])</c> if each line
        /// of the decompressed image is padded to the nearest 32-bit boundary, as is the case for Windows bitmaps.
        /// <remarks>Note: <c>scaledWidth</c> can be determined by calling <see cref="TJSCALED"/> with the JPEG image
        /// width and one of the scaling factors returned by <see cref="tjGetScalingFactors"/></remarks> You can also be
        /// clever and use the pitch parameter to skip lines, etc. Setting this parameter to 0 is the equivalent of
        /// setting it to <c>scaledWidth* tjPixelSize[pixelFormat]</c>.
        /// </param>
        /// <param name="height">
        /// Desired height (in pixels) of the destination image. If this is different than the height of the JPEG image
        /// being decompressed, then TurboJPEG will use scaling in the JPEG decompressor to generate the largest
        /// possible image that will fit within the desired height. If <paramref name="height"/> is set to 0, then only
        /// the width will be considered when determining the scaled image size.
        /// </param>
        /// <param name="pixelFormat">Pixel format of the destination image (see <see cref="TJPixelFormats"/> "Pixel formats".)</param>
        /// <param name="flags">The bitwise OR of one or more of the <see cref="TJFlags"/> "flags"</param>
        /// <returns>0 if successful, or -1 if an error occurred (see <see cref="tjGetErrorStr"/>)</returns>
        public static int TjDecompress(IntPtr handle, IntPtr jpegBuf, ulong jpegSize, IntPtr dstBuf, int width, int stride, int height, int pixelFormat, int flags)
        {
            switch (IntPtr.Size)
            {
                case 4:
                    return tjDecompress2_x86(handle, jpegBuf, (uint)jpegSize, dstBuf, width, stride, height, pixelFormat, flags);

                case 8:
                    return tjDecompress2_x64(handle, jpegBuf, jpegSize, dstBuf, width, stride, height, pixelFormat, flags);

                default:
                    throw new InvalidOperationException("Invalid platform. Can not find proper function");
            }
        }

        /// <summary>
        /// Retrieve information about a JPEG image without decompressing it.
        /// </summary>
        /// <param name="handle">A handle to a TurboJPEG decompressor or transformer instance</param>
        /// <param name="jpegBuf">Pointer to a buffer containing a JPEG image. This buffer is not modified.</param>
        /// <param name="jpegSize">Size of the JPEG image (in bytes)</param>
        /// <param name="width">Pointer to an integer variable that will receive the width (in pixels) of the JPEG image</param>
        /// <param name="height">Pointer to an integer variable that will receive the height (in pixels) of the JPEG image</param>
        /// <param name="jpegSubsamp">
        /// Pointer to an integer variable that will receive the level of chrominance subsampling used when the JPEG
        /// image was compressed (see <see cref="TJSubsamplingOptions"/> "Chrominance subsampling options".)
        /// </param>
        /// <param name="jpegColorspace">
        /// Pointer to an integer variable that will receive one of the JPEG colorspace constants, indicating the
        /// colorspace of the JPEG image(see <see cref="TJColorSpaces"/> "JPEG colorspaces".)
        /// </param>
        /// <returns>0 if successful, or -1 if an error occurred (see <see cref="tjGetErrorStr"/>)</returns>
        public static int TjDecompressHeader(IntPtr handle, IntPtr jpegBuf, ulong jpegSize, out int width, out int height, out TJSubsamplingOptions jpegSubsamp, out TJColorSpaces jpegColorspace)
        {
            switch (IntPtr.Size)
            {
                case 4:
                    return tjDecompressHeader3_x86(handle, jpegBuf, (uint)jpegSize, out width, out height, out jpegSubsamp, out jpegColorspace);

                case 8:
                    return tjDecompressHeader3_x64(handle, jpegBuf, jpegSize, out width, out height, out jpegSubsamp, out jpegColorspace);

                default:
                    throw new InvalidOperationException("Invalid platform. Can not find proper function");
            }
        }

        /// <summary>
        /// Decompress a JPEG image into separate Y, U (Cb), and V (Cr) image planes. This function performs JPEG
        /// decompression but leaves out the color conversion step, so a planar YUV image is generated instead of an RGB
        /// image.
        /// </summary>
        /// <param name="handle">A handle to a TurboJPEG decompressor or transformer instance</param>
        /// <param name="jpegBuf">Pointer to a buffer containing the JPEG image to decompress</param>
        /// <param name="jpegSize">Size of the JPEG image (in bytes)</param>
        /// <param name="dstPlanes">
        /// An array of pointers to Y, U (Cb), and V (Cr) image planes (or just a Y plane, if decompressing a grayscale
        /// image) that will receive the YUV image.  These planes can be contiguous or non-contiguous in memory. Use
        /// #tjPlaneSizeYUV() to determine the appropriate size for each plane based on the scaled image width, scaled
        /// image height, strides, and level of chrominance subsampling.
        /// </param>
        /// <param name="width">
        /// Desired width (in pixels) of the YUV image.  If this is different than the width of the JPEG image being
        /// decompressed, then TurboJPEG will use scaling in the JPEG decompressor to generate the largest possible
        /// image that will fit within the desired width. If width is set to 0, then only the height will be considered
        /// when determining the scaled image size.  If the scaled width is not an even multiple of the MCU block width
        /// (see #tjMCUWidth), then an intermediate buffer copy will be performed within TurboJPEG.
        /// </param>
        /// <param name="strides">
        /// stride bytes per line in the image plane. Setting this to 0 is the equivalent of setting it to the plane
        /// width.
        /// </param>
        /// <param name="height">
        /// Desired height (in pixels) of the YUV image.  If this is different than the height of the JPEG image being
        /// decompressed, then TurboJPEG will use scaling in the JPEG decompressor to generate the largest possible
        /// image that will fit within the desired height.  If height is set to 0, then only the width will be
        /// considered when determining the scaled image size.  If the scaled height is not an even multiple of the MCU
        /// block height (see #tjMCUHeight), then an intermediate buffer copy will be performed within TurboJPEG.
        /// </param>
        /// <param name="flags">The bitwise OR of one or more of the TJFLAG_BOTTOMUP "flags"</param>
        /// <returns>0 if successful, or -1 if an error occurred</returns>
        public static int TjDecompressToYUVPlanes(IntPtr handle, IntPtr jpegBuf, ulong jpegSize, IntPtr[] dstPlanes, int width, int[] strides, int height, int flags)
        {
            switch (IntPtr.Size)
            {
                case 4:
                    return tjDecompressToYUVPlanes_x86(handle, jpegBuf, (uint)jpegSize, dstPlanes, width, strides, height, flags);

                case 8:
                    return tjDecompressToYUVPlanes_x64(handle, jpegBuf, jpegSize, dstPlanes, width, strides, height, flags);

                default:
                    throw new InvalidOperationException("Invalid platform. Can not find proper function");
            }
        }

        /// <summary>
        /// Destroy a TurboJPEG compressor, decompressor, or transformer instance
        /// </summary>
        /// <param name="handle">a handle to a TurboJPEG compressor, decompressor or transformer instance</param>
        /// <returns>0 if successful, or -1 if an error occurred (see <see cref="tjGetErrorStr"/>)</returns>
        public static int TjDestroy(IntPtr handle)
        {
            switch (IntPtr.Size)
            {
                case 4:
                    return tjDestroy_x86(handle);

                case 8:
                    return tjDestroy_x64(handle);

                default:
                    throw new InvalidOperationException("Invalid platform. Can not find proper function");
            }
        }

        /// <summary>
        /// Free an image buffer previously allocated by TurboJPEG.  You should always use this function to free JPEG
        /// destination buffer(s) that were automatically (re)allocated by <see cref="TjCompress2"/> or <see
        /// cref="tjTransform"/> or that were manually allocated using <see cref="TjAlloc"/>.
        /// </summary>
        /// <param name="buffer">Address of the buffer to free</param>
        /// <seealso cref="TjAlloc"/>
        public static void TjFree(IntPtr buffer)
        {
            switch (IntPtr.Size)
            {
                case 4:
                    tjFree_x86(buffer);
                    break;

                case 8:
                    tjFree_x64(buffer);
                    break;

                default:
                    throw new InvalidOperationException("Invalid platform. Can not find proper function");
            }
        }

        /// <summary>
        /// Create a TurboJPEG compressor instance.
        /// </summary>
        /// <returns>
        /// handle to the newly-created instance, or <see cref="IntPtr.Zero"/> if an error occurred (see <see
        /// cref="tjGetErrorStr"/> )
        /// </returns>
        public static IntPtr TjInitCompress()
        {
            switch (IntPtr.Size)
            {
                case 4:
                    return tjInitCompress_x86();

                case 8:
                    return tjInitCompress_x64();

                default:
                    throw new InvalidOperationException("Invalid platform. Can not find proper function");
            }
        }

        /// <summary>
        /// Create a TurboJPEG decompressor instance.
        /// </summary>
        /// <returns>A handle to the newly-created instance, or NULL if an error occurred(see <see cref="tjGetErrorStr"/>)</returns>
        public static IntPtr TjInitDecompress()
        {
            switch (IntPtr.Size)
            {
                case 4:
                    return tjInitDecompress_x86();

                case 8:
                    return tjInitDecompress_x64();

                default:
                    throw new InvalidOperationException("Invalid platform. Can not find proper function");
            }
        }

        /// <summary>
        /// The size of the buffer (in bytes) required to hold a YUV image plane with the given parameters.
        /// </summary>
        /// <param name="componentID">ID number of the image plane (0 = Y, 1 = U/Cb, 2 = V/Cr)</param>
        /// <param name="width">
        /// width (in pixels) of the YUV image.  NOTE: this is the width of the whole image, not the plane width.
        /// </param>
        /// <param name="stride">
        /// stride bytes per line in the image plane.  Setting this to 0 is the equivalent of setting it to the plane
        /// width.
        /// </param>
        /// <param name="height">
        /// height (in pixels) of the YUV image.  NOTE: this is the height of the whole image, not the plane height.
        /// </param>
        /// <param name="subsamp">
        /// level of chrominance subsampling in the image (see @ref TJSAMP "Chrominance subsampling options".)
        /// </param>
        /// <returns>
        /// the size of the buffer (in bytes) required to hold the YUV image plane, or -1 if the arguments are out of
        /// bounds.
        /// </returns>
        public static int TjPlaneSizeYUV(int componentID, int width, int stride, int height, TJSubsamplingOptions subsamp)
        {
            switch (IntPtr.Size)
            {
                case 4:
                    return tjPlaneSizeYUV_x86(componentID, width, stride, height, subsamp);

                case 8:
                    return tjPlaneSizeYUV_x64(componentID, width, stride, height, subsamp);

                default:
                    throw new InvalidOperationException("Invalid platform. Can not find proper function");
            }
        }

        [DllImport("turbojpeg_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjCompress2")]
        private static extern int tjCompress2_x64(
            IntPtr handle,
            IntPtr srcBuf,
            int width,
            int pitch,
            int height,
            int pixelFormat,
            ref IntPtr jpegBuf,
            ref ulong jpegSize,
            int jpegSubsamp,
            int jpegQual,
            int flags);

        [DllImport("turbojpeg_x86.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjCompress2")]
        private static extern int tjCompress2_x86(
            IntPtr handle,
            IntPtr srcBuf,
            int width,
            int pitch,
            int height,
            int pixelFormat,
            ref IntPtr jpegBuf,
            ref ulong jpegSize,
            int jpegSubsamp,
            int jpegQual,
            int flags);

        [DllImport("turbojpeg_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjDecompress2")]
        private static extern int tjDecompress2_x64(IntPtr handle, IntPtr jpegBuf, ulong jpegSize, IntPtr dstBuf, int width, int pitch, int height, int pixelFormat, int flags);

        [DllImport("turbojpeg_x86.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjDecompress2")]
        private static extern int tjDecompress2_x86(IntPtr handle, IntPtr jpegBuf, uint jpegSize, IntPtr dstBuf, int width, int pitch, int height, int pixelFormat, int flags);

        [DllImport("turbojpeg_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjDecompressHeader3")]
        private static extern int tjDecompressHeader3_x64(
            IntPtr handle,
            IntPtr jpegBuf,
            ulong jpegSize,
            out int width,
            out int height,
            out TJSubsamplingOptions jpegSubsamp,
            out TJColorSpaces jpegColorspace);

        [DllImport("turbojpeg_x86.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjDecompressHeader3")]
        private static extern int tjDecompressHeader3_x86(
            IntPtr handle,
            IntPtr jpegBuf,
            uint jpegSize,
            out int width,
            out int height,
            out TJSubsamplingOptions jpegSubsamp,
            out TJColorSpaces jpegColorspace);

        [DllImport("turbojpeg_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjDecompressToYUVPlanes")]
        private static extern int tjDecompressToYUVPlanes_x64(IntPtr handle, IntPtr jpegBuf, ulong jpegSize, IntPtr[] dstPlanes, int width, int[] strides, int height, int flags);

        [DllImport("turbojpeg_x86.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjDecompressToYUVPlanes")]
        private static extern int tjDecompressToYUVPlanes_x86(IntPtr handle, IntPtr jpegBuf, uint jpegSize, IntPtr[] dstPlanes, int width, int[] strides, int height, int flags);

        [DllImport("turbojpeg_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjDestroy")]
        private static extern int tjDestroy_x64(IntPtr handle);

        [DllImport("turbojpeg_x86.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjDestroy")]
        private static extern int tjDestroy_x86(IntPtr handle);

        [DllImport("turbojpeg_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjFree")]
        private static extern void tjFree_x64(IntPtr buffer);

        [DllImport("turbojpeg_x86.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjFree")]
        private static extern void tjFree_x86(IntPtr buffer);

        [DllImport("turbojpeg_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjInitCompress")]
        private static extern IntPtr tjInitCompress_x64();

        [DllImport("turbojpeg_x86.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjInitCompress")]
        private static extern IntPtr tjInitCompress_x86();

        [DllImport("turbojpeg_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjInitDecompress")]
        private static extern IntPtr tjInitDecompress_x64();

        [DllImport("turbojpeg_x86.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjInitDecompress")]
        private static extern IntPtr tjInitDecompress_x86();

        [DllImport("turbojpeg_x64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjPlaneSizeYUV")]
        private static extern int tjPlaneSizeYUV_x64(int componentID, int width, int stride, int height, TJSubsamplingOptions subsamp);

        [DllImport("turbojpeg_x86.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjPlaneSizeYUV")]
        private static extern int tjPlaneSizeYUV_x86(int componentID, int width, int stride, int height, TJSubsamplingOptions subsamp);
    }
}