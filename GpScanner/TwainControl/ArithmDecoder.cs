using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static TwainControl.ArithmDecoder;

namespace TwainControl
{
    internal class ArithmDecoder
    {
        internal class CXStats
        {
            internal byte[] cxTab;
            internal CXStats(int contextSizeA = 512)
            {
                cxTab = new byte[contextSizeA];
                reset();
            }
            internal void reset() => Array.Clear(cxTab, 0, cxTab.Length);
            internal void setEntry(uint cx, int i, int mps) => cxTab[cx] = (byte)((i << 1) + mps);
        };
        private static readonly uint[] qeTab = [
            0x56010000, 0x34010000, 0x18010000, 0x0AC10000, 0x05210000, 0x02210000, 0x56010000, 0x54010000,
            0x48010000, 0x38010000, 0x30010000, 0x24010000, 0x1C010000, 0x16010000, 0x56010000, 0x54010000,
            0x51010000, 0x48010000, 0x38010000, 0x34010000, 0x30010000, 0x28010000, 0x24010000, 0x22010000,
            0x1C010000, 0x18010000, 0x16010000, 0x14010000, 0x12010000, 0x11010000, 0x0AC10000, 0x09C10000,
            0x08A10000, 0x05210000, 0x04410000, 0x02A10000, 0x02210000, 0x01410000, 0x01110000, 0x00850000,
            0x00490000, 0x00250000, 0x00150000, 0x00090000, 0x00050000, 0x00010000, 0x56010000
        ];
        private static readonly int[] nmpsTab = [
            1,  2,  3,  4,  5, 38,  7,  8,  9, 10, 11, 12, 13, 29, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24,
            25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 45, 46
        ];
        private static readonly int[] nlpsTab = [
            1,  6,  9, 12, 29, 33,  6, 14, 14, 14, 17, 18, 20, 21, 14, 14, 15, 16, 17, 18, 19, 19, 20, 21,
            22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 46
        ];
        private static readonly int[] switchTab = [
            1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        ];
        internal ArithmDecoder(MemoryStream str = null)
        {
            if (str != null)
            {
                setStream(str);
                start();
            }
        }
        internal void setStream(MemoryStream strA) { str = strA; dataLen = 0; limitStream = false; }
        internal void setStream(MemoryStream strA, int dataLenA) { str = strA; dataLen = dataLenA; limitStream = true; }
        // Start decoding on a new stream.  This fills the byte buffers and runs INITDEC.
        internal void start()
        {
            buf0 = readByte();
            buf1 = readByte();
            c = (buf0 ^ 0xff) << 16;    // INITDEC
            byteIn();
            c <<= 7;
            ct -= 7;
            a = 0x80000000;
        }
        // Restart decoding on an interrupted stream.  This refills the
        // buffers if needed, but does not run INITDEC.  (This is used in
        // JPEG 2000 streams when codeblock data is split across multiple
        // packets/layers.)
        internal void restart(int dataLenA)
        {
            if (dataLen >= 0)
            {
                dataLen = dataLenA;
            }
            else if (dataLen == -1)
            {
                dataLen = dataLenA;
                buf1 = readByte();
            }
            else
            {
                int k = ((-dataLen - 1) * 8) - ct, nBits;
                dataLen = dataLenA;
                uint cAdd = 0;
                bool prevFF = false;
                for (; k > 0;)
                {
                    buf0 = readByte();
                    if (prevFF)
                    {
                        cAdd += 0xfe00 - (buf0 << 9);
                        nBits = 7;
                    }
                    else
                    {
                        cAdd += 0xff00 - (buf0 << 8);
                        nBits = 8;
                    }
                    prevFF = buf0 == 0xff;
                    if (k > nBits)
                    {
                        cAdd <<= nBits;
                        k -= nBits;
                    }
                    else
                    {
                        cAdd <<= k;
                        ct = nBits - k;
                        k = 0;
                    }
                }
                c += cAdd;
                buf1 = readByte();
            }
        }
        // Read any leftover data in the stream.
        internal void cleanup()
        {
            if (!limitStream)
            {
                return;
            }
            // This saves one extra byte of data from the end of packet i, to be used in packet i+1.
            // It's not clear from the JPEG 2000 spec exactly how this should work, but this kludge
            // does seem to fix decode of some problematic JPEG 2000 streams.  It may actually be
            // necessary to buffer an arbitrary number of bytes (> 1), but I haven't run into that case yet.
            for (; dataLen > 0; readBuf = (int)readByte())
            {
                readBuf = -1;
            }
        }
        internal int decodeBit(int context, CXStats stats)
        {
            int iCX = stats.cxTab[context] >> 1, mps = stats.cxTab[context] & 1, bit;
            uint qe = qeTab[iCX];
            a -= qe;
            if (c < a)
            {
                if ((a & 0x80000000) != 0)
                {
                    bit = mps;
                }
                else
                {                          // MPS_EXCHANGE
                    if (a < qe)
                    {
                        bit = 1 - mps;
                        stats.cxTab[context] = (byte)((nlpsTab[iCX] << 1)
                                    | (switchTab[iCX] != 0 ? 1 - mps : mps));
                    }
                    else
                    {
                        bit = mps;
                        stats.cxTab[context] = (byte)((nmpsTab[iCX] << 1) | mps);
                    }
                    do
                    {                        // RENORMD
                        if (ct == 0)
                        {
                            byteIn();
                        }

                        a <<= 1;
                        c <<= 1;
                        --ct;
                    } while ((a & 0x80000000) == 0);
                }
            }
            else
            {
                c -= a;                         // LPS_EXCHANGE
                if (a < qe)
                {
                    bit = mps;
                    stats.cxTab[context] = (byte)((nmpsTab[iCX] << 1) | mps);
                }
                else
                {
                    bit = 1 - mps;
                    stats.cxTab[context] = (byte)((nlpsTab[iCX] << 1)
                                | (switchTab[iCX] != 0 ? 1 - mps : mps));
                }
                a = qe;                         // RENORMD
                do
                {
                    if (ct == 0)
                    {
                        byteIn();
                    }

                    a <<= 1;
                    c <<= 1;
                    --ct;
                } while ((a & 0x80000000) == 0);
            }
            return bit;
        }
        // Returns false for OOB, otherwise sets *<x> and returns true.
        internal long decodeInt(CXStats stats)
        {
            int ret = 0;
            return !decodeInt(ref ret, stats) ? long.MaxValue : ret;
        }
        internal bool decodeInt(ref int x, CXStats stats)
        {
            prev = 1;
            int v, i, a;
            int s = decodeIntBit(stats) == 0 ? 1 : -1;
            if (decodeIntBit(stats) != 0)
            {
                if (decodeIntBit(stats) != 0)
                {
                    if (decodeIntBit(stats) != 0)
                    {
                        if (decodeIntBit(stats) != 0)
                        {
                            (i, a) = (decodeIntBit(stats) != 0)
                                ? (32, 4436) : (12, 340);
                        }
                        else
                        {
                            (i, a) = (8, 84);
                        }
                    }
                    else
                    {
                        (i, a) = (6, 20);
                    }
                }
                else
                {
                    (i, a) = (4, 4);
                }
            }
            else
            {
                (i, a) = (2, 0);
            }

            for (v = 0; i > 0; --i)
            {
                v = (v << 1) | decodeIntBit(stats);
            }

            v += a;
            if (s != 1 && v == 0)
            {
                return false;
            }

            x = s * v;
            return true;
        }
        internal uint decodeIAID(int codeLen, CXStats stats)
        {
            prev = 1;
            for (int i = 0; i < codeLen; ++i)
            {
                int bit = decodeBit((int)prev, stats);
                prev = (uint)(((int)prev << 1) | bit);
            }
            return (uint)(prev - (1 << codeLen));
        }
        private uint readByte()
        {
            if (limitStream)
            {
                if (readBuf >= 0)
                {
                    uint x = (uint)readBuf;
                    readBuf = -1;
                    return x;
                }
                --dataLen;
                if (dataLen < 0)
                {
                    return 0xff;
                }
            }
            return (uint)str.ReadByte() & 0xff;
        }
        private int decodeIntBit(CXStats stats)
        {
            int bit = decodeBit((int)prev, stats);
            prev = (uint)((prev < 0x100) ? ((int)prev << 1) | bit
                        : ((((int)prev << 1) | bit) & 0x1ff) | 0x100);
            return bit;
        }
        private void byteIn()
        {
            if (buf0 == 0xff)
            {
                if (buf1 > 0x8f)
                {
                    if (limitStream)
                    {
                        (buf0, buf1) = (buf1, readByte());
                        c += 0xff00 - (buf0 << 8);
                    }
                    ct = 8;
                }
                else
                {
                    (buf0, buf1, ct) = (buf1, readByte(), 7);
                    c += 0xfe00 - (buf0 << 9);
                }
            }
            else
            {
                (buf0, buf1, ct) = (buf1, readByte(), 8);
                c += 0xff00 - (buf0 << 8);
            }
        }
        private uint buf0, buf1, c, a, prev;         // for the integer decoder
        private int ct, dataLen = 0, readBuf = -1;
        private MemoryStream str = null;
        private bool limitStream = false;
    };
    internal class ImgStream : MemoryStream
    {
        private readonly byte[] orgData;
        internal long startPos = 0, bitBuf = 0, bitAvl = 0;
        public ImgStream(byte[] data) : base(data)
        {
            orgData = data;
        }
        public ImgStream(ImgStream oth, long off, long len)
                : base(oth.orgData, (int)(oth.startPos + off), (int)len)
        {
            orgData = oth.orgData;
            startPos = oth.startPos + off;
        }
        public sbyte readSByte()
        {
            bitAvl = 0;
            int ch = ReadByte();
            return ch < 0 ? throw new EndOfStreamException() : (sbyte)ch;
        }
        public int readInt(int n = 4)
        {
            if (EOF(n - 1))
            {
                return -1; // throw new EndOfStreamException();
            }

            bitAvl = 0;
            int ret = 0;
            for (; n > 0; n--)
            {
                ret = (ret << 8) | ReadByte();
            }

            return ret;
        }
        public int readBits(int numBits)
        {
            if (numBits == 0)
            {
                return 0;
            }

            for (; bitAvl < numBits; bitAvl += 8)
            {
                bitBuf = (bitBuf << 8) | (long)ReadByte();
            }

            ulong ret = ((ulong)bitBuf >> (int)(bitAvl - numBits)) & (ulong)((1 << numBits) - 1);
            bitAvl -= numBits;
            return (int)ret;
        }
        public void Seek(long pos)
        {
            bitAvl = 0;
            _ = base.Seek(pos, SeekOrigin.Begin);
        }
        public void skipBits() => bitAvl = 0;
        public bool EOF(int n = 0) => Position + n >= Length;
        public long RealPos => Position + startPos;
    }
    public class XPdfStream
    {
        internal ImgStream bufStr = null;      // buffered stream (for lookahead)
        internal bool readByte(ref int x)
        {
            int c = bufStr.ReadByte();
            if (c == -1)
            {
                return false;
            }

            x = (sbyte)c;
            return true;
        }
        internal int GetInt(int nBytes) => bufStr.readInt(nBytes);
        internal static bool error(string msg, bool fatal = true) => fatal ? throw new Exception(msg) : false;
    }
    public class XPdfJpx : XPdfStream
    {
        private class ArrayPtr<T>
        {
            internal T[] data;
            internal int off;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ArrayPtr(T[] b = null, int o = 0) { data = b; off = o; }
            internal T this[int i]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => data[off + i]; [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set => data[off + i] = value;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ArrayPtr<T> operator +(ArrayPtr<T> v, int i) => new(v.data, v.off + i);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void set(ArrayPtr<T> v, int i = 0)
            {
                data = v.data; off = v.off + i;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void inc(int i) => off += i;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ArrayPtr<T> operator ++(ArrayPtr<T> v)
            {
                v.off++; return v;
            }

        };
        private class JPXPalette
        {
            internal int nEntries;          // number of entries in the palette
            internal int nComps;           // number of components in each entry
            internal uint[] bpc = null;     // bits per component, for each component
            internal int[] c = null;        // color data: c[i*nComps+j] = entry i, component j
        };
        private class JPXTagTreeNode
        {
            internal bool finished = false;     // true if this node is finished
            internal int val = 0;              // current value
        };
        private class JPXCodeBlock
        {                    //----- size
            internal int x0, y0, x1, y1;        // bounds
            internal bool seen = false;         // true if this code-block has already been seen
            internal int lBlock;                // base number of bits used for pkt data length
            internal int nextPass;              // next coding pass
            internal int nZeroBitPlanes;        // number of zero bit planes
            internal int included;              // code-block inclusion in this packet: 0=not included, 1=included
            internal int nCodingPasses;         // number of coding passes in this pkt
            internal int[] dataLen = null;      // data lengths (one per codeword segment)
                                                //----- coefficient data
            internal ArrayPtr<int> coeffs;
            internal ArrayPtr<bool> touched = null;// coefficient 'touched' flags
            internal int len;                 // coefficient length
            internal ArithmDecoder arithDecoder = null;// arithmetic decoder
            internal CXStats stats = null;  // arithmetic decoder stats
        };
        private class JPXSubband
        {
            internal int nXCBs, nYCBs;              // number of code-blocks in the x and y directions
            internal int maxTTLevel;                // max tag tree level
            internal JPXTagTreeNode[] inclusion;    // inclusion tag tree for each subband
            internal JPXTagTreeNode[] zeroBitPlane; // zero-bit plane tag tree for each subband
            internal JPXCodeBlock[] cbs;            //----- children the code-blocks (len = nXCBs * nYCBs)
        };
        private class JPXPrecinct
        {
            internal JPXSubband[] subbands = null;       //----- children the subbands
        };
        private class JPXResLevel
        {
            //----- from the COD and COC segments (main and tile)
            internal int precinctWidth = 15, precinctHeight = 15;  // log2(precinct width/height)
                                                                   //----- computed
            internal int x0, y0, x1, y1;                            // bounds of this tile-comp at this res level
            internal int[] bx0 = new int[3], by0 = new int[3],      // subband bounds
                           bx1 = new int[3], by1 = new int[3];
            internal int codeBlockW, codeBlockH;                    // log2(code-block width/height)
            internal int cbW, cbH;                                  // code-block width/height
            internal bool empty;            // true if all subbands and precincts are zero width or height
            internal JPXPrecinct[] precincts = null; //---- children the precincts
        };
        private class JPXTileComp
        {
            //----- from the SIZ segment
            internal bool sgned;           // 1 for signed, 0 for unsigned
            internal int prec;                      // precision, in bits
            internal int hSep, vSep;                // hor/vert separation of samples

            //----- from the COD and COC segments (main and tile)
            internal int style;                     // coding style parameter (Scod / Scoc)
            internal int nDecompLevels;             // number of decomposition levels
            internal int codeBlockW, codeBlockH;    // log2(code-block width/height)
            internal int codeBlockStyle;            // code-block style
            internal int transform;                 // wavelet transformation
                                                    //----- from the QCD and QCC segments (main and tile)
            internal int quantStyle;                // quantization style
            internal int[] quantSteps = null;       // quantization step size for each subband
            internal int x0, y0, x1, y1;            // bounds of the tile-comp, in ref coords
            internal int w, h;                      // data size = {x1 - x0, y1 - y0} >> reduction
                                                    //----- image data
            internal int[] data = null;             // the decoded image data
            internal int[] buf = null;              // intermediate buffer for the inverse transform
            internal JPXResLevel[] resLevels = null; //----- children the resolution levels (len = nDecompLevels + 1)
            internal JPXTileComp Clone() => MemberwiseClone() as JPXTileComp;
            internal void Init(JPXTileComp tmpTile)
            {
                if (tmpTile.nDecompLevels < 1 || tmpTile.nDecompLevels > 31
                || tmpTile.codeBlockW > 10 || tmpTile.codeBlockH > 10
                || tmpTile.transform == -1)
                {
                    _ = error("Error in JPX COD marker segment");
                }

                style = tmpTile.style;
                nDecompLevels = tmpTile.nDecompLevels;
                codeBlockW = tmpTile.codeBlockW;
                codeBlockH = tmpTile.codeBlockH;
                codeBlockStyle = tmpTile.codeBlockStyle;
                transform = tmpTile.transform;
                _ = ReAlloc(ref resLevels, tmpTile.nDecompLevels + 1);
                for (int r = 0; r <= tmpTile.nDecompLevels; ++r)
                {
                    resLevels[r].precincts = null;
                }
            }
            internal void InitResLvels(XPdfJpx jpx)
            {
                for (int r = 0, sz; r <= nDecompLevels; ++r)
                {
                    if ((style & 0x01) != 0)
                    {
                        if ((sz = jpx.GetInt(1)) == -1)
                        {
                            _ = error("Error in JPX COD marker segment");
                        }

                        if (r > 0 && ((sz & 0x0f) == 0 || (sz & 0xf0) == 0))
                        {
                            _ = error("Invalid precinct size in JPX COD marker segment");
                        }

                        resLevels[r].precinctWidth = sz & 0x0f;
                        resLevels[r].precinctHeight = (sz >> 4) & 0x0f;
                    }
                    else
                    {
                        resLevels[r].precinctWidth = 15;
                        resLevels[r].precinctHeight = 15;
                    }
                }
            }
            internal void InitQuantSteps(XPdfJpx jpx, int segLen)
            {
                int nQuantSteps = 0;
                if ((quantStyle & 0x1f) == 0x00)
                {
                    nQuantSteps = segLen;
                }
                else if ((quantStyle & 0x1f) == 0x01)
                {
                    nQuantSteps = 1;
                }
                else if ((quantStyle & 0x1f) == 0x02)
                {
                    nQuantSteps = segLen / 2;
                }

                if (nQuantSteps < 1)
                {
                    _ = error("Error in JPX QCD marker segment");
                }

                _ = ReAlloc(ref quantSteps, nQuantSteps);
                for (int i = 0; i < nQuantSteps; ++i)
                {
                    if ((quantSteps[i] = jpx.GetInt(1)) == -1)
                    {
                        _ = error("Error in JPX QCD marker segment");
                    }
                }
            }
        };
        private class JPXTile
        {
            internal bool init = false;
            //----- from the COD segments (main and tile)
            internal int progOrder;        // progression order
            internal int nLayers;          // number of layers
            internal int multiComp;        // multiple component transformation

            //----- computed
            internal int x0, y0, x1, y1;       // bounds of the tile, in ref coords
            internal int maxNDecompLevels;      // max number of decomposition levels used in any component in this tile
            internal int maxNPrecincts;         // max number of precints in any component/res level in this tile

            //----- progression order loop counters
            internal int comp;         // component
            internal int res;          // resolution level
            internal int precinct;     // precinct
            internal int layer;            // layer
            internal bool done;         // set when this tile is done
            internal int nextTilePart = 0;     // next expected tile-part
            internal JPXTileComp[] tileComps = null;// the tile-components (len = JPXImage.nComps)
        };
        private class JPXImage
        {                            //----- from the SIZ segment
            internal int xSize, ySize;              // size of reference grid
            internal int xOffset, yOffset;          // image offset
            internal int xTileSize, yTileSize;      // size of tiles
            internal int xTileOffset, yTileOffset;  // offset of first tile
            internal int nComps;                    // number of components
            internal int nXTiles, nYTiles;          // number of tiles in x/y direction
            internal JPXTile[] tiles = null;        // the tiles (len = nXTiles * nYTiles)
        };

        private const int jpxNContexts = 19, jpxContextSigProp = 0, jpxContextRunLength = 17, jpxContextUniform = 18;
        private const int jpxPassSigProp = 0, jpxPassMagRef = 1, jpxPassCleanup = 2;

        //------------------------------------------------------------------------
        private static readonly int[][][][] sigPropContext = [
            [
                [[0,0,0],[1,1,3],[2,2,6],[2,2,8],[2,2,8]],
                [[5,3,1],[6,3,4],[6,3,7],[6,3,8],[6,3,8]],
                [[8,4,2],[8,4,5],[8,4,7],[8,4,8],[8,4,8]]
            ], [
                [[3,5,1],[3,6,4],[3,6,7],[3,6,8],[3,6,8]],
                [[7,7,2],[7,7,5],[7,7,7],[7,7,8],[7,7,8]],
                [[8,7,2],[8,7,5],[8,7,7],[8,7,8],[8,7,8]]
            ], [
                [[4,8,2],[4,8,5],[4,8,7],[4,8,8],[4,8,8]],
                [[7,8,2],[7,8,5],[7,8,7],[7,8,8],[7,8,8]],
                [[8,8,2],[8,8,5],[8,8,7],[8,8,8],[8,8,8]]
            ]
        ];

        // arithmetic decoder context and xor bit for the sign bit in the significance propagation pass:
        //     [horiz][vert][k]
        // where horiz/vert are offset by 2 (i.e., range is -2 .. 2)
        // and k = 0 for the context
        //       = 1 for the xor bit
        private static readonly int[][][] signContext = [
            [[13,1],[13,1],[12,1],[11,1],[11,1]],
            [[13,1],[13,1],[12,1],[11,1],[11,1]],
            [[10,1],[10,1],[9,0],[10,0],[10,0]],
            [[11,0],[11,0],[12,0],[13,0],[13,0]],
            [[11,0],[11,0],[12,0],[13,0],[13,0]],
        ];

        // constants used in the IDWT
        private const double idwtAlpha = -1.586134342059924, idwtBeta = -0.052980118572961,
            idwtGamma = 0.882911075530934, idwtDelta = 0.443506852043971,
            idwtKappa = 1.230174104914001, idwtIKappa = 1.0 / idwtKappa;

        // sum of the sample size (number of bits) and the number of bits to
        // the right of the decimal point for the fixed point arithmetic used
        // in the IDWT
        private const int fracBits = 24;
        private int jpxFloorDivPow2(int x, int y) => x <= 0 ? 0 : x >> y;
        private int jpxCeilDiv(int x, int y) => (x + y - 1) / y;
        private int jpxCeilDivPow2(int x, int y) => (x + (1 << y) - 1) >> y;
        public XPdfJpx(byte[] strA)
        {
            bufStr = new ImgStream(strA);
        }
        public Image decodeImage(byte[] gsPlt = null)
        {
            bufStr.Position = 0;
            readBoxes();
            int nComps = havePalette ? palette.nComps : img.nComps;
            if (nComps is not 3 and not 1)
            {
                throw new Exception("invalid format");
            }

            Bitmap bmp = new(img.xSize, img.ySize, PixelFormat.Format24bppRgb);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, img.xSize, img.ySize),
                                            ImageLockMode.ReadWrite, bmp.PixelFormat);
            byte[] buf = new byte[bd.Stride];
            for (int curY = img.yOffset; curY < img.ySize; curY++)
            {
                Array.Clear(buf, 0, buf.Length);
                for (int curX = img.xOffset; curX < img.xSize; curX++)
                {
                    JPXTile tile = img.tiles[((curY - img.yTileOffset) / img.yTileSize * img.nXTiles)
                                            + ((curX - img.xTileOffset) / img.xTileSize)];
                    for (int curComp = 0; curComp < nComps; curComp++)
                    {
                        JPXTileComp tileComp = tile.tileComps[havePalette ? 0 : curComp];
                        int pix = tileComp.data[(Math.Max(0, (curY / tileComp.vSep) - tileComp.y0) * tileComp.w)
                                              + Math.Max(0, (curX / tileComp.hSep) - tileComp.x0)];
                        if (havePalette)
                        {
                            pix = (pix >= 0 && pix < palette.nEntries)
                                ? palette.c[(pix * palette.nComps) + curComp] : 0;
                        }

                        if (nComps != 1)
                        {
                            buf[(curX * nComps) + (2 - curComp)] = (byte)pix;
                        }
                        else if (pix != 0)
                        {
                            pix = gsPlt == null ? 255   // use just pix for grayscale!!!!!
                                    : ((gsPlt.Length > 256) ? gsPlt[pix * 3] : gsPlt[pix]);
                            buf[curX * 3] = buf[(curX * 3) + 1] = buf[(curX * 3) + 2] = (byte)pix;
                        }
                    }   // still some work to do grayscale are not quite working...
                }
                Marshal.Copy(buf, 0, bd.Scan0 + (curY * bd.Stride), buf.Length);
            }
            img = null;
            bmp.UnlockBits(bd);
            return bmp;
        }
        private static T[] Alloc<T>(int num) where T : new()
        {
            T[] r = new T[num];
            for (int i = 0; i < num; i++)
            {
                r[i] = new T();
            }

            return r;
        }
        private static T[] ReAlloc<T>(ref T[] a, int n) where T : new()
        {
            int o = (a?.Length) ?? 0;
            if (a == null)
            {
                a = new T[n];
            }
            else if (o != n)
            {
                Array.Resize(ref a, n);
            }

            for (int i = o; i < n; i++)
            {
                a[i] = new T();
            }

            return a;
        }
        private void readBoxes()
        {
            int boxType = 0, boxLen = 0, dataLen = 0, compression;
            if (new int[] { 0x0c, 0x6a502020, 0x0d0a870a }.Any(x => x != GetInt(4)))
            {
                _ = error("File is neither valid JP2 file nor valid JPEG 2000 codestream");
            }

            long dataStart = bufStr.Position;
            for (; readBoxHdr(ref boxType, ref boxLen, ref dataLen); bufStr.Position = dataStart + dataLen)
            {
                dataStart = bufStr.Position;
                switch (boxType)
                {
                    case 0x69686472:                // image header
                        bufStr.Position += 8; nComps = GetInt(2);
                        bufStr.Position++; compression = GetInt(1);
                        if (compression != 7 || nComps < 1 || bufStr.EOF())
                        {
                            _ = error("Bad header info");
                        }

                        break;
                    case 0x70636c72:        // palette
                        if ((palette.nEntries = GetInt(2)) == -1 || (palette.nComps = GetInt(1)) == -1)
                        {
                            _ = error("Unexpected EOF in JPX stream");
                        }

                        havePalette = true;
                        palette.bpc = new uint[palette.nComps];
                        palette.c = new int[palette.nEntries * palette.nComps];
                        for (int i = 0; i < palette.nComps; ++i)
                        {
                            palette.bpc[i] = (uint)GetInt(1) + 1;
                        }

                        for (int i = 0; i < palette.nEntries; ++i)
                        {
                            for (int j = 0; j < palette.nComps; ++j)
                            {
                                palette.c[(i * palette.nComps) + j] = GetInt((((int)palette.bpc[j] & 0x7f) + 7) >> 3);
                            }
                        }

                        break;
                    case 0x6A703263:        // contiguous codestream
                        readCodestream();
                        dataStart = bufStr.Position; dataLen = 0;
                        break;
                    case 0x6a703268:        // JP2 header
                                            // this is a grouping box ('superbox') which has no real contents and
                                            // doesn't appear to be used consistently, i.e., some things which should
                                            // be subboxes of the JP2 header box show up outside of it - so we simply
                                            // ignore the JP2 header box
                    case 0x63646566:        // channel definition
                    case 0x636d6170:        // component mapping
                    case 0x62706363:        // bits per component
                    case 0x636F6C72:        // color specification
                    default:
                        break;
                }
            }
        }
        private void readCodestream()
        {
            bool haveSIZ = false, haveCOD = false, haveQCD = false, haveSOT = false;
            int segType = 0, progOrder, multiComp, style, nLayers, segLen = 0, comp, i, r;
            JPXTileComp tmpTile;
            do
            {                //----- main header
                if (!readMarkerHdr(ref segType, ref segLen))
                {
                    _ = error("Error in JPX codestream");
                }

                switch (segType)
                {
                    case 0x4f: break;   // SOC - start of codestream
                    case 0x51:          // SIZ - image and tile size
                        _ = GetInt(2);
                        (img.xSize, img.ySize, img.xOffset, img.yOffset) = (GetInt(4), GetInt(4), GetInt(4), GetInt(4));
                        (img.xTileSize, img.yTileSize, img.xTileOffset, img.yTileOffset) = (GetInt(4), GetInt(4), GetInt(4), GetInt(4));
                        if (haveSIZ || (img.nComps = GetInt(2)) == -1 || (nComps != 0 && img.nComps != nComps)
                        || img.xSize == 0 || img.ySize == 0
                        || img.xOffset >= img.xSize || img.yOffset >= img.ySize
                        || img.xTileSize == 0 || img.yTileSize == 0
                        || img.xTileOffset > img.xOffset || img.yTileOffset > img.yOffset
                        || img.xTileSize + img.xTileOffset <= img.xOffset
                        || img.yTileSize + img.yTileOffset <= img.yOffset || img.nComps == 0)
                        {
                            _ = error("Error in JPX SIZ marker segment");
                        }

                        img.nXTiles = (img.xSize - img.xTileOffset + img.xTileSize - 1) / img.xTileSize;
                        img.nYTiles = (img.ySize - img.yTileOffset + img.yTileSize - 1) / img.yTileSize;
                        // check for overflow before allocating memory
                        if (img.nXTiles <= 0 || img.nYTiles <= 0 || img.nXTiles >= int.MaxValue / img.nYTiles)
                        {
                            _ = error("Bad tile count in JPX SIZ marker segment");
                        }

                        img.tiles = Alloc<JPXTile>(img.nXTiles * img.nYTiles);
                        for (i = 0; i < img.nXTiles * img.nYTiles; ++i)
                        {
                            img.tiles[i].tileComps = Alloc<JPXTileComp>(img.nComps);
                        }

                        for (comp = 0; comp < img.nComps; ++comp)
                        {
                            img.tiles[0].tileComps[comp].prec = (sbyte)GetInt(1);
                            img.tiles[0].tileComps[comp].hSep = GetInt(1);
                            img.tiles[0].tileComps[comp].vSep = GetInt(1);
                            if (img.tiles[0].tileComps[comp].hSep < 1 || img.tiles[0].tileComps[comp].vSep < 1)
                            {
                                _ = error("Error in JPX SIZ marker segment");
                            }

                            img.tiles[0].tileComps[comp].sgned = (img.tiles[0].tileComps[comp].prec & 0x80) != 0;
                            img.tiles[0].tileComps[comp].prec = (img.tiles[0].tileComps[comp].prec & 0x7f) + 1;
                            for (i = 1; i < img.nXTiles * img.nYTiles; ++i)
                            {
                                img.tiles[i].tileComps[comp] = img.tiles[0].tileComps[comp].Clone();
                            }
                        }
                        haveSIZ = true;
                        break;
                    case 0x52:          // COD - coding style default
                        (style, progOrder, nLayers, multiComp)
                            = (GetInt(1), GetInt(1), GetInt(2), GetInt(1));
                        if (!haveSIZ || multiComp == -1)
                        {
                            _ = error("Error in JPX COD marker segment");
                        }

                        tmpTile = new JPXTileComp
                        {
                            style = style,
                            nDecompLevels = GetInt(1),
                            codeBlockW = GetInt(1) + 2,
                            codeBlockH = GetInt(1) + 2,
                            codeBlockStyle = GetInt(1),
                            transform = GetInt(1)
                        };
                        for (i = 0; i < img.nXTiles * img.nYTiles; ++i)
                        {
                            img.tiles[i].progOrder = progOrder;
                            img.tiles[i].nLayers = nLayers;
                            img.tiles[i].multiComp = multiComp;
                            for (comp = 0; comp < img.nComps; ++comp)
                            {
                                img.tiles[i].tileComps[comp].Init(tmpTile);
                            }
                        }
                        img.tiles[0].tileComps[0].InitResLvels(this);
                        for (i = 0; i < img.nXTiles * img.nYTiles; ++i)
                        {
                            for (comp = 0; comp < img.nComps; ++comp)
                            {
                                if (!(i == 0 && comp == 0))
                                {
                                    for (r = 0; r <= tmpTile.nDecompLevels; ++r)
                                    {
                                        img.tiles[i].tileComps[comp].resLevels[r].precinctWidth =
                                            img.tiles[0].tileComps[0].resLevels[r].precinctWidth;
                                        img.tiles[i].tileComps[comp].resLevels[r].precinctHeight =
                                            img.tiles[0].tileComps[0].resLevels[r].precinctHeight;
                                    }
                                }
                            }
                        }

                        haveCOD = true;
                        break;
                    case 0x53:          // COC - coding style component
                        (comp, style) = (GetInt(img.nComps > 256 ? 2 : 1), GetInt(1));
                        if (!haveCOD || comp >= img.nComps || style == -1)
                        {
                            _ = error("Error in JPX COC marker segment");
                        }

                        tmpTile = new JPXTileComp
                        {
                            nDecompLevels = GetInt(1),
                            style = (img.tiles[0].tileComps[comp].style & ~1) | (style & 1),
                            codeBlockW = GetInt(1) + 2,
                            codeBlockH = GetInt(1) + 2,
                            codeBlockStyle = GetInt(1),
                            transform = GetInt(1)
                        };
                        for (i = 0; i < img.nXTiles * img.nYTiles; ++i)
                        {
                            img.tiles[i].tileComps[comp].Init(tmpTile);
                        }

                        img.tiles[0].tileComps[comp].InitResLvels(this);
                        for (i = 1; i < img.nXTiles * img.nYTiles; ++i)
                        {
                            for (r = 0; r <= img.tiles[i].tileComps[comp].nDecompLevels; ++r)
                            {
                                img.tiles[i].tileComps[comp].resLevels[r].precinctWidth
                                    = img.tiles[0].tileComps[comp].resLevels[r].precinctWidth;
                                img.tiles[i].tileComps[comp].resLevels[r].precinctHeight
                                    = img.tiles[0].tileComps[comp].resLevels[r].precinctHeight;
                            }
                        }

                        break;
                    case 0x5c:          // QCD - quantization default
                        img.tiles[0].tileComps[0].quantStyle = GetInt(1);
                        img.tiles[0].tileComps[0].InitQuantSteps(this, segLen - 3);
                        for (i = 0; i < img.nXTiles * img.nYTiles; ++i)
                        {
                            for (comp = 0; comp < img.nComps; ++comp)
                            {
                                if (!(i == 0 && comp == 0))
                                {
                                    img.tiles[i].tileComps[comp].quantStyle = img.tiles[0].tileComps[0].quantStyle;
                                    img.tiles[i].tileComps[comp].quantSteps = [.. img.tiles[0].tileComps[0].quantSteps];
                                }
                            }
                        }

                        haveQCD = true;
                        break;
                    case 0x5d:          // QCC - quantization component
                        if (!haveQCD)
                        {
                            _ = error("JPX QCC marker segment before QCD segment");
                        }

                        comp = GetInt(img.nComps > 256 ? 2 : 1);
                        img.tiles[0].tileComps[comp].quantStyle = (sbyte)GetInt(1);
                        img.tiles[0].tileComps[comp].InitQuantSteps(this, segLen - (img.nComps > 256 ? 5 : 4));
                        for (i = 1; i < img.nXTiles * img.nYTiles; ++i)
                        {
                            img.tiles[i].tileComps[comp].quantStyle = img.tiles[0].tileComps[comp].quantStyle;
                            img.tiles[i].tileComps[comp].quantSteps = [.. img.tiles[0].tileComps[comp].quantSteps];
                        }
                        break;
                    case 0x90:          // SOT - start of tile
                        haveSOT = true;
                        break;
                    case 0x5e:          // RGN - region of interest ~ ROI is unimplemented
                    case 0x5f:          // POC - progression order change
                    case 0x60:          // PPM - packed packet headers, main header
                    case 0x55:          // TLM - tile-part lengths
                    case 0x57:          // PLM - packet length, main header
                    case 0x63:          // CRG - component registration
                    case 0x64:          // COM - comment
                    default:
                        //error("Unknown marker segment {0:02x} in JPX stream:" + segType, false);
                        if (segLen > 2)
                        {
                            bufStr.Position += segLen - 2;
                        }

                        break;
                }
            } while (!haveSOT);
            if (!haveSIZ)
            {
                _ = error("Missing SIZ marker segment in JPX stream");
            }

            if (!haveCOD)
            {
                _ = error("Missing COD marker segment in JPX stream");
            }

            if (!haveQCD)
            {
                _ = error("Missing QCD marker segment in JPX stream");
            }
            //----- read the tile-parts
            for (segType = 0x90; segType == 0x90;)
            { // SOT - start of tile
                readTilePart();
                if (!readMarkerHdr(ref segType, ref segLen))
                {
                    _ = error("Error in JPX codestream");
                }
            }
            if (segType != 0xd9)        // EOC - end of codestream
            {
                _ = error("Missing EOC marker in JPX codestream");
            }
            //----- finish decoding the image
            for (i = 0; i < img.nXTiles * img.nYTiles; ++i)
            {
                JPXTile tile = img.tiles[i];
                if (!tile.init)
                {
                    _ = error("Uninitialized tile in JPX codestream");
                }

                for (comp = 0; comp < img.nComps; ++comp)
                {
                    inverseTransform(tile.tileComps[comp]);
                }

                inverseMultiCompAndDC(tile);
            }
        }
        private void readTilePart()
        {
            int tileIdx, tilePartIdx, nTileParts, progOrder, multiComp, cbX, cbY,
                qStyle, nLayers, preCol0, preCol1, preRow0, preRow1, preCol, preRow, style,
                nx, ny, nSBs, comp, segLen = 0, i, j, r, sb, n, segType = 0, level, tilePartLen;
            JPXTileComp tmpTile;
            // process the SOT marker segment
            (tileIdx, tilePartLen, tilePartIdx, nTileParts) = (GetInt(2), GetInt(4), GetInt(1), GetInt(1));
            // check tileIdx and tilePartIdx
            // (this ignores nTileParts, because some encoders get it wrong)
            if (nTileParts == -1
            || tileIdx >= img.nXTiles * img.nYTiles || tilePartIdx != img.tiles[tileIdx].nextTilePart
            || (tilePartIdx > 0 && !img.tiles[tileIdx].init)
            || (tilePartIdx == 0 && img.tiles[tileIdx].init))
            {
                _ = error("Weird tile-part header in JPX stream");
            }

            ++img.tiles[tileIdx].nextTilePart;
            bool tilePartToEOC = tilePartLen == 0, haveSOD = false;
            tilePartLen -= 12; // subtract size of SOT segment
            do
            {
                if (!readMarkerHdr(ref segType, ref segLen))
                {
                    _ = error("Error in JPX tile-part codestream");
                }

                tilePartLen -= 2 + segLen;
                switch (segType)
                {
                    case 0x52:          // COD - coding style default
                        (style, progOrder, nLayers, multiComp)
                            = (GetInt(1), GetInt(1), GetInt(2), GetInt(1));
                        if (tilePartIdx != 0 || multiComp == -1)
                        {
                            _ = error("Error in JPX COD marker segment");
                        }

                        tmpTile = new JPXTileComp
                        {
                            style = style,
                            nDecompLevels = GetInt(1),
                            codeBlockW = GetInt(1) + 2,
                            codeBlockH = GetInt(1) + 2,
                            codeBlockStyle = GetInt(1),
                            transform = GetInt(1)
                        };
                        img.tiles[tileIdx].progOrder = progOrder;
                        img.tiles[tileIdx].nLayers = nLayers;
                        img.tiles[tileIdx].multiComp = multiComp;
                        for (comp = 0; comp < img.nComps; ++comp)
                        {
                            img.tiles[tileIdx].tileComps[comp].Init(tmpTile);
                        }

                        img.tiles[tileIdx].tileComps[0].InitResLvels(this);
                        for (comp = 1; comp < img.nComps; ++comp)
                        {
                            for (r = 0; r <= tmpTile.nDecompLevels; ++r)
                            {
                                img.tiles[tileIdx].tileComps[comp].resLevels[r].precinctWidth =
                                    img.tiles[tileIdx].tileComps[0].resLevels[r].precinctWidth;
                                img.tiles[tileIdx].tileComps[comp].resLevels[r].precinctHeight =
                                    img.tiles[tileIdx].tileComps[0].resLevels[r].precinctHeight;
                            }
                        }

                        break;
                    case 0x53:          // COC - coding style component
                        (comp, style) = (GetInt(img.nComps > 256 ? 2 : 1), GetInt(1));
                        if (comp >= img.nComps || style == -1 || tilePartIdx != 0)
                        {
                            _ = error("Error in JPX COC marker segment");
                        }

                        tmpTile = new JPXTileComp
                        {
                            nDecompLevels = GetInt(1),
                            style = (img.tiles[tileIdx].tileComps[comp].style & ~1) | (style & 1),
                            codeBlockW = GetInt(1) + 2,
                            codeBlockH = GetInt(1) + 2,
                            codeBlockStyle = GetInt(1),
                            transform = GetInt(1)
                        };
                        img.tiles[tileIdx].tileComps[comp].Init(tmpTile);
                        img.tiles[tileIdx].tileComps[comp].InitResLvels(this);
                        break;
                    case 0x5c:          // QCD - quantization default
                        img.tiles[tileIdx].tileComps[0].quantStyle = (sbyte)GetInt(1);
                        img.tiles[tileIdx].tileComps[0].InitQuantSteps(this, segLen - 3);
                        for (comp = 1; comp < img.nComps; ++comp)
                        {
                            img.tiles[tileIdx].tileComps[comp].quantStyle = img.tiles[tileIdx].tileComps[0].quantStyle;
                            img.tiles[tileIdx].tileComps[comp].quantSteps = [.. img.tiles[tileIdx].tileComps[0].quantSteps];
                        }
                        break;
                    case 0x5d:          // QCC - quantization component
                        if (tilePartIdx != 0)
                        {
                            _ = error("Extraneous JPX QCC marker segment");
                        }

                        if ((comp = GetInt(img.nComps > 256 ? 2 : 1)) == -1 || comp >= img.nComps
                        || !readByte(ref img.tiles[tileIdx].tileComps[comp].quantStyle))
                        {
                            _ = error("Error in JPX QCC marker segment");
                        }

                        img.tiles[tileIdx].tileComps[comp].InitQuantSteps(this, segLen - (img.nComps > 256 ? 5 : 4));
                        break;
                    case 0x93:          // SOD - start of data
                        haveSOD = true;
                        break;
                    case 0x5e:          // RGN - region of interest
                    case 0x5f:          // POC - progression order change
                    case 0x61:          // PPT - packed packet headers, tile-part hdr
                    case 0x58:          // PLT - packet length, tile-part header
                    case 0x64:          // COM - comment
                    default:
                        if (segLen > 2)
                        {
                            bufStr.Position += segLen - 2;
                        }

                        _ = error("Error in JPX marker segment " + segType);
                        break;
                }
            } while (!haveSOD);
            for (comp = 0; comp < img.nComps; ++comp)
            {
                JPXTileComp tileComp = img.tiles[tileIdx].tileComps[comp];
                qStyle = tileComp.quantStyle & 0x1f;
                if ((qStyle == 0 && tileComp.quantSteps.Length < (3 * tileComp.nDecompLevels) + 1)
                || (qStyle == 1 && tileComp.quantSteps.Length < 1)
                || (qStyle == 2 && tileComp.quantSteps.Length < (3 * tileComp.nDecompLevels) + 1))
                {
                    _ = error("Too few quant steps in JPX tile part");
                }
            }
            //----- initialize the tile, precincts, and code-blocks
            if (tilePartIdx == 0)
            {
                JPXTile tile = img.tiles[tileIdx];
                (i, j) = (tileIdx / img.nXTiles, tileIdx % img.nXTiles);
                tile.x0 = Math.Max(img.xOffset, img.xTileOffset + (j * img.xTileSize));
                tile.y0 = Math.Max(img.yOffset, img.yTileOffset + (i * img.yTileSize));
                tile.x1 = Math.Min(img.xSize, img.xTileOffset + ((j + 1) * img.xTileSize));
                tile.y1 = Math.Min(img.ySize, img.yTileOffset + ((i + 1) * img.yTileSize));
                tile.done = false;
                tile.maxNDecompLevels = tile.maxNPrecincts = tile.comp
                        = tile.res = tile.precinct = tile.layer = 0;
                for (comp = 0; comp < img.nComps; ++comp)
                {
                    JPXTileComp tileComp = tile.tileComps[comp];
                    if (tileComp.nDecompLevels > tile.maxNDecompLevels)
                    {
                        tile.maxNDecompLevels = tileComp.nDecompLevels;
                    }

                    tileComp.x0 = jpxCeilDiv(tile.x0, tileComp.hSep);
                    tileComp.y0 = jpxCeilDiv(tile.y0, tileComp.vSep);
                    tileComp.x1 = jpxCeilDiv(tile.x1, tileComp.hSep);
                    tileComp.y1 = jpxCeilDiv(tile.y1, tileComp.vSep);
                    tileComp.w = tileComp.x1 - tileComp.x0;
                    tileComp.h = tileComp.y1 - tileComp.y0;
                    if (tileComp.w == 0 || tileComp.h == 0 || tileComp.w > int.MaxValue / tileComp.h)
                    {
                        _ = error("Invalid tile size or sample separation in JPX stream");
                    }

                    tileComp.data = new int[tileComp.w * tileComp.h];
                    n = Math.Max(tileComp.x1 - tileComp.x0, tileComp.y1 - tileComp.y0);
                    tileComp.buf = new int[n + 8];
                    for (r = 0; r <= tileComp.nDecompLevels; ++r)
                    {
                        JPXResLevel resLevel = tileComp.resLevels[r];
                        resLevel.x0 = jpxCeilDivPow2(tileComp.x0, tileComp.nDecompLevels - r);
                        resLevel.y0 = jpxCeilDivPow2(tileComp.y0, tileComp.nDecompLevels - r);
                        resLevel.x1 = jpxCeilDivPow2(tileComp.x1, tileComp.nDecompLevels - r);
                        resLevel.y1 = jpxCeilDivPow2(tileComp.y1, tileComp.nDecompLevels - r);
                        resLevel.codeBlockW = Math.Min(resLevel.precinctWidth - (r == 0 ? 0 : 1), tileComp.codeBlockW);
                        resLevel.codeBlockH = Math.Min(resLevel.precinctHeight - (r == 0 ? 0 : 1), tileComp.codeBlockH);
                        resLevel.cbW = 1 << resLevel.codeBlockW;
                        resLevel.cbH = 1 << resLevel.codeBlockH;
                        // the JPEG 2000 spec says that packets for empty res levels
                        // should all be present in the codestream (B.6, B.9, B.10),
                        // but it appears that encoders drop packets if the res level
                        // AND the subbands are all completely empty
                        resLevel.empty = resLevel.x0 == resLevel.x1 || resLevel.y0 == resLevel.y1;
                        if (r == 0)
                        {
                            nSBs = 1;
                            (resLevel.bx0[0], resLevel.by0[0]) = (resLevel.x0, resLevel.y0);
                            (resLevel.bx1[0], resLevel.by1[0]) = (resLevel.x1, resLevel.y1);
                            resLevel.empty = resLevel.empty
                                && (resLevel.bx0[0] == resLevel.bx1[0] || resLevel.by0[0] == resLevel.by1[0]);
                        }
                        else
                        {
                            nSBs = 3;
                            resLevel.bx0[0] = jpxCeilDivPow2(resLevel.x0 - 1, 1);
                            resLevel.by0[0] = jpxCeilDivPow2(resLevel.y0, 1);
                            resLevel.bx1[0] = jpxCeilDivPow2(resLevel.x1 - 1, 1);
                            resLevel.by1[0] = jpxCeilDivPow2(resLevel.y1, 1);
                            resLevel.bx0[1] = jpxCeilDivPow2(resLevel.x0, 1);
                            resLevel.by0[1] = jpxCeilDivPow2(resLevel.y0 - 1, 1);
                            resLevel.bx1[1] = jpxCeilDivPow2(resLevel.x1, 1);
                            resLevel.by1[1] = jpxCeilDivPow2(resLevel.y1 - 1, 1);
                            resLevel.bx0[2] = jpxCeilDivPow2(resLevel.x0 - 1, 1);
                            resLevel.by0[2] = jpxCeilDivPow2(resLevel.y0 - 1, 1);
                            resLevel.bx1[2] = jpxCeilDivPow2(resLevel.x1 - 1, 1);
                            resLevel.by1[2] = jpxCeilDivPow2(resLevel.y1 - 1, 1);
                            resLevel.empty = resLevel.empty
                                && (resLevel.bx0[0] == resLevel.bx1[0] || resLevel.by0[0] == resLevel.by1[0])
                                && (resLevel.bx0[1] == resLevel.bx1[1] || resLevel.by0[1] == resLevel.by1[1])
                                && (resLevel.bx0[2] == resLevel.bx1[2] || resLevel.by0[2] == resLevel.by1[2]);
                        }
                        preCol0 = jpxFloorDivPow2(resLevel.x0, resLevel.precinctWidth);
                        preCol1 = jpxCeilDivPow2(resLevel.x1, resLevel.precinctWidth);
                        preRow0 = jpxFloorDivPow2(resLevel.y0, resLevel.precinctHeight);
                        preRow1 = jpxCeilDivPow2(resLevel.y1, resLevel.precinctHeight);
                        int nPrecincts = (preCol1 - preCol0) * (preRow1 - preRow0);
                        resLevel.precincts = Alloc<JPXPrecinct>(nPrecincts);
                        tile.maxNPrecincts = Math.Max(tile.maxNPrecincts, nPrecincts);
                        int pIdx = 0;
                        for (preRow = preRow0; preRow < preRow1; ++preRow)
                        {
                            for (preCol = preCol0; preCol < preCol1; ++preCol, ++pIdx)
                            {
                                JPXPrecinct precinct = resLevel.precincts[pIdx];
                                precinct.subbands = Alloc<JPXSubband>(nSBs);
                                for (sb = 0; sb < nSBs; ++sb)
                                {
                                    precinct.subbands[sb].inclusion = null;
                                    precinct.subbands[sb].zeroBitPlane = null;
                                    precinct.subbands[sb].cbs = null;
                                }
                                for (sb = 0; sb < nSBs; ++sb)
                                {
                                    JPXSubband subband = precinct.subbands[sb];
                                    int off = (r == 0) ? 0 : 1;
                                    int px0 = Math.Max(resLevel.bx0[sb], preCol << (resLevel.precinctWidth - off));
                                    int px1 = Math.Min(resLevel.bx1[sb], (preCol + 1) << (resLevel.precinctWidth - off));
                                    int py0 = Math.Max(resLevel.by0[sb], preRow << (resLevel.precinctHeight - off));
                                    int py1 = Math.Min(resLevel.by1[sb], (preRow + 1) << (resLevel.precinctHeight - off));
                                    int sbCoeffs = r == 0
                                        ? 0
                                        : sb == 0
                                            ? resLevel.bx1[1] - resLevel.bx0[1]
                                            : sb == 1
                                        ? (resLevel.by1[0] - resLevel.by0[0]) * tileComp.w
                                        : ((resLevel.by1[0] - resLevel.by0[0]) * tileComp.w)
                                                  + (resLevel.bx1[1] - resLevel.bx0[1]);
                                    int cbCol0 = jpxFloorDivPow2(px0, resLevel.codeBlockW);
                                    int cbCol1 = jpxCeilDivPow2(px1, resLevel.codeBlockW);
                                    int cbRow0 = jpxFloorDivPow2(py0, resLevel.codeBlockH);
                                    int cbRow1 = jpxCeilDivPow2(py1, resLevel.codeBlockH);
                                    subband.nXCBs = cbCol1 - cbCol0;
                                    subband.nYCBs = cbRow1 - cbRow0;
                                    n = Math.Max(subband.nXCBs, subband.nYCBs);
                                    for (subband.maxTTLevel = 0, --n; n != 0; ++subband.maxTTLevel, n >>= 1)
                                    {

                                    }

                                    for (n = 0, level = subband.maxTTLevel; level >= 0; --level)
                                    {
                                        nx = jpxCeilDivPow2(subband.nXCBs, level);
                                        ny = jpxCeilDivPow2(subband.nYCBs, level);
                                        n += nx * ny;
                                    }
                                    subband.inclusion = Alloc<JPXTagTreeNode>(n);
                                    subband.zeroBitPlane = Alloc<JPXTagTreeNode>(n);
                                    subband.cbs = Alloc<JPXCodeBlock>(subband.nXCBs * subband.nYCBs);
                                    int cbi = 0;
                                    for (cbY = cbRow0; cbY < cbRow1; ++cbY)
                                    {
                                        for (cbX = cbCol0; cbX < cbCol1; ++cbX, ++cbi)
                                        {
                                            JPXCodeBlock cb = subband.cbs[cbi];
                                            cb.x0 = Math.Max(px0, cbX << resLevel.codeBlockW);
                                            cb.y0 = Math.Max(py0, cbY << resLevel.codeBlockH);
                                            cb.x1 = Math.Min(px1, cb.x0 + resLevel.cbW);
                                            cb.y1 = Math.Min(py1, cb.y0 + resLevel.cbH);
                                            (cb.seen, cb.lBlock, cb.nextPass, cb.nZeroBitPlanes, cb.dataLen)
                                                = (false, 3, jpxPassCleanup, 0, new int[1]);
                                            if (r <= tileComp.nDecompLevels)
                                            {
                                                cb.coeffs = new ArrayPtr<int>(tileComp.data, sbCoeffs + (cb.x0 - resLevel.bx0[sb])
                                                                                          + (tileComp.w * (cb.y0 - resLevel.by0[sb])));
                                                cb.touched = new ArrayPtr<bool>(new bool[1 << (resLevel.codeBlockW + resLevel.codeBlockH)]);
                                                Array.Clear(cb.touched.data, 0, cb.touched.data.Length);
                                            }
                                            else
                                            {
                                                (cb.coeffs, cb.touched) = (null, null);
                                            }

                                            cb.len = 0;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                tile.init = true;
            }
            readTilePartData(tileIdx, tilePartLen, tilePartToEOC);
        }
        private void readTilePartData(int tileIdx, int tilePartLen, bool tilePartToEOC)
        {
            int bits = 0, nx, ny, cbX, cbY, sb, j, i, ttVal, level, n;
            JPXTile tile = img.tiles[tileIdx];
            for (; ; )
            {         // read all packets from this tile-part
                if (tile.done)          // if the tile is finished, skip any remaining data
                {
                    bufStr.Position += tilePartLen;
                }

                if (!tilePartToEOC && tilePartLen == 0)
                {
                    break;
                }

                JPXTileComp tileComp = tile.tileComps[tile.comp];
                JPXResLevel resLevel = tileComp.resLevels[tile.res];
                JPXPrecinct precinct = resLevel.precincts[tile.precinct];
                if (resLevel.empty)
                {
                    goto nextPacket;
                }
                //----- packet header
                startBitBuf(tilePartLen);               // setup
                if ((tileComp.style & 0x02) != 0)
                {
                    skipSOP();
                }

                if (!readBits(1, ref bits))             // zero-length flag
                {
                    _ = error("Error in JPX stream");
                }

                if (bits == 0)      // packet is empty -- clear all code-block inclusion flags
                {
                    for (sb = 0; sb < (tile.res == 0 ? 1 : 3); ++sb)
                    {
                        JPXSubband subband = precinct.subbands[sb];
                        for (cbY = 0; cbY < subband.nYCBs; ++cbY)
                        {
                            for (cbX = 0; cbX < subband.nXCBs; ++cbX)
                            {
                                subband.cbs[(cbY * subband.nXCBs) + cbX].included = 0;
                            }
                        }
                    }
                }
                else
                {
                    for (sb = 0; sb < (tile.res == 0 ? 1 : 3); ++sb)
                    {
                        JPXSubband subband = precinct.subbands[sb];
                        for (cbY = 0; cbY < subband.nYCBs; ++cbY)
                        {
                            for (cbX = 0; cbX < subband.nXCBs; ++cbX)
                            {
                                JPXCodeBlock cb = subband.cbs[(cbY * subband.nXCBs) + cbX];
                                if (cb.x0 >= cb.x1 || cb.y0 >= cb.y1)
                                {
                                    cb.included = 0;        // skip code-blocks with no coefficients
                                    continue;
                                }
                                if (cb.seen)
                                {              // code-block inclusion
                                    if (!readBits(1, ref cb.included))
                                    {
                                        _ = error("Error in JPX stream");
                                    }
                                }
                                else
                                {
                                    for (ttVal = i = 0, level = subband.maxTTLevel; level >= 0; --level)
                                    {
                                        nx = jpxCeilDivPow2(subband.nXCBs, level);
                                        ny = jpxCeilDivPow2(subband.nYCBs, level);
                                        j = i + ((cbY >> level) * nx) + (cbX >> level);
                                        if (!subband.inclusion[j].finished && subband.inclusion[j].val == 0)
                                        {
                                            subband.inclusion[j].val = ttVal;
                                        }
                                        else
                                        {
                                            ttVal = subband.inclusion[j].val;
                                        }

                                        while (!subband.inclusion[j].finished && ttVal <= tile.layer)
                                        {
                                            if (!readBits(1, ref bits))
                                            {
                                                _ = error("Error in JPX stream");
                                            }

                                            if (bits == 1)
                                            {
                                                subband.inclusion[j].finished = true;
                                            }
                                            else
                                            {
                                                ++ttVal;
                                            }
                                        }
                                        subband.inclusion[j].val = ttVal;
                                        if (ttVal > tile.layer)
                                        {
                                            break;
                                        }

                                        i += nx * ny;
                                    }
                                    cb.included = level < 0 ? 1 : 0;
                                }

                                if (cb.included != 0)
                                {
                                    if (!cb.seen)
                                    {     // zero bit-plane count
                                        for (ttVal = i = 0, level = subband.maxTTLevel; level >= 0;
                                                --level, i += nx * ny)
                                        {
                                            nx = jpxCeilDivPow2(subband.nXCBs, level);
                                            ny = jpxCeilDivPow2(subband.nYCBs, level);
                                            j = i + ((cbY >> level) * nx) + (cbX >> level);
                                            if (!subband.zeroBitPlane[j].finished && subband.zeroBitPlane[j].val == 0)
                                            {
                                                subband.zeroBitPlane[j].val = ttVal;
                                            }
                                            else
                                            {
                                                ttVal = subband.zeroBitPlane[j].val;
                                            }

                                            while (!subband.zeroBitPlane[j].finished)
                                            {
                                                if (!readBits(1, ref bits))
                                                {
                                                    _ = error("Error in JPX stream");
                                                }

                                                if (bits == 1)
                                                {
                                                    subband.zeroBitPlane[j].finished = true;
                                                }
                                                else
                                                {
                                                    ++ttVal;
                                                }
                                            }
                                            subband.zeroBitPlane[j].val = ttVal;
                                        }
                                        cb.nZeroBitPlanes = ttVal;
                                    }
                                    if (!readBits(1, ref bits)) // number of coding passes
                                    {
                                        _ = error("Error in JPX stream");
                                    }

                                    if (bits == 0)
                                    {
                                        cb.nCodingPasses = 1;
                                    }
                                    else if (readBits(1, ref bits) && bits == 0)
                                    {
                                        cb.nCodingPasses = 2;
                                    }
                                    else if (readBits(2, ref bits) && bits < 3)
                                    {
                                        cb.nCodingPasses = 3 + bits;
                                    }
                                    else if (readBits(5, ref bits) && bits < 31)
                                    {
                                        cb.nCodingPasses = 6 + bits;
                                    }
                                    else if (readBits(7, ref bits))
                                    {
                                        cb.nCodingPasses = 37 + bits;
                                    }
                                    else
                                    {
                                        _ = error("Error in JPX stream");
                                    }

                                    for (; ; ++cb.lBlock)
                                    {     // update Lblock
                                        if (!readBits(1, ref bits))
                                        {
                                            _ = error("Error in JPX stream");
                                        }

                                        if (bits == 0)
                                        {
                                            break;
                                        }
                                    }
                                    // one codeword segment for each of the coding passes
                                    if ((tileComp.codeBlockStyle & 0x04) != 0)
                                    {
                                        if (cb.nCodingPasses > cb.dataLen.Length)
                                        {
                                            _ = ReAlloc(ref cb.dataLen, cb.nCodingPasses);
                                        }

                                        for (i = 0; i < cb.nCodingPasses; ++i)
                                        {
                                            if (!readBits(cb.lBlock, ref cb.dataLen[i]))
                                            {
                                                _ = error("Error in JPX stream");
                                            }
                                        }
                                    }
                                    else
                                    {  // read the length
                                        for (n = cb.lBlock, i = cb.nCodingPasses >> 1; i != 0; ++n, i >>= 1)
                                        {

                                        }

                                        if (!readBits(n, ref cb.dataLen[0]))
                                        {
                                            _ = error("Error in JPX stream");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if ((tileComp.style & 0x04) != 0)
                {
                    skipEPH();
                }

                tilePartLen = finishBitBuf();

                //----- packet data
                for (sb = 0; sb < (tile.res == 0 ? 1 : 3); ++sb)
                {
                    JPXSubband subband = precinct.subbands[sb];
                    for (cbY = 0; cbY < subband.nYCBs; ++cbY)
                    {
                        for (cbX = 0; cbX < subband.nXCBs; ++cbX)
                        {
                            JPXCodeBlock cb = subband.cbs[(cbY * subband.nXCBs) + cbX];
                            if (cb.included != 0)
                            {
                                readCodeBlockData(tileComp, resLevel, tile.res, sb, cb);
                                if ((tileComp.codeBlockStyle & 0x04) != 0)
                                {
                                    for (i = 0; i < cb.nCodingPasses; ++i)
                                    {
                                        tilePartLen -= cb.dataLen[i];
                                    }
                                }
                                else
                                {
                                    tilePartLen -= cb.dataLen[0];
                                }

                                cb.seen = true;
                            }
                        }
                    }
                }

            //----- next packet
            nextPacket:
                do
                {
                    switch (tile.progOrder)
                    {
                        case 0: // layer, resolution level, component, precinct
                            tile.done = nextTile(ref tile.precinct, tile.maxNPrecincts, ref tile.comp, img.nComps,
                                ref tile.res, tile.maxNDecompLevels + 1, ref tile.layer, tile.nLayers);
                            break;
                        case 1: // resolution level, layer, component, precinct
                            tile.done = nextTile(ref tile.precinct, tile.maxNPrecincts, ref tile.comp, img.nComps,
                                ref tile.layer, tile.nLayers, ref tile.res, tile.maxNDecompLevels + 1);
                            break;
                        case 2: // resolution level, precinct, component, layer - incorrect if there are subsampled components (?)
                            tile.done = nextTile(ref tile.layer, tile.nLayers, ref tile.comp, img.nComps,
                                ref tile.precinct, tile.maxNPrecincts, ref tile.res, tile.maxNPrecincts + 1);
                            break;
                        case 3: // precinct, component, resolution level, layer - incorrect if there are subsampled components (?)
                            tile.done = nextTile(ref tile.layer, tile.nLayers, ref tile.res, tile.maxNDecompLevels + 1,
                                ref tile.comp, img.nComps, ref tile.precinct, tile.maxNPrecincts);
                            break;
                        case 4: // component, precinct, resolution level, layer
                            tile.done = nextTile(ref tile.layer, tile.nLayers, ref tile.res, tile.maxNDecompLevels + 1,
                                ref tile.precinct, tile.maxNPrecincts, ref tile.comp, img.nComps);
                            break;
                    }
                } while (!tile.done && (tile.res > tile.tileComps[tile.comp].nDecompLevels
                                            || tile.precinct >= tile.tileComps[tile.comp].resLevels[tile.res].precincts.Length));
            }
            return;
            static bool nextTile(ref int v1, int m1, ref int v2, int m2, ref int v3, int m3, ref int v4, int m4)
            {
                if (++v1 != m1)
                {
                    return false;
                }

                v1 = 0;
                if (++v2 != m2)
                {
                    return false;
                }

                v2 = 0;
                if (++v3 != m3)
                {
                    return false;
                }

                v3 = 0;
                if (++v4 != m4)
                {
                    return false;
                }

                v4 = 0;
                return true;
            }
        }
        private void readCodeBlockData(JPXTileComp tileComp, JPXResLevel resLevel, int res, int sb, JPXCodeBlock cb)
        {
            ArrayPtr<int> coeff0 = new(), coeff1 = new(), coeff = new();
            ArrayPtr<bool> touched0 = new(), touched1 = new(), touched = new();
            int horiz, vert, diag, all, xorBit, horizSign, vertSign, bit, segSym, n, i, x, y0, y1, cc, cx;
            if (res > tileComp.nDecompLevels)
            { // skip the codeblock data
                if ((tileComp.codeBlockStyle & 0x04) != 0)
                {
                    for (n = 0, i = 0; i < cb.nCodingPasses; ++i)
                    {
                        n += cb.dataLen[i];
                    }
                }
                else
                {
                    n = cb.dataLen[0];
                }

                bufStr.Position += n;
                return;
            }
            if (cb.arithDecoder != null)
            {
                if ((tileComp.codeBlockStyle & 0x04) != 0)
                {
                    cb.arithDecoder.setStream(bufStr, cb.dataLen[0]);
                    cb.arithDecoder.start();
                }
                else
                {
                    cb.arithDecoder.restart(cb.dataLen[0]);
                }
            }
            else
            {
                cb.arithDecoder = new ArithmDecoder();
                cb.arithDecoder.setStream(bufStr, cb.dataLen[0]);
                cb.arithDecoder.start();
                cb.stats = new CXStats(jpxNContexts);
                cb.stats.setEntry(jpxContextSigProp, 4, 0);
                cb.stats.setEntry(jpxContextRunLength, 3, 0);
                cb.stats.setEntry(jpxContextUniform, 46, 0);
            }
            for (i = 0; i < cb.nCodingPasses; ++i)
            {
                if ((tileComp.codeBlockStyle & 0x04) != 0 && i > 0)
                {
                    cb.arithDecoder.setStream(bufStr, cb.dataLen[i]);
                    cb.arithDecoder.start();
                }

                switch (cb.nextPass)
                {
                    case jpxPassSigProp:    //----- significance propagation pass
                        for (y0 = cb.y0, coeff0.set(cb.coeffs), touched0.set(cb.touched); y0 < cb.y1;
                                y0 += 4, coeff0.inc(4 * tileComp.w), touched0.inc(4 << resLevel.codeBlockW))
                        {
                            for (x = cb.x0, coeff1.set(coeff0), touched1.set(touched0);
                                 x < cb.x1; ++x, ++coeff1, ++touched1)
                            {
                                for (y1 = 0, coeff.set(coeff1), touched.set(touched1);
                                     y1 < 4 && y0 + y1 < cb.y1;
                                     ++y1, coeff.inc(tileComp.w), touched.inc(resLevel.cbW))
                                {
                                    if (coeff[0] == 0)
                                    {
                                        horiz = vert = diag = 0;
                                        horizSign = vertSign = 2;
                                        if (x > cb.x0)
                                        {
                                            if ((cc = coeff[-1]) != 0)
                                            {
                                                ++horiz;
                                                horizSign += cc < 0 ? -1 : 1;
                                            }
                                            if (y0 + y1 > cb.y0)
                                            {
                                                diag += coeff[-tileComp.w - 1] != 0 ? 1 : 0;
                                            }

                                            if (y0 + y1 < cb.y1 - 1
                                            && ((tileComp.codeBlockStyle & 0x08) == 0 || y1 < 3))
                                            {
                                                diag += coeff[tileComp.w - 1] != 0 ? 1 : 0;
                                            }
                                        }
                                        if (x < cb.x1 - 1)
                                        {
                                            if ((cc = coeff[1]) != 0)
                                            {
                                                ++horiz;
                                                horizSign += cc < 0 ? -1 : 1;
                                            }
                                            if (y0 + y1 > cb.y0)
                                            {
                                                diag += coeff[-tileComp.w + 1] != 0 ? 1 : 0;
                                            }

                                            if (y0 + y1 < cb.y1 - 1
                                            && ((tileComp.codeBlockStyle & 0x08) == 0 || y1 < 3))
                                            {
                                                diag += coeff[tileComp.w + 1] != 0 ? 1 : 0;
                                            }
                                        }
                                        if (y0 + y1 > cb.y0 && (cc = coeff[-tileComp.w]) != 0)
                                        {
                                            ++vert;
                                            vertSign += cc < 0 ? -1 : 1;
                                        }
                                        if (y0 + y1 < cb.y1 - 1
                                        && ((tileComp.codeBlockStyle & 0x08) == 0 || y1 < 3)
                                        && (cc = coeff[tileComp.w]) != 0)
                                        {
                                            ++vert;
                                            vertSign += cc < 0 ? -1 : 1;
                                        }
                                        cx = sigPropContext[horiz][vert][diag][res == 0 ? 1 : sb];
                                        if (cx != 0)
                                        {
                                            if (cb.arithDecoder.decodeBit(cx, cb.stats) != 0)
                                            {
                                                cx = signContext[horizSign][vertSign][0];
                                                xorBit = signContext[horizSign][vertSign][1];
                                                coeff[0] = ((cb.arithDecoder.decodeBit(cx, cb.stats) ^ xorBit) != 0)
                                                    ? -1 : 1;
                                            }
                                            touched[0] = true;
                                        }
                                    }
                                }
                            }
                        }
                        ++cb.nextPass;
                        break;

                    case jpxPassMagRef:         //----- magnitude refinement pass
                        for (y0 = cb.y0, coeff0.set(cb.coeffs), touched0.set(cb.touched); y0 < cb.y1;
                                y0 += 4, coeff0.inc(4 * tileComp.w), touched0.inc(4 << resLevel.codeBlockW))
                        {
                            for (x = cb.x0, coeff1.set(coeff0), touched1.set(touched0);
                                 x < cb.x1; ++x, ++coeff1, ++touched1)
                            {
                                for (y1 = 0, coeff.set(coeff1), touched.set(touched1);
                                     y1 < 4 && y0 + y1 < cb.y1;
                                     ++y1, coeff.inc(tileComp.w), touched.inc(resLevel.cbW))
                                {
                                    if ((cc = coeff[0]) != 0 && !touched[0])
                                    {
                                        if (cc is 1 or (-1))
                                        {
                                            all = 0;
                                            if (x > cb.x0)
                                            {
                                                all += coeff[-1] != 0 ? 1 : 0;
                                                if (y0 + y1 > cb.y0)
                                                {
                                                    all += coeff[-tileComp.w - 1] != 0 ? 1 : 0;
                                                }

                                                if (y0 + y1 < cb.y1 - 1
                                                && ((tileComp.codeBlockStyle & 0x08) == 0 || y1 < 3))
                                                {
                                                    all += coeff[tileComp.w - 1] != 0 ? 1 : 0;
                                                }
                                            }
                                            if (x < cb.x1 - 1)
                                            {
                                                all += coeff[1] != 0 ? 1 : 0;
                                                if (y0 + y1 > cb.y0)
                                                {
                                                    all += coeff[-tileComp.w + 1] != 0 ? 1 : 0;
                                                }

                                                if (y0 + y1 < cb.y1 - 1
                                                && ((tileComp.codeBlockStyle & 0x08) == 0 || y1 < 3))
                                                {
                                                    all += coeff[tileComp.w + 1] != 0 ? 1 : 0;
                                                }
                                            }
                                            if (y0 + y1 > cb.y0)
                                            {
                                                all += coeff[-tileComp.w] != 0 ? 1 : 0;
                                            }

                                            if (y0 + y1 < cb.y1 - 1
                                            && ((tileComp.codeBlockStyle & 0x08) == 0 || y1 < 3))
                                            {
                                                all += coeff[tileComp.w] != 0 ? 1 : 0;
                                            }

                                            cx = all != 0 ? 15 : 14;
                                        }
                                        else
                                        {
                                            cx = 16;
                                        }

                                        bit = cb.arithDecoder.decodeBit(cx, cb.stats);
                                        coeff[0] = (cc << 1) + (cc < 0 ? -bit : +bit);
                                        touched[0] = true;
                                    }
                                }
                            }
                        }
                        ++cb.nextPass;
                        break;

                    case jpxPassCleanup:
                        for (y0 = cb.y0, coeff0.set(cb.coeffs), touched0.set(cb.touched);
                                y0 < cb.y1;
                                y0 += 4, coeff0.inc(4 * tileComp.w), touched0.inc(4 << resLevel.codeBlockW))
                        {
                            for (x = cb.x0, coeff1.set(coeff0), touched1.set(touched0);
                                 x < cb.x1; ++x, ++coeff1, ++touched1)
                            {
                                y1 = 0;
                                if (y0 + 3 < cb.y1
                                && !touched1[0]
                                && !touched1[resLevel.cbW]
                                && !touched1[2 * resLevel.cbW]
                                && !touched1[3 * resLevel.cbW]
                                && (x == cb.x0 || y0 == cb.y0 || coeff1[-tileComp.w - 1] == 0)
                                && (y0 == cb.y0 || coeff1[-tileComp.w] == 0)
                                && (x == cb.x1 - 1 || y0 == cb.y0 || coeff1[-tileComp.w + 1] == 0)
                                && (x == cb.x0 || (coeff1[-1] == 0
                                        && coeff1[tileComp.w - 1] == 0
                                        && coeff1[(2 * tileComp.w) - 1] == 0
                                        && coeff1[(3 * tileComp.w) - 1] == 0))
                                && (x == cb.x1 - 1 || (coeff1[1] == 0
                                        && coeff1[tileComp.w + 1] == 0
                                        && coeff1[(2 * tileComp.w) + 1] == 0
                                        && coeff1[(3 * tileComp.w) + 1] == 0))
                                && ((tileComp.codeBlockStyle & 0x08) != 0
                                    || ((x == cb.x0 || y0 + 4 == cb.y1 || coeff1[(4 * tileComp.w) - 1] == 0)
                                        && (y0 + 4 == cb.y1 || coeff1[4 * tileComp.w] == 0)
                                        && (x == cb.x1 - 1 || y0 + 4 == cb.y1
                                            || coeff1[(4 * tileComp.w) + 1] == 0))))
                                {
                                    if (cb.arithDecoder.decodeBit(jpxContextRunLength, cb.stats) != 0)
                                    {
                                        y1 = cb.arithDecoder.decodeBit(jpxContextUniform, cb.stats);
                                        y1 = (y1 << 1) | cb.arithDecoder.decodeBit(jpxContextUniform, cb.stats);
                                        coeff.set(coeff1, y1 * tileComp.w);
                                        cx = signContext[2][2][0];
                                        xorBit = signContext[2][2][1];
                                        coeff[0] = ((cb.arithDecoder.decodeBit(cx, cb.stats)
                                                        ^ xorBit) != 0) ? -1 : 1;
                                        ++y1;
                                    }
                                    else
                                    {
                                        y1 = 4;
                                    }
                                }
                                for (coeff.set(coeff1, y1 * tileComp.w),
                                    touched.set(touched1, y1 << resLevel.codeBlockW);
                                        y1 < 4 && y0 + y1 < cb.y1;
                                        ++y1, coeff.inc(tileComp.w), touched.inc(resLevel.cbW))
                                {
                                    if (!touched[0])
                                    {
                                        horiz = vert = diag = 0;
                                        horizSign = vertSign = 2;
                                        if (x > cb.x0)
                                        {
                                            if ((cc = coeff[-1]) != 0)
                                            {
                                                ++horiz;
                                                horizSign += cc < 0 ? -1 : 1;
                                            }
                                            if (y0 + y1 > cb.y0)
                                            {
                                                diag += coeff[-tileComp.w - 1] != 0 ? 1 : 0;
                                            }

                                            if (y0 + y1 < cb.y1 - 1
                                            && ((tileComp.codeBlockStyle & 0x08) == 0 || y1 < 3))
                                            {
                                                diag += coeff[tileComp.w - 1] != 0 ? 1 : 0;
                                            }
                                        }
                                        if (x < cb.x1 - 1)
                                        {
                                            if ((cc = coeff[1]) != 0)
                                            {
                                                ++horiz;
                                                horizSign += cc < 0 ? -1 : 1;
                                            }
                                            if (y0 + y1 > cb.y0)
                                            {
                                                diag += coeff[-tileComp.w + 1] != 0 ? 1 : 0;
                                            }

                                            if (y0 + y1 < cb.y1 - 1
                                            && ((tileComp.codeBlockStyle & 0x08) == 0 || y1 < 3))
                                            {
                                                diag += coeff[tileComp.w + 1] != 0 ? 1 : 0;
                                            }
                                        }
                                        if (y0 + y1 > cb.y0)
                                        {
                                            if ((cc = coeff[-tileComp.w]) != 0)
                                            {
                                                ++vert;
                                                vertSign += cc < 0 ? -1 : 1;
                                            }
                                        }

                                        if (y0 + y1 < cb.y1 - 1
                                        && ((tileComp.codeBlockStyle & 0x08) == 0 || y1 < 3)
                                        && (cc = coeff[tileComp.w]) != 0)
                                        {
                                            ++vert;
                                            vertSign += cc < 0 ? -1 : 1;
                                        }
                                        cx = sigPropContext[horiz][vert][diag][res == 0 ? 1 : sb];
                                        if (cb.arithDecoder.decodeBit(cx, cb.stats) != 0)
                                        {
                                            cx = signContext[horizSign][vertSign][0];
                                            xorBit = signContext[horizSign][vertSign][1];
                                            coeff[0] = ((cb.arithDecoder.decodeBit(cx, cb.stats) ^ xorBit) != 0)
                                                ? -1 : 1;
                                        }
                                    }
                                    else
                                    {
                                        touched[0] = false;
                                    }
                                }
                            }
                        }
                        ++cb.len;
                        // look for a segmentation symbol
                        if ((tileComp.codeBlockStyle & 0x20) != 0)
                        {
                            segSym = cb.arithDecoder.decodeBit(jpxContextUniform, cb.stats) << 3;
                            segSym |= cb.arithDecoder.decodeBit(jpxContextUniform, cb.stats) << 2;
                            segSym |= cb.arithDecoder.decodeBit(jpxContextUniform, cb.stats) << 1;
                            segSym |= cb.arithDecoder.decodeBit(jpxContextUniform, cb.stats);
                            if (segSym != 0x0a) // in theory this should be a fatal error, but it seems to be problematic
                            {
                                _ = error("Missing or invalid segmentation symbol in JPX stream", false);
                            }
                        }
                        cb.nextPass = jpxPassSigProp;
                        break;
                }
                if ((tileComp.codeBlockStyle & 0x02) != 0)
                {
                    cb.stats.reset();
                    cb.stats.setEntry(jpxContextSigProp, 4, 0);
                    cb.stats.setEntry(jpxContextRunLength, 3, 0);
                    cb.stats.setEntry(jpxContextUniform, 46, 0);
                }
                if ((tileComp.codeBlockStyle & 0x04) != 0)
                {
                    cb.arithDecoder.cleanup();
                }
            }
            cb.arithDecoder.cleanup();
        }
        private void inverseTransform(JPXTileComp tileComp)
        {
            ArrayPtr<int> coeff0 = new(), coeff = new();
            ArrayPtr<bool> touched0 = new(), touched = new();
            int eps, shift, r, x, y, shift2;
            double mu;
            JPXResLevel resLevel = tileComp.resLevels[0];   //----- (NL)LL subband (resolution level 0)
            int qStyle = tileComp.quantStyle & 0x1f;      // i-quant parameters
            int guard = (tileComp.quantStyle >> 5) & 7;
            if (qStyle == 0)
            {
                eps = (tileComp.quantSteps[0] >> 3) & 0x1f;
                shift = guard + eps - 1;
                mu = 0;
            }
            else
            {
                shift = guard - 1 + tileComp.prec;
                mu = (0x800 + (tileComp.quantSteps[0] & 0x7ff)) / 2048.0;
            }
            if (tileComp.transform == 0)
            {
                shift += fracBits - tileComp.prec;
            }
            // do fixed point adjustment and dequantization on (NL)LL
            for (int pre = 0; pre < resLevel.precincts.Length; ++pre)
            {
                JPXSubband subband = resLevel.precincts[pre].subbands[0];
                for (int cbY = 0, cbi = 0; cbY < subband.nYCBs; ++cbY)
                {
                    for (int cbX = 0; cbX < subband.nXCBs; ++cbX, cbi++)
                    {
                        JPXCodeBlock cb = subband.cbs[cbi];
                        for (y = cb.y0, coeff0.set(cb.coeffs), touched0.set(cb.touched);
                                     y < cb.y1; ++y, coeff0.inc(tileComp.w), touched0.inc(resLevel.cbW))
                        {
                            for (x = cb.x0, coeff.set(coeff0), touched.set(touched0);
                                             x < cb.x1; ++x, ++coeff, ++touched)
                            {
                                int val = coeff[0];
                                if (val != 0)
                                {
                                    shift2 = shift - (cb.nZeroBitPlanes + cb.len + (touched[0] ? 1 : 0));
                                    if (shift2 > 0)
                                    {
                                        val = (val << shift2) + (val < 0
                                            ? -(1 << (shift2 - 1)) : (1 << (shift2 - 1)));
                                    }
                                    else
                                    {
                                        val >>= -shift2;
                                    }

                                    if (qStyle != 0)
                                    {
                                        val = (int)(mu * val);
                                    }
                                    else if (tileComp.transform == 0)
                                    {
                                        val &= -1 << (fracBits - tileComp.prec);
                                    }
                                }
                                coeff[0] = val;
                            }
                        }
                    }
                }
            }
            //----- IDWT for each level
            // (n)LL is already in the upper-left corner of the
            // tile-component data array -- interleave with (n)HL/LH/HH
            // and inverse transform to get (n-1)LL, which will be stored
            // in the upper-left corner of the tile-component data array
            for (r = 1; r <= tileComp.nDecompLevels; ++r)
            {
                inverseTransformLevel(tileComp, r, tileComp.resLevels[r]);
            }
        }
        private void inverseTransformLevel(JPXTileComp tileComp, int r, JPXResLevel resLevel)
        {
            ArrayPtr<int> coeff0 = new(), coeff = new();
            ArrayPtr<bool> touched0 = new(), touched = new();
            double mu;
            int shift, t, x, y;
            int qStyle = tileComp.quantStyle & 0x1f;
            int guard = (tileComp.quantStyle >> 5) & 7, eps;
            int nx1 = resLevel.bx1[1] - resLevel.bx0[1];
            int nx2 = nx1 + resLevel.bx1[0] - resLevel.bx0[0];
            int ny1 = resLevel.by1[0] - resLevel.by0[0];
            int ny2 = ny1 + resLevel.by1[1] - resLevel.by0[1];
            if (nx2 == 0 || ny2 == 0)
            {
                return;
            }

            for (int sb = 0; sb < 3; ++sb)
            {    //----- fixed-point adjustment and dequantization
                if (qStyle == 0)
                {          // i-quant parameters
                    eps = (tileComp.quantSteps[(3 * r) - 2 + sb] >> 3) & 0x1f;
                    shift = guard + eps - 1;
                    mu = 0; // make gcc happy
                }
                else
                {
                    shift = guard + tileComp.prec;
                    if (sb == 2)
                    {
                        ++shift;
                    }

                    t = tileComp.quantSteps[qStyle == 1 ? 0 : ((3 * r) - 2 + sb)];
                    mu = (0x800 + (t & 0x7ff)) / 2048.0;
                }
                if (tileComp.transform == 0)
                {
                    shift += fracBits - tileComp.prec;
                }
                // fixed point adjustment and dequantization
                for (int pre = 0; pre < resLevel.precincts.Length; ++pre)
                {
                    JPXPrecinct precinct = resLevel.precincts[pre];
                    JPXSubband subband = precinct.subbands[sb];
                    for (int cbi = 0, cbY = 0; cbY < subband.nYCBs; ++cbY)
                    {
                        for (int cbX = 0; cbX < subband.nXCBs; ++cbX, cbi++)
                        {
                            JPXCodeBlock cb = subband.cbs[cbi];
                            for (y = cb.y0, coeff0.set(cb.coeffs), touched0.set(cb.touched);
                                 y < cb.y1; ++y, coeff0.inc(tileComp.w), touched0.inc(resLevel.cbW))
                            {
                                for (x = cb.x0, coeff.set(coeff0), touched.set(touched0);
                                        x < cb.x1; ++x, ++coeff.off, ++touched.off)
                                {
                                    int val = coeff.data[coeff.off];// coeff[0];
                                    if (val != 0)
                                    {
                                        int shift2 = shift - (cb.nZeroBitPlanes + cb.len + (touched[0] ? 1 : 0));
                                        if (shift2 > 0)
                                        {
                                            val = (val << shift2)
                                                + (val < 0 ? -(1 << (shift2 - 1)) : (1 << (shift2 - 1)));
                                        }
                                        else
                                        {
                                            val >>= -shift2;
                                        }

                                        if (qStyle != 0)
                                        {
                                            val = (int)(val * mu);
                                        }
                                        else if (tileComp.transform == 0)
                                        {
                                            val &= -1 << (fracBits - tileComp.prec);
                                        }
                                    }
                                    coeff.data[coeff.off] = val;// coeff[0]
                                }
                            }
                        }
                    }
                }
            }
            //----- inverse transform
            ArrayPtr<int> dataPtr = new(tileComp.data),
                            bufPtr = new(tileComp.buf);
            int offset = 3 + (resLevel.x0 & 1);                                     // horizontal (row) transforms
            int o1, o2;
            (o1, o2) = resLevel.bx0[0] == resLevel.bx0[1] ? (0, 1) : (1, 0);
            for (y = 0; y < ny2; ++y, dataPtr.off += tileComp.w)
            {
                for (x = 0, bufPtr.off = offset + o1; x < nx1; ++x, bufPtr.off += 2)
                {
                    bufPtr.data[bufPtr.off] = dataPtr.data[dataPtr.off + x];        // fetch LL/LH
                }

                for (x = nx1, bufPtr.off = offset + o2; x < nx2; ++x, bufPtr.off += 2)
                {
                    bufPtr.data[bufPtr.off] = dataPtr.data[dataPtr.off + x];        // fetch LL/LH
                }

                inverseTransform1D(tileComp, tileComp.buf, offset, nx2);
                Array.Copy(bufPtr.data, offset, dataPtr.data, dataPtr.off, nx2);
            }
            offset = 3 + (resLevel.y0 & 1);                                         // vertical (column) transforms
            (o1, o2) = resLevel.by0[0] == resLevel.by0[1] ? (0, 1) : (1, 0);
            for (x = 0, dataPtr.off = 0; x < nx2; ++x, ++dataPtr.off)
            {
                for (y = 0, bufPtr.off = offset + o1; y < ny1; ++y, bufPtr.off += 2)
                {
                    bufPtr.data[bufPtr.off] = dataPtr.data[dataPtr.off + (y * tileComp.w)];   // fetch LL/HL
                }

                for (y = ny1, bufPtr.off = offset + o2; y < ny2; ++y, bufPtr.off += 2)
                {
                    bufPtr.data[bufPtr.off] = dataPtr.data[dataPtr.off + (y * tileComp.w)];   // fetch LH/HH
                }

                inverseTransform1D(tileComp, tileComp.buf, offset, ny2);
                for (y = 0, bufPtr.off = offset; y < ny2; ++y, ++bufPtr.off)
                {
                    dataPtr.data[dataPtr.off + (y * tileComp.w)] = bufPtr.data[bufPtr.off];
                }
            }
        }
        private void inverseTransform1D(JPXTileComp tileComp, int[] data, int offset, int n)
        {
            if (n != 1)
            {
                int end = offset + n, i;
                data[end] = data[end - 2];
                if (n == 2)
                {
                    data[end + 1] = data[end + 3] = data[offset + 1];
                    data[end + 2] = data[offset];
                }
                else
                {
                    data[end + 1] = data[end - 3];
                    if (n == 3)
                    {
                        data[end + 2] = data[offset + 1];
                        data[end + 3] = data[offset + 2];
                    }
                    else
                    {
                        data[end + 2] = data[end - 4];
                        data[end + 3] = data[(n == 4) ? offset + 1 : end - 5];
                    }
                }
                data[offset - 1] = data[offset + 1];    //----- extend left
                data[offset - 2] = data[offset + 2];
                data[offset - 3] = data[offset + 3];
                if (offset == 4)
                {
                    data[0] = data[offset + 4];
                }

                if (tileComp.transform == 0)
                {              //----- 9-7 irreversible filter
                    for (i = 1; i <= end + 2; i += 2)       // step 1 (even)
                    {
                        data[i] = (int)(idwtKappa * data[i]);
                    }

                    for (i = 0; i <= end + 3; i += 2)       // step 2 (odd)
                    {
                        data[i] = (int)(idwtIKappa * data[i]);
                    }

                    for (i = 1; i <= end + 2; i += 2)       // step 3 (even)
                    {
                        data[i] = (int)(data[i] - (idwtDelta * (data[i - 1] + data[i + 1])));
                    }

                    for (i = 2; i <= end + 1; i += 2)       // step 4 (odd)
                    {
                        data[i] = (int)(data[i] - (idwtGamma * (data[i - 1] + data[i + 1])));
                    }

                    for (i = 3; i <= end; i += 2)           // step 5 (even)
                    {
                        data[i] = (int)(data[i] - (idwtBeta * (data[i - 1] + data[i + 1])));
                    }

                    for (i = 4; i <= end - 1; i += 2)       // step 6 (odd)
                    {
                        data[i] = (int)(data[i] - (idwtAlpha * (data[i - 1] + data[i + 1])));
                    }
                }
                else
                {                                      //----- 5-3 reversible filter
                    for (i = 3; i <= end; i += 2)           // step 1 (even)
                    {
                        data[i] -= (data[i - 1] + data[i + 1] + 2) >> 2;
                    }

                    for (i = 4; i < end; i += 2)            // step 2 (odd)
                    {
                        data[i] += (data[i - 1] + data[i + 1]) >> 1;
                    }
                }
            }
            else if (offset == 4)
            {
                data[0] >>= 1;
            }
        }
        private void inverseMultiCompAndDC(JPXTile tile)
        {
            int t, j, x, y;
            if (tile.multiComp == 1)
            {                  //----- inverse multi-component transform
                if (img.nComps < 3
                || tile.tileComps[0].hSep != tile.tileComps[1].hSep
                || tile.tileComps[0].vSep != tile.tileComps[1].vSep
                || tile.tileComps[1].hSep != tile.tileComps[2].hSep
                || tile.tileComps[1].vSep != tile.tileComps[2].vSep)
                {
                    return;
                }

                int[] b0 = tile.tileComps[0].data, b1 = tile.tileComps[1].data, b2 = tile.tileComps[2].data;
                bool irr = tile.tileComps[0].transform == 0;
                for (j = y = 0; y < tile.tileComps[0].h; ++y)
                {
                    for (x = 0; x < tile.tileComps[0].w; ++x, ++j)
                    {
                        int d0 = b0[j], d1 = b1[j], d2 = b2[j];
                        if (irr)
                        { // inverse irreversible multiple component transform
                            b0[j] = (int)(d0 + (1.402 * d2) + 0.5);
                            b1[j] = (int)(d0 - (0.34413 * d1) - (0.71414 * d2) + 0.5);
                            b2[j] = (int)(d0 + (1.772 * d1) + 0.5);
                        }
                        else
                        {
                            b1[j] = t = d0 - ((d2 + d1) >> 2);
                            b0[j] = d2 + t;
                            b2[j] = d1 + t;
                        }
                    }
                }
            }
            //----- DC level shift
            for (int comp = 0; comp < img.nComps; ++comp)
            {
                JPXTileComp tileComp = tile.tileComps[comp];
                ArrayPtr<int> dataPtr = new(tileComp.data);
                if (tileComp.sgned)
                {           // signed: clip
                    int minVal = -(1 << (tileComp.prec - 1));
                    int maxVal = (1 << (tileComp.prec - 1)) - 1;
                    for (y = 0; y < tileComp.h; ++y)
                    {
                        for (x = 0; x < tileComp.w; ++x, ++dataPtr)
                        {
                            int coeff = dataPtr[0];
                            if (tileComp.transform == 0)
                            {
                                coeff >>= fracBits - tileComp.prec;
                            }

                            dataPtr[0] = Math.Min(Math.Max(coeff, minVal), maxVal);
                        }
                    }
                }
                else
                {                      // unsigned: inverse DC level shift and clip
                    int maxVal = (1 << tileComp.prec) - 1;
                    int zeroVal = 1 << (tileComp.prec - 1);
                    int shift = fracBits - tileComp.prec;
                    int rounding = 1 << (shift - 1);
                    for (y = 0; y < tileComp.h; ++y)
                    {
                        for (x = 0; x < tileComp.w; ++x, ++dataPtr)
                        {
                            int coeff = dataPtr[0];
                            if (tileComp.transform == 0)
                            {
                                coeff = (coeff + rounding) >> shift;
                            }

                            dataPtr[0] = Math.Min(Math.Max(coeff + zeroVal, 0), maxVal);
                        }
                    }
                }
            }
        }
        private bool readBoxHdr(ref int boxType, ref int boxLen, ref int dataLen)
        {
            (boxLen, boxType) = (GetInt(4), GetInt(4));
            if (boxLen == 1)
            {
                bufStr.Position += 4;
                boxLen = GetInt(4);
                dataLen = boxLen - 16;
            }
            else
            {
                dataLen = (boxLen == 0) ? 0 : boxLen - 8;
            }

            return boxType != -1;
        }
        private bool readMarkerHdr(ref int segType, ref int segLen)
        {
            int c;
            do
            {
                do
                {
                    if ((c = bufStr.ReadByte()) == -1)
                    {
                        return false;
                    }
                } while (c != 0xff);
                do
                {
                    if ((c = bufStr.ReadByte()) == -1)
                    {
                        return false;
                    }
                } while (c == 0xff);
            } while (c == 0x00);
            segType = c;
            if (c is (>= 0x30 and <= 0x3f) or 0x4f or 0x92 or 0x93 or 0xd9)
            {
                segLen = 0;
                return true;
            }
            return (segLen = GetInt(2)) != -1;
        }
        private void startBitBuf(int byteCountA)
        {
            bitBufLen = 0;
            bitBufSkip = false;
            byteCount = byteCountA;
        }
        private bool readBits(int nBits, ref int x)
        {
            for (int c; bitBufLen < nBits;)
            {
                if (byteCount == 0 || (c = bufStr.ReadByte()) == -1)
                {
                    return false;
                }

                --byteCount;
                if (bitBufSkip)
                {
                    bitBuf = (bitBuf << 7) | (c & 0x7f);
                    bitBufLen += 7;
                }
                else
                {
                    bitBuf = (bitBuf << 8) | (c & 0xff);
                    bitBufLen += 8;
                }
                bitBufSkip = c == 0xff;
            }
            x = (bitBuf >> (bitBufLen - nBits)) & ((1 << nBits) - 1);
            bitBufLen -= nBits;
            return true;
        }
        private void skipSOP()
        {
            // SOP occurs at the start of the packet header, so we don't need to
            // worry about bit-stuff prior to it
            long pos = bufStr.Position;
            int b1 = bufStr.ReadByte(), b2 = bufStr.ReadByte();
            bufStr.Position = pos;
            if (byteCount >= 6 && b1 == 0xff && b2 == 0x91)
            {
                bufStr.Position += 6;
                byteCount -= 6;
                bitBufLen = 0;
                bitBufSkip = false;
            }
        }
        private void skipEPH()
        {
            int k = bitBufSkip ? 1 : 0;
            long pos = bufStr.Position;
            byte[] buf = new byte[k + 2];
            _ = bufStr.Read(buf, 0, buf.Length);
            bufStr.Position = pos;
            if (byteCount >= k + 2 && buf[k] == 0xff && buf[k + 1] == 0x92)
            {
                bufStr.Position += k + 2;
                byteCount -= k + 2;
                bitBufLen = 0;
                bitBufSkip = false;
            }
        }
        private int finishBitBuf()
        {
            if (bitBufSkip)
            {
                _ = bufStr.ReadByte();
                --byteCount;
            }
            return byteCount;
        }
        private int nComps = 0;                     // number of components
        private readonly JPXPalette palette = new(); // the palette
        private bool havePalette = false;           // set if a palette has been found
        private JPXImage img = new();      // JPEG2000 decoder data
        private int bitBuf = 0;                     // buffer for bit reads
        private int bitBufLen = 0;                  // number of bits in bitBuf
        private int byteCount = 0;                  // number of available bytes left
        private bool bitBufSkip = false;            // true if next bit should be skipped (for bit stuffing)
    }
    public class PBoxJBig2 : XPdfStream
    {
        internal class JB2Bmp
        {
            internal int height, width, stride;
            internal byte[] bitmap;
            internal JB2Bmp(int width, int height)
            {
                this.height = height;
                this.width = width;
                stride = (width + 7) >> 3;
                bitmap = new byte[this.height * stride];
            }
            internal byte getPixel(int x, int y)
            {
                int byteIndex = getIdx(x, y), bitOffset = x & 0x07, toShift = 7 - bitOffset;
                return (byte)((bitmap[byteIndex] >> toShift) & 0x01);
            }
            internal void setPixel(int x, int y, byte pixelValue)
            {
                int byteIndex = getIdx(x, y), bitOffset = x & 0x07, shift = 7 - bitOffset;
                bitmap[byteIndex] = (byte)(bitmap[byteIndex] | (pixelValue << shift));
            }
            internal int getIdx(int x, int y) => (y * stride) + (x >> 3);
            internal int getInt(int index) => bitmap[index] & 0xff;
            internal int Length => bitmap.Length;
            internal void fillBitmap(byte fillByte)
            {
                for (int i = bitmap.Length - 1; i >= 0; i--)
                {
                    bitmap[i] = fillByte;
                }
            }
            internal JB2Bmp extract(Rectangle roi)
            {
                JB2Bmp dst = new(roi.Width, roi.Height);
                int upShift = roi.X & 0x07, downShift = 8 - upShift, dstLnStart = 0,
                    srcLnStart = getIdx(roi.X, roi.Y), pad = (8 - dst.width) & 0x07,
                    srcLnEnd = getIdx(roi.X + roi.Width - 1, roi.Y);
                bool usePadding = dst.stride == srcLnEnd + 1 - srcLnStart;
                for (int y = roi.Y; y < roi.Bottom; y++)
                {
                    int srcIdx = srcLnStart, dstIdx = dstLnStart;
                    if (srcLnStart == srcLnEnd)
                    {
                        byte pixels = (byte)(bitmap[srcIdx] << upShift);
                        dst.bitmap[dstIdx] = unpad(pad, pixels);
                    }
                    else if (upShift == 0)
                    {
                        for (int x = srcLnStart; x <= srcLnEnd; x++)
                        {
                            byte value = bitmap[srcIdx++];
                            if (x == srcLnEnd && usePadding)
                            {
                                value = unpad(pad, value);
                            }

                            dst.bitmap[dstIdx++] = value;
                        }
                    }
                    else
                    {
                        for (int x = srcLnStart; x < srcLnEnd; x++)
                        {
                            if (srcIdx + 1 < Length)
                            {
                                bool isLastByte = x + 1 == srcLnEnd;
                                byte value = (byte)(((uint)bitmap[srcIdx++] << upShift)
                                                   | (((uint)bitmap[srcIdx] & 0xff) >> downShift));
                                if (isLastByte && !usePadding)
                                {
                                    value = unpad(pad, value);
                                }

                                dst.bitmap[dstIdx++] = value;
                                if (isLastByte && usePadding)
                                {
                                    value = unpad(pad, (byte)((bitmap[srcIdx] & 0xff) << upShift));
                                    dst.bitmap[dstIdx] = value;
                                }
                            }
                            else
                            {
                                byte value = (byte)((bitmap[srcIdx++] << upShift) & 0xff);
                                dst.bitmap[dstIdx++] = value;
                            }
                        }
                    }
                    srcLnStart += stride;
                    srcLnEnd += stride;
                    dstLnStart += dst.stride;
                }
                return dst;
            }
            private static byte unpad(int padding, byte value) => (byte)((uint)value >> padding << padding);
            internal static byte combineBytes(byte value1, byte value2, ComboOper op)
            {
                return op switch
                {
                    ComboOper.OR => (byte)(value2 | value1),
                    ComboOper.AND => (byte)(value2 & value1),
                    ComboOper.XOR => (byte)(value2 ^ value1),
                    ComboOper.XNOR => (byte)~(value1 ^ value2),
                    _ => value2,
                };
            }
            internal void blit(JB2Bmp dst, int x, int y, ComboOper op)
            {
                int lnStart = 0, srcStart = 0, srcEnd = stride - 1;
                if (x < 0)
                {
                    srcStart = -x;
                    x = 0;
                }
                else if (x + width > dst.width)
                {
                    srcEnd -= width + x - dst.width;
                }

                if (y < 0)
                {
                    lnStart = -y;
                    y = 0;
                    srcStart += stride;
                    srcEnd += stride;
                }
                else if (y + height > dst.height)
                {
                    lnStart = height + y - dst.height;
                }

                int shiftVal1 = x & 0x07, shiftVal2 = 8 - shiftVal1,
                    padding = width & 0x07, toShift = shiftVal2 - padding,
                    dstStart = dst.getIdx(x, y),
                    lastLine = Math.Min(height, lnStart + dst.height);
                bool useShift = (shiftVal2 & 0x07) != 0;
                bool specialCase = width <= ((srcEnd - srcStart) << 3) + shiftVal2;
                if (!useShift)
                {
                    int length = srcEnd - srcStart + 1; // srcEndIdx is inclusive
                    int srcStartOffset = srcStart, dstStartOffset = dstStart;
                    for (int lines = lastLine - lnStart; lines > 0; lines--,
                            srcStartOffset += stride, dstStartOffset += dst.stride)
                    {
                        int srcIdx = srcStartOffset, dstIdx = dstStartOffset, count = length;
                        if (ComboOper.REPLACE == op)
                        {
                            Array.Copy(bitmap, srcIdx, dst.bitmap, dstIdx, count);
                        }
                        else
                        {
                            while (count-- > 0)
                            {
                                dst.bitmap[dstIdx] = combineBytes(bitmap[srcIdx++], dst.bitmap[dstIdx++], op);
                            }
                        }
                    }
                }
                else if (specialCase)
                {
                    for (int dstLine = lnStart; dstLine < lastLine; dstLine++,
                            dstStart += dst.stride, srcStart += stride, srcEnd += stride)
                    {
                        int register = 0, dstIdx = dstStart;
                        for (int srcIdx = srcStart; srcIdx <= srcEnd; srcIdx++)
                        {
                            register = (register | (bitmap[srcIdx] & 0xff)) << shiftVal2;
                            byte oldByte = dst.bitmap[dstIdx], newByte = (byte)(register >> 8);
                            if (srcIdx == srcEnd)
                            {
                                newByte = unpad(toShift, newByte);
                            }

                            dst.bitmap[dstIdx++] = combineBytes(oldByte, newByte, op);
                            register <<= shiftVal1;
                        }
                    }
                }
                else
                {
                    for (int dstLine = lnStart; dstLine < lastLine; dstLine++, dstStart += dst
                            .stride, srcStart += stride, srcEnd += stride)
                    {
                        int register = 0, dstIdx = dstStart;
                        for (int srcIdx = srcStart; srcIdx <= srcEnd; srcIdx++)
                        {
                            register = (register | (bitmap[srcIdx] & 0xff)) << shiftVal2;
                            byte oldByte = dst.bitmap[dstIdx], newByte = (byte)((uint)register >> 8);
                            dst.bitmap[dstIdx++] = combineBytes(oldByte, newByte, op);
                            register <<= shiftVal1;
                            if (srcIdx == srcEnd)
                            {
                                newByte = (byte)((uint)register >> (8 - shiftVal2));
                                if (padding != 0)
                                {
                                    newByte = unpad(8 + toShift, newByte);
                                }

                                oldByte = dst.bitmap[dstIdx];
                                dst.bitmap[dstIdx] = combineBytes(oldByte, newByte, op);
                            }
                        }
                    }
                }
            }
            internal Image ToImage()
            {
                Bitmap bmp = new(width, height, PixelFormat.Format1bppIndexed);
                BitmapData bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                            ImageLockMode.ReadWrite, PixelFormat.Format1bppIndexed);
                byte[] tmp = new byte[stride];
                for (int y = 0, len = tmp.Length; y < bmp.Height; y++)
                {
                    Array.Clear(tmp, 0, tmp.Length);
                    for (int x = 0; x < len; x++)
                    {
                        tmp[x] = (byte)~bitmap[(y * len) + x];
                    }

                    Marshal.Copy(tmp, 0, bd.Scan0 + (y * bd.Stride), tmp.Length);
                }
                bmp.UnlockBits(bd);
                return bmp;
            }
        }
        internal class HuffmanTable
        {
            internal class Code
            {
                internal int prefixLen, rngLen, rngLow, code = -1;
                internal bool isLowRng;
                internal Code(int prefixLength, int rangeLength, int rangeLow, bool isLowerRange)
                {
                    (prefixLen, rngLen, rngLow, isLowRng)
                        = (prefixLength, rangeLength, rangeLow, isLowerRange);
                }
                internal Code()
                {
                }
                internal Code(Code oth) :
                    this(oth.prefixLen, oth.rngLen, oth.rngLow, oth.isLowRng)
                { }
                internal virtual long decode(ImgStream iis) => long.MaxValue;
            }
            internal class InternalNode : Code
            {
                private readonly int depth;
                private Code zero, one;
                internal InternalNode(int depth = 0)
                {
                    this.depth = depth;
                }
                internal void append(Code c)
                {
                    if (c.prefixLen == 0)            // ignore unused codes
                    {
                        return;
                    }

                    int shift = c.prefixLen - 1 - depth, bit = (c.code >> shift) & 1;
                    if (shift < 0)
                    {
                        throw new Exception("Negative shifting is not possible.");
                    }

                    if (bit == 1)
                    {
                        setNode(ref one, c, shift);
                    }
                    else
                    {
                        setNode(ref zero, c, shift);
                    }
                }
                private void setNode(ref Code dst, Code c, int shift)
                {
                    Code n = null;
                    if (shift == 0)
                    {
                        n = (c.rngLen == -1) ? new Code(c) : new ValueNode(c);
                    }
                    else
                    {
                        if (dst == null)
                        {
                            n = new InternalNode(depth + 1);
                        } ((InternalNode)(n ?? dst)).append(c);
                    }
                    if (n != null && dst != null)
                    {
                        throw new Exception("Node already have a value");
                    }

                    dst ??= n;
                }
                internal override long decode(ImgStream iis) => (iis.readBits(1) == 0 ? zero : one).decode(iis);
            }
            internal class ValueNode : Code
            {
                internal ValueNode(Code c) : base(c) { }
                internal override long decode(ImgStream iis) => rngLow + (isLowRng ? -iis.readBits(rngLen) : iis.readBits(rngLen));
            }
            private static readonly int[][][] TABLES = [	// B1
				[
                    [1, 4, 0], [2, 8, 16], [3, 16, 272], [3, 32, 65808]
                ],  [	// B2
						[1, 0, 0], [2, 0, 1], [3, 0, 2], [4, 3, 3], [5, 6, 11], [6, 32, 75], [6, -1, 0]
                ], [ // B3
						[8, 8, -256], [1, 0, 0], [2, 0, 1], [3, 0, 2], [4, 3, 3], [5, 6, 11], [8, 32, -257, 999], [7, 32, 75], [6, -1, 0]
                ], [ // B4
            			[1, 0, 1], [2, 0, 2], [3, 0, 3], [4, 3, 4], [5, 6, 12], [5, 32, 76]
                ], [ // B5
            			[7, 8, -255], [1, 0, 1], [2, 0, 2], [3, 0, 3], [4, 3, 4], [5, 6, 12], [7, 32, -256, 999], [6, 32, 76]
                ], [ // B6
						[5, 10, -2048], [4, 9, -1024], [4, 8, -512], [4, 7, -256], [5, 6, -128], [5, 5, -64], [4, 5, -32], [2, 7, 0], [3, 7, 128], [3, 8, 256], [4, 9, 512], [4, 10, 1024], [6, 32, -2049, 999], [6, 32, 2048]
                ], [ // B7
						[4, 9, -1024], [3, 8, -512], [4, 7, -256], [5, 6, -128], [5, 5, -64], [4, 5, -32], [4, 5, 0], [5, 5, 32], [5, 6, 64], [4, 7, 128], [3, 8, 256], [3, 9, 512], [3, 10, 1024], [5, 32, -1025, 999], [5, 32, 2048]
                ], [ // B8
						[8, 3, -15], [9, 1, -7], [8, 1, -5], [9, 0, -3], [7, 0, -2], [4, 0, -1], [2, 1, 0], [5, 0, 2], [6, 0, 3], [3, 4, 4], [6, 1, 20], [4, 4, 22], [4, 5, 38], [5, 6, 70], [5, 7, 134], [6, 7, 262], [7, 8, 390], [6, 10, 646], [9, 32, -16, 999], [9, 32, 1670], [2, -1, 0]
                ], [ // B9
            			[8, 4, -31], [9, 2, -15], [8, 2, -11], [9, 1, -7], [7, 1, -5], [4, 1, -3], [3, 1, -1], [3, 1, 1], [5, 1, 3], [6, 1, 5], [3, 5, 7], [6, 2, 39], [4, 5, 43], [4, 6, 75], [5, 7, 139], [5, 8, 267], [6, 8, 523], [7, 9, 779], [6, 11, 1291], [9, 32, -32, 999], [9, 32, 3339], [2, -1, 0]
                ], [ // B10
            			[7, 4, -21], [8, 0, -5], [7, 0, -4], [5, 0, -3], [2, 2, -2], [5, 0, 2], [6, 0, 3], [7, 0, 4], [8, 0, 5], [2, 6, 6], [5, 5, 70], [6, 5, 102], [6, 6, 134], [6, 7, 198], [6, 8, 326], [6, 9, 582], [6, 10, 1094], [7, 11, 2118], [8, 32, -22, 999], [8, 32, 4166], [2, -1, 0]
                ], [ // B11
            			[1, 0, 1], [2, 1, 2], [4, 0, 4], [4, 1, 5], [5, 1, 7], [5, 2, 9], [6, 2, 13], [7, 2, 17], [7, 3, 21], [7, 4, 29], [7, 5, 45], [7, 6, 77], [7, 32, 141]
                    ], [ // B12
            			[1, 0, 1], [2, 0, 2], [3, 1, 3], [5, 0, 5], [5, 1, 6], [6, 1, 8], [7, 0, 10], [7, 1, 11], [7, 2, 13], [7, 3, 17], [7, 4, 25], [8, 5, 41], [8, 32, 73] //
				], [ // B13
            			[1, 0, 1], [3, 0, 2], [4, 0, 3], [5, 0, 4], [4, 1, 5], [3, 3, 7], [6, 1, 15], [6, 2, 17], [6, 3, 21], [6, 4, 29], [6, 5, 45], [7, 6, 77], [7, 32, 141]
                ], [ // B14
            			[3, 0, -2], [3, 0, -1], [1, 0, 0], [3, 0, 1], [3, 0, 2] //
				], [ // B15
						[7, 4, -24], [6, 2, -8], [5, 1, -4], [4, 0, -2], [3, 0, -1], [1, 0, 0], [3, 0, 1], [4, 0, 2], [5, 1, 3], [6, 2, 5], [7, 4, 9], [7, 32, -25, 999], [7, 32, 25]
            ] ];
            private static readonly HuffmanTable[] STANDARD_TABLES = new HuffmanTable[TABLES.Length];
            private readonly InternalNode rootNode = new();
            internal void initTree(List<Code> codeTable)
            {
                int maxPrefixLength = codeTable.Max(x => x.prefixLen); /* Annex B.3 1) - build the histogram */
                int[] lenCount = new int[maxPrefixLength + 1];
                foreach (Code c in codeTable)
                {
                    lenCount[c.prefixLen]++;
                }

                int[] firstCode = new int[lenCount.Length + 1];
                lenCount[0] = 0;                    /* Annex B.3 3) */
                for (int curLen = 1; curLen <= lenCount.Length; curLen++)
                {
                    firstCode[curLen] = (firstCode[curLen - 1] + lenCount[curLen - 1]) << 1;
                    int curCode = firstCode[curLen];
                    foreach (Code code in codeTable)
                    {
                        if (code.prefixLen == curLen)
                        {
                            code.code = curCode++;
                        }
                    }
                }
                foreach (Code c in codeTable)
                {
                    rootNode.append(c);
                }
            }
            internal long decode(ImgStream iis) => rootNode.decode(iis);
            public HuffmanTable(List<Code> codes = null)
            {
                if (codes != null)
                {
                    initTree(codes);
                }
            }
            public static HuffmanTable getStdTbl(int number)
            {
                HuffmanTable table = STANDARD_TABLES[number - 1];
                if (table == null)
                {
                    IEnumerable<Code> iniVals = TABLES[number - 1].Select(x => new Code(x[0], x[1], x[2], x.Length > 3));
                    STANDARD_TABLES[number - 1] = table = new HuffmanTable([.. iniVals]);
                }
                return table;
            }
        }
        internal class MMRDecompressor
        {
            private const int Lvl1TblSize = 8, Lvl1Mark = (1 << Lvl1TblSize) - 1, CodeOffset = 24,
                        Lvl2TblSize = 5, Lbl2Mask = (1 << Lvl2TblSize) - 1;
            private const int EOF = -3, INVALID = -2, EOL = -1, CODE_P = 0, CODE_H = 1, CODE_V0 = 2,
                CODE_VR1 = 3, CODE_VR2 = 4, CODE_VR3 = 5, CODE_VL1 = 6, CODE_VL2 = 7, CODE_VL3 = 8,
                CODE_EXT2D = 9, CODE_EXT1D = 10;
            // --------------------------------------------------------------------------------------------------------------
            private static readonly int[][] ModeCodes = [
                [4, 0x1, CODE_P], [3, 0x1, CODE_H], [1, 0x1, CODE_V0],
                [3, 0x3, CODE_VR1], [6, 0x3, CODE_VR2], [7, 0x3, CODE_VR3],
                [3, 0x2, CODE_VL1], [6, 0x2, CODE_VL2], [7, 0x2, CODE_VL3],
                [10, 0xf, CODE_EXT2D], [12, 0xf, CODE_EXT1D], [12, 0x1, EOL]
            ];
            private static readonly int[][] WhiteCodes = [
                [4, 0x07, 2], [4, 0x08, 3], [4, 0x0B, 4], [4, 0x0C, 5],
                [4, 0x0E, 6], [4, 0x0F, 7], [5, 0x12, 128], [5, 0x13, 8],
                [5, 0x14, 9], [5, 0x1B, 64], [5, 0x07, 10], [5, 0x08, 11],
                [6, 0x17, 192], [6, 0x18, 1664], [6, 0x2A, 16], [6, 0x2B, 17],
                [6, 0x03, 13], [6, 0x34, 14], [6, 0x35, 15], [6, 0x07, 1],
                [6, 0x08, 12], [7, 0x13, 26], [7, 0x17, 21], [7, 0x18, 28],
                [7, 0x24, 27], [7, 0x27, 18], [7, 0x28, 24], [7, 0x2B, 25],
                [7, 0x03, 22], [7, 0x37, 256], [7, 0x04, 23], [7, 0x08, 20],
                [7, 0xC, 19], [8, 0x12, 33], [8, 0x13, 34], [8, 0x14, 35],
                [8, 0x15, 36], [8, 0x16, 37], [8, 0x17, 38], [8, 0x1A, 31],
                [8, 0x1B, 32], [8, 0x02, 29], [8, 0x24, 53], [8, 0x25, 54],
                [8, 0x28, 39], [8, 0x29, 40], [8, 0x2A, 41], [8, 0x2B, 42],
                [8, 0x2C, 43], [8, 0x2D, 44], [8, 0x03, 30], [8, 0x32, 61],
                [8, 0x33, 62], [8, 0x34, 63], [8, 0x35, 0], [8, 0x36, 320],
                [8, 0x37, 384], [8, 0x04, 45], [8, 0x4A, 59], [8, 0x4B, 60],
                [8, 0x5, 46], [8, 0x52, 49], [8, 0x53, 50], [8, 0x54, 51],
                [8, 0x55, 52], [8, 0x58, 55], [8, 0x59, 56], [8, 0x5A, 57],
                [8, 0x5B, 58], [8, 0x64, 448], [8, 0x65, 512], [8, 0x67, 640],
                [8, 0x68, 576], [8, 0x0A, 47], [8, 0x0B, 48], [9, 0x01, INVALID],
                [9, 0x98, 1472], [9, 0x99, 1536], [9, 0x9A, 1600], [9, 0x9B, 1728],
                [9, 0xCC, 704], [9, 0xCD, 768], [9, 0xD2, 832], [9, 0xD3, 896],
                [9, 0xD4, 960], [9, 0xD5, 1024], [9, 0xD6, 1088], [9, 0xD7, 1152],
                [9, 0xD8, 1216], [9, 0xD9, 1280], [9, 0xDA, 1344], [9, 0xDB, 1408],
                [10, 0x01, INVALID], [11, 0x01, INVALID], [11, 0x08, 1792],
                [11, 0x0C, 1856], [11, 0x0D, 1920], [12, 0x00, EOF],
                [12, 0x01, EOL], [12, 0x12, 1984], [12, 0x13, 2048],
                [12, 0x14, 2112], [12, 0x15, 2176], [12, 0x16, 2240],
                [12, 0x17, 2304], [12, 0x1C, 2368], [12, 0x1D, 2432],
                [12, 0x1E, 2496], [12, 0x1F, 2560]
            ];
            private static readonly int[][] BlackCodes = [
                [2, 0x02, 3], [2, 0x03, 2], [3, 0x02, 1], [3, 0x03, 4],
                [4, 0x02, 6], [4, 0x03, 5], [5, 0x03, 7], [6, 0x04, 9],
                [6, 0x05, 8], [7, 0x04, 10], [7, 0x05, 11], [7, 0x07, 12],
                [8, 0x04, 13], [8, 0x07, 14], [9, 0x01, INVALID], [9, 0x18, 15],
                [10, 0x01, INVALID], [10, 0x17, 16], [10, 0x18, 17], [10, 0x37, 0],
                [10, 0x08, 18], [10, 0x0F, 64], [11, 0x01, INVALID], [11, 0x17, 24],
                [11, 0x18, 25], [11, 0x28, 23], [11, 0x37, 22], [11, 0x67, 19],
                [11, 0x68, 20], [11, 0x6C, 21], [11, 0x08, 1792], [11, 0x0C, 1856],
                [11, 0x0D, 1920], [12, 0x00, EOF], [12, 0x01, EOL], [12, 0x12, 1984],
                [12, 0x13, 2048], [12, 0x14, 2112], [12, 0x15, 2176], [12, 0x16, 2240],
                [12, 0x17, 2304], [12, 0x1C, 2368], [12, 0x1D, 2432], [12, 0x1E, 2496],
                [12, 0x1F, 2560], [12, 0x24, 52], [12, 0x27, 55], [12, 0x28, 56],
                [12, 0x2B, 59], [12, 0x2C, 60], [12, 0x33, 320], [12, 0x34, 384],
                [12, 0x35, 448], [12, 0x37, 53], [12, 0x38, 54], [12, 0x52, 50],
                [12, 0x53, 51], [12, 0x54, 44], [12, 0x55, 45], [12, 0x56, 46],
                [12, 0x57, 47], [12, 0x58, 57], [12, 0x59, 58], [12, 0x5A, 61],
                [12, 0x5B, 256], [12, 0x64, 48], [12, 0x65, 49], [12, 0x66, 62],
                [12, 0x67, 63], [12, 0x68, 30], [12, 0x69, 31], [12, 0x6A, 32],
                [12, 0x6B, 33], [12, 0x6C, 40], [12, 0x6D, 41], [12, 0xC8, 128],
                [12, 0xC9, 192], [12, 0xCA, 26], [12, 0xCB, 27], [12, 0xCC, 28],
                [12, 0xCD, 29], [12, 0xD2, 34], [12, 0xD3, 35], [12, 0xD4, 36],
                [12, 0xD5, 37], [12, 0xD6, 38], [12, 0xD7, 39], [12, 0xDA, 42],
                [12, 0xDB, 43], [13, 0x4A, 640], [13, 0x4B, 704], [13, 0x4C, 768],
                [13, 0x4D, 832], [13, 0x52, 1280], [13, 0x53, 1344], [13, 0x54, 1408],
                [13, 0x55, 1472], [13, 0x5A, 1536], [13, 0x5B, 1600], [13, 0x64, 1664],
                [13, 0x65, 1728], [13, 0x6C, 512], [13, 0x6D, 576], [13, 0x72, 896],
                [13, 0x73, 960], [13, 0x74, 1024], [13, 0x75, 1088], [13, 0x76, 1152],
                [13, 0x77, 1216]
            ];
            private readonly int width, height;
            // A class encapsulating the compressed raw data.
            internal class MMRCode
            {
                internal MMRCode[] subTable = null;
                internal int bitLength, codeWord, runLength;
                internal MMRCode(int[] codeData)
                {
                    (bitLength, codeWord, runLength) = (codeData[0], codeData[1], codeData[2]);
                }
            }
            internal class RunData
            {
                internal ImgStream stream;      /** Compressed data stream. */
                internal int offset, lstOffset = 0, lstCode = 0, bufBase, bufTop, iniPos, maxLen;
                private readonly byte[] buffer;
                internal RunData(ImgStream stream, long len)
                {
                    this.stream = stream;
                    iniPos = (int)stream.Position;
                    lstOffset = 1;
                    try
                    {
                        maxLen = (int)len;
                        buffer = new byte[(int)Math.Min(Math.Max(3, len), 1 << 17)];
                        fillBuffer(0);
                    }
                    catch (IOException e)
                    {
                        buffer = new byte[10];
                        _ = error(e.ToString(), false);
                    }
                }
                internal MMRCode uncompressGetCode(MMRCode[] table)
                {
                    int code = uncompressGetNextCodeLittleEndian() & 0xffffff;
                    MMRCode res = table[code >> (CodeOffset - Lvl1TblSize)];
                    if (res?.subTable != null)  // perform second-level lookup
                    {
                        res = res.subTable[(code >> (CodeOffset - Lvl1TblSize - Lvl2TblSize)) & Lbl2Mask];
                    }

                    return res;
                }
                // Fill up the code word in little endian mode. This is a hotspot, therefore the algorithm is heavily optimised.
                // For the frequent cases (i.e. short words) we try to get away with as little work as possible. <br>
                // This method returns code words of 16 bits, which are aligned to the 24th bit. The lowest 8 bits are used as a
                // "queue" of bits so that an access to the actual data is only needed, when this queue becomes empty.
                private int uncompressGetNextCodeLittleEndian()
                {
                    try
                    {
                        int bitsToFill = offset - lstOffset;           // the # of bits to fill (offset difference)
                        if (bitsToFill is < 0 or > 24)
                        {        // refill at absolute offset
                            int byteOffset = (offset >> 3) - bufBase;// offset>>3 is equivalent to offset/8
                            if (byteOffset >= bufTop)
                            {
                                fillBuffer(byteOffset += bufBase);
                                byteOffset -= bufBase;
                            }
                            lstCode = ((buffer[byteOffset] & 0xff) << 16)
                                    | ((buffer[byteOffset + 1] & 0xff) << 8)
                                    | (buffer[byteOffset + 2] & 0xff);
                            lstCode <<= offset & 7; // equivalent to offset%8
                        }
                        else
                        {
                            int bitOffset = lstOffset & 7;     // the offset to the next byte boundary 
                            if (bitsToFill <= 7 - bitOffset)    // check whether there are enough bits 
                            {
                                lstCode <<= bitsToFill;        // in the "queue"
                            }
                            else
                            {
                                int byteOffset = (lstOffset >> 3) + 3 - bufBase;
                                if (byteOffset >= bufTop)
                                {
                                    byteOffset += bufBase;
                                    fillBuffer(byteOffset);
                                    byteOffset -= bufBase;
                                }
                                bitOffset = 8 - bitOffset;
                                do
                                {
                                    lstCode <<= bitOffset;
                                    lstCode |= buffer[byteOffset] & 0xff;
                                    bitsToFill -= bitOffset;
                                    byteOffset++;
                                    bitOffset = 8;
                                } while (bitsToFill >= 8);
                                lstCode <<= bitsToFill; // shift the rest
                            }
                        }
                        lstOffset = offset;
                        return lstCode;
                    }
                    catch (IOException e)
                    {// will this actually happen? only with broken data, I'd say.
                        throw new IndexOutOfRangeException("Corrupted RLE data caused by an IOException while reading raw data: " + e);
                    }
                }
                private void fillBuffer(int byteOffset)
                {
                    bufBase = byteOffset;
                    if (byteOffset < maxLen)
                    {
                        try
                        {
                            stream.Seek(iniPos + byteOffset);
                            bufTop = stream.Read(buffer, 0, buffer.Length - bufBase);
                            for (int ml = Math.Min(3, buffer.Length); bufTop < ml; bufTop++)
                            {
                                buffer[bufTop] = 0;
                            }
                        }
                        catch
                        {
                            bufTop = -1;                         // IDK which kind of EOF will kick in
                        }                                           // check filling degree
                    }

                    bufTop -= 3;                                 // leave some room, in order to save 
                    if (bufTop < 0)
                    {                            // a few tests in the calling code
                        Array.Clear(buffer, 0, buffer.Length);      // if EOF, just supply zero-bytes
                        bufTop = buffer.Length - 3;
                    }
                }
                internal void align() => offset = ((offset + 7) >> 3) << 3;
            }
            private static MMRCode[] whiteTable = null, blackTable = null, modeTable = null;
            internal RunData data;
            private int uncompress2D(RunData runData, int[] refOffsets, int refRunLen, int[] runOffsets, int width)
            {
                int refBufIdx = 0, curBufIdx = 0, curLnBitPos = 0;
                bool whiteRun = true; // Always start with a white run
                MMRCode code = null; // Storage var for current code being processed
                refOffsets[refRunLen] = refOffsets[refRunLen + 1] = width;
                refOffsets[refRunLen + 2] = refOffsets[refRunLen + 3] = width + 1;
                try
                {
                    while (curLnBitPos < width)
                    {
                        code = runData.uncompressGetCode(modeTable);    // Get the mode code
                        if (code == null)
                        {
                            runData.offset++;
                            break;
                        }
                        runData.offset += code.bitLength;               // Add the code length to the bit offset
                        switch (code.runLength)
                        {
                            case CODE_V0: curLnBitPos = refOffsets[refBufIdx]; break;
                            case CODE_VR1: curLnBitPos = refOffsets[refBufIdx] + 1; break;
                            case CODE_VL1: curLnBitPos = refOffsets[refBufIdx] - 1; break;
                            case CODE_VR2: curLnBitPos = refOffsets[refBufIdx] + 2; break;
                            case CODE_VL2: curLnBitPos = refOffsets[refBufIdx] - 2; break;
                            case CODE_VR3: curLnBitPos = refOffsets[refBufIdx] + 3; break;
                            case CODE_VL3: curLnBitPos = refOffsets[refBufIdx] - 3; break;
                            case CODE_P:
                                refBufIdx++;
                                curLnBitPos = refOffsets[refBufIdx++];
                                continue;
                            case CODE_H:
                                for (int ever = 1; ever > 0;)
                                {
                                    code = runData.uncompressGetCode(whiteRun ? whiteTable : blackTable);
                                    if (code == null)
                                    {
                                        goto loopDone;
                                    }

                                    runData.offset += code.bitLength;
                                    if (code.runLength < 64)
                                    {
                                        if (code.runLength < 0)
                                        {
                                            runOffsets[curBufIdx++] = curLnBitPos;
                                            code = null;
                                            goto loopDone;
                                        }
                                        curLnBitPos += code.runLength;
                                        runOffsets[curBufIdx++] = curLnBitPos;
                                        break;
                                    }
                                    curLnBitPos += code.runLength;
                                }
                                for (int ever1 = 1, HalfBitPos1 = curLnBitPos; ever1 > 0;)
                                {
                                    code = runData.uncompressGetCode(!whiteRun ? whiteTable : blackTable);
                                    if (code == null)
                                    {
                                        goto loopDone;
                                    }

                                    runData.offset += code.bitLength;
                                    if (code.runLength < 64)
                                    {
                                        if (code.runLength < 0)
                                        {
                                            runOffsets[curBufIdx++] = curLnBitPos;
                                            goto loopDone;
                                        }
                                        curLnBitPos += code.runLength;
                                        // don't generate 0-length run at EOL for cases where the line ends in an H-run.
                                        if (curLnBitPos < width || curLnBitPos != HalfBitPos1)
                                        {
                                            runOffsets[curBufIdx++] = curLnBitPos;
                                        }

                                        break;
                                    }
                                    curLnBitPos += code.runLength;
                                }
                                while (curLnBitPos < width && refOffsets[refBufIdx] <= curLnBitPos)
                                {
                                    refBufIdx += 2;
                                }

                                continue;
                            case EOL:
                            default:        // Possibly MMR Decoded
                                _ = error("Should not happen!", false);
                                if (runData.offset == 12 && code.runLength == EOL)
                                {
                                    runData.offset = 0;
                                    _ = uncompress1D(runData, refOffsets, width);
                                    runData.offset++;
                                    _ = uncompress1D(runData, runOffsets, width);
                                    int retCode = uncompress1D(runData, refOffsets, width);
                                    runData.offset++;
                                    return retCode;
                                }
                                curLnBitPos = width;
                                continue;
                        }
                        if (curLnBitPos <= width)
                        { // Only vertical modes get this far
                            whiteRun = !whiteRun;
                            runOffsets[curBufIdx++] = curLnBitPos;
                            refBufIdx += refBufIdx > 0 ? -1 : 1;
                            while (curLnBitPos < width && refOffsets[refBufIdx] <= curLnBitPos)
                            {
                                refBufIdx += 2;
                            }
                        }
                    }
                loopDone:
                    refBufIdx = 0; // compiler warning
                }
                catch (Exception t)
                {
                    _ = error(t.ToString(), false);
                    return EOF;
                }
                if (runOffsets[curBufIdx] != width)
                {
                    runOffsets[curBufIdx] = width;
                }

                return (code == null) ? EOL : curBufIdx;
            }
            internal MMRDecompressor(int width, int height, ImgStream stream, long len)
            {
                this.width = width;
                this.height = height;
                data = new RunData(stream, len);
                if (whiteTable == null)
                {
                    whiteTable = createLittleEndianTable(WhiteCodes);
                    blackTable = createLittleEndianTable(BlackCodes);
                    modeTable = createLittleEndianTable(ModeCodes);
                }
            }
            internal JB2Bmp uncompress()
            {
                JB2Bmp result = new(width, height);
                int[] curOff = new int[width + 5], refOff = new int[width + 5];
                refOff[0] = width;
                int line = 0, refRunLen = 1, count;
                for (; line < height; line++, refRunLen = count)
                {
                    if (EOF == (count = uncompress2D(data, refOff, refRunLen, curOff, width)))
                    {
                        break;
                    }

                    if (count > 0)
                    {
                        fillBitmap(result, line, curOff, count);
                    }

                    (refOff, curOff) = (curOff, refOff);
                }
                while (true)
                {
                    MMRCode code = data.uncompressGetCode(modeTable);
                    if (code?.runLength == EOL)
                    {
                        data.offset += code.bitLength;
                    }
                    else
                    {
                        break;
                    }
                }
                data.align();
                return result;
            }
            private void fillBitmap(JB2Bmp result, int line, int[] currentOffsets, int count)
            {
                int x = 0, targetByte = result.getIdx(0, line);
                byte dstByteVal = 0;
                for (int index = 0; index < count; index++)
                {
                    for (int offset = currentOffsets[index], value = index & 1; x < offset;)
                    {
                        dstByteVal = (byte)((dstByteVal << 1) | value);
                        if ((++x & 7) == 0)
                        {
                            result.bitmap[targetByte++] = dstByteVal;
                            dstByteVal = 0;
                        }
                    }
                }

                if ((x & 7) != 0)
                {
                    dstByteVal <<= 8 - (x & 7);
                    result.bitmap[targetByte] = dstByteVal;
                }
            }
            private int uncompress1D(RunData runData, int[] runOffsets, int width)
            {
                bool whiteRun = true;               // should not get here !!!!!
                MMRCode code = null;
                int iBitPos = 0, refOffset = 0;
                while (iBitPos < width)
                {
                    while (true)
                    {
                        code = runData.uncompressGetCode(whiteRun ? whiteTable : blackTable);
                        runData.offset += code.bitLength;
                        if (code.runLength < 0)
                        {
                            goto loop;
                        }

                        iBitPos += code.runLength;
                        if (code.runLength < 64)
                        {
                            whiteRun = !whiteRun;
                            runOffsets[refOffset++] = iBitPos;
                            break;
                        }
                    }
                }

            loop:
                if (runOffsets[refOffset] != width)
                {
                    runOffsets[refOffset] = width;
                }

                return code != null && code.runLength != EOL ? refOffset : EOL;
            }
            private static MMRCode[] createLittleEndianTable(int[][] codes)
            {
                MMRCode[] lvl1Tbl = new MMRCode[Lvl1Mark + 1];
                for (int i = 0; i < codes.Length; i++)
                {
                    MMRCode code = new(codes[i]);
                    if (code.bitLength <= Lvl1TblSize)
                    {
                        int varLen = Lvl1TblSize - code.bitLength;
                        int baseWord = code.codeWord << varLen;
                        for (int variant = (1 << varLen) - 1; variant >= 0; variant--)
                        {
                            lvl1Tbl[baseWord | variant] = code;
                        }
                    }
                    else
                    {                  // init second level table
                        int lvl1Idx = (int)(((uint)code.codeWord) >> (code.bitLength - Lvl1TblSize));
                        lvl1Tbl[lvl1Idx] ??= new MMRCode(new int[3]) { subTable = new MMRCode[Lbl2Mask + 1] };
                        if (code.bitLength <= Lvl1TblSize + Lvl2TblSize)
                        {
                            MMRCode[] lvl2Tbl = lvl1Tbl[lvl1Idx].subTable;
                            int varLen = Lvl1TblSize + Lvl2TblSize - code.bitLength;
                            int baseWord = (code.codeWord << varLen) & Lbl2Mask;
                            for (int variant = (1 << varLen) - 1; variant >= 0; variant--)
                            {
                                lvl2Tbl[baseWord | variant] = code;
                            }
                        }
                        else
                        {
                            throw new ArgumentException("Code table overflow in MMRDecompressor");
                        }
                    }
                }
                return lvl1Tbl;
            }
        }
        internal enum ComboOper { OR = 0, AND = 1, XOR = 2, XNOR = 3, REPLACE = 4 }
        internal class SegmentHeader
        {
            private static readonly Dictionary<int, Type> SegTypesMap = new()
            {
                { 0,  typeof(SymbolDictionary) },       { 4,  typeof(TextRegion) },
                { 6,  typeof(TextRegion) },             { 7,  typeof(TextRegion) },
                { 16, typeof(PatternDictionary) },      { 20, typeof(HalftoneRegion) },
                { 22, typeof(HalftoneRegion) },         { 23, typeof(HalftoneRegion) },
                { 36, typeof(GenRegion) },              { 38, typeof(GenRegion) },
                { 39, typeof(GenRegion) },              { 40, typeof(GenRefineRegion) },
                { 42, typeof(GenRefineRegion) },        { 43, typeof(GenRefineRegion) },
                { 48, typeof(PageInformation) },        { 50, typeof(EndOfStripe) },
                { 52, typeof(SegmentData) },            { 53, typeof(Table) }
            };
            internal int segNo, segType, pgAssoc;
            internal long DataLen, DataStart;
            private readonly SegmentHeader[] rtSegments;
            private readonly ImgStream imgStrm;
            private SegmentData segData;
            internal SegmentHeader(PBoxJBig2 document, ImgStream sis, long offset)
            {
                imgStrm = sis;
                sis.Seek(offset);
                segNo = sis.readInt(4);
                _ = sis.readBits(1);   // Bit 7: if 1, this segment is retained;
                                       // Bit 6: Size of the page association field. One byte if 0, four bytes if 1;
                bool pgAssocFldSz = (byte)sis.readBits(1) != 0;
                // Bit 5-0: Contains the values (between 0 and 62 with gaps) for segment types, specified in 7.3
                segType = sis.readBits(6) & 0xff;
                /* 7.2.4 Amount of referred-to segments */
                int countOfRTS = sis.readBits(3) & 0xf, arrayLength = 5;
                if (countOfRTS > 4)
                {                       /* long format */
                    countOfRTS = (int)(sis.readBits(29) & 0xffffffff);
                    arrayLength = (countOfRTS + 8) >> 3;
                }
                for (int i = 0; i < arrayLength; i++)
                {
                    _ = sis.readBits(1);
                }

                int[] rtsNumbers = new int[countOfRTS];     /* 7.2.5 Referred-to segments numbers */
                if (countOfRTS > 0)
                {
                    int rtsSize = (segNo > 65536) ? 4 : (segNo > 256 ? 2 : 1);
                    rtSegments = new SegmentHeader[countOfRTS];
                    for (int i = 0; i < countOfRTS; i++)
                    {
                        rtsNumbers[i] = sis.readInt(rtsSize);
                    }
                }
                /* 7.2.6 Segment page association (Checks how big the page association field is.) */
                pgAssoc = pgAssocFldSz ? sis.readInt(4) : sis.readInt(1);
                if (countOfRTS > 0)
                {
                    JBIG2Page page = document.getPage(pgAssoc);
                    for (int i = 0; i < countOfRTS; i++)
                    {
                        rtSegments[i] = page != null
                            ? page.getSegment(rtsNumbers[i]) : document.getGlobalSegment(rtsNumbers[i]);
                    }
                }
                DataLen = sis.readInt(4);
                DataStart = sis.Position;                   // for rand get later 
                if ((int)DataLen == -1)                     // overwritten in the MapStream
                {
                    DataLen = sis.Length - sis.Position;
                }
            }
            internal SegmentHeader[] getRtSegments() => rtSegments;
            internal SegmentData getSegData()
            {
                if (segData != null)
                {
                    return segData;
                }

                Type segmentClass = SegTypesMap[segType] ?? throw new ArgumentException("No segment class for type " + segType);
                segData = Activator.CreateInstance(segmentClass) as SegmentData;
                segData.init(this, new ImgStream(imgStrm, DataStart, DataLen));
                return segData;
            }
        }
        internal class SegmentData
        {
            protected SegmentHeader segHdr;
            internal long dataOffset, dataLength;
            internal ImgStream imgStrm;
            protected ComboOper comboOper;
            internal virtual void init(SegmentHeader header, ImgStream sis)
            {
                imgStrm = sis;
                segHdr = header;
            }
            internal virtual List<JB2Bmp> getDictionary() => null;
            public ComboOper getComboOper() => comboOper;
        }
        internal class EndOfStripe : SegmentData
        {
            internal int lineNum;
            internal override void init(SegmentHeader header, ImgStream sis)
            {
                base.init(header, sis);
                lineNum = imgStrm.readInt(4);
            }
        }
        internal class PageInformation : SegmentData
        {
            internal int width, height, defPix;
            /** Page segment flags, one byte, 7.4.8.5 */
            internal bool comboOperOverrideAllow, reqAuxBuf, hasRefine, isLossless, isStriped;
            internal override void init(SegmentHeader header, ImgStream sis)
            {
                base.init(header, sis);
                width = imgStrm.readInt(4);
                height = imgStrm.readInt(4);
                _ = imgStrm.readInt(4);                                 // resolutionX & 0xffffffff);
                _ = imgStrm.readInt(4);                                 // resolutionY & 0xffffffff);
                _ = imgStrm.readBits(1);                                // dirty read
                comboOperOverrideAllow = imgStrm.readBits(1) == 1;/* Bit 6 */
                reqAuxBuf = imgStrm.readBits(1) == 1;             /* Bit 5 */
                comboOper = (ComboOper)(imgStrm.readBits(2) & 0xf); /* Bit 3-4 */
                defPix = imgStrm.readBits(1);                   /* Bit 2 */
                hasRefine = imgStrm.readBits(1) == 1;             /* Bit 1 */
                isLossless = imgStrm.readBits(1) == 1;              /* Bit 0 */
                isStriped = imgStrm.readBits(1) == 1;             /* Bit 15 */
                _ = imgStrm.readBits(15);                               /* Bit 0-14 maxStripeSize */
            }
            public bool isComboOperOverrideAllow() => comboOperOverrideAllow;
        }
        internal class RegSegInfo : SegmentData
        {
            internal int width, height, xLoc, yLoc;
            /** Region segment flags, 7.4.1.5 */
            public RegSegInfo(ImgStream subInputStream = null)
            {
                imgStrm = subInputStream;
            }
            public void parseHeader()
            {
                width = imgStrm.readInt(4);
                height = imgStrm.readInt(4);
                xLoc = imgStrm.readInt(4);
                yLoc = imgStrm.readInt(4);
                _ = imgStrm.readBits(5); // Dirty read... reserved bits are 0
                comboOper = (ComboOper)(imgStrm.readBits(3) & 0xf);
            }
        }
        internal class Table : SegmentData
        {
            private int htOOB, htPS, htRS, htLow, htHigh;         /** Code table flags, B.2.1, page 87 */
            internal override void init(SegmentHeader header, ImgStream sis)
            {
                base.init(header, sis);
                if (imgStrm.readBits(1) == 1)               /* Bit 7 */
                {
                    throw new ArgumentException("B.2.1 Code table flags: Bit 7 must be zero");
                }

                htRS = imgStrm.readBits(3) + 1;         /* Bit 4-6 */
                htPS = imgStrm.readBits(3) + 1;         /* Bit 1-3 */
                htOOB = imgStrm.readBits(1);                /* Bit 0 */
                htLow = imgStrm.readInt(4);
                htHigh = imgStrm.readInt(4);
            }
            internal HuffmanTable getHuffman()
            {
                int pref, rng;
                List<HuffmanTable.Code> codeTable = [];
                for (int c = htLow; c < htHigh; c += 1 << rng)
                {
                    pref = imgStrm.readBits(htPS);
                    rng = imgStrm.readBits(htRS);
                    codeTable.Add(new HuffmanTable.Code(pref, rng, c, false));
                }
                codeTable.Add(new HuffmanTable.Code(imgStrm.readBits(htPS), 32, htLow - 1, true));
                codeTable.Add(new HuffmanTable.Code(imgStrm.readBits(htPS), 32, htHigh, false));
                if (htOOB == 1)                  /* Annex B.2 10) - out-of-band table line */
                {
                    codeTable.Add(new HuffmanTable.Code(imgStrm.readBits(htPS), -1, -1, false));
                }

                return new HuffmanTable(codeTable);
            }
        }
        internal class PatternDictionary : SegmentData
        {
            /** Segment data structure (only necessary if MMR is used) */
            private short[] gbAtX, gbAtY;
            private bool isMMR;
            private int hdpWidth, hdpHeight, grayMax, hdTemplate;
            private List<JB2Bmp> patterns;
            internal override void init(SegmentHeader header, ImgStream sis)
            {
                base.init(header, sis);
                _ = imgStrm.readBits(5);                        // Dirty read .../* Bit 3-7 */
                hdTemplate = imgStrm.readBits(2);     /* Bit 1-2 */
                isMMR = imgStrm.readBits(1) == 1; /* Bit 0 */
                hdpWidth = imgStrm.readSByte();
                hdpHeight = imgStrm.readSByte();
                grayMax = imgStrm.readInt(4);
                dataOffset = imgStrm.Position;
                dataLength = imgStrm.Length - dataOffset;
                if (hdpHeight < 1 || hdpWidth < 1)
                {
                    throw new ArgumentException("Width/Heigth must be greater than zero.");
                }
            }
            internal override List<JB2Bmp> getDictionary()
            {
                if (patterns != null)
                {
                    return patterns;
                }

                if (!isMMR)
                {
                    if (hdTemplate == 0)
                    {
                        gbAtX = [(short)-hdpWidth, -3, 2, -2];
                        gbAtY = [0, -1, -2, -2];
                    }
                    else
                    {
                        gbAtX = [(short)-hdpWidth];
                        gbAtY = [0];
                    }
                }

                GenRegion genReg = new(imgStrm);              // 2)
                genReg.setParameters(this, isMMR, hdpHeight,
                        (grayMax + 1) * hdpWidth, hdTemplate, gbAtX, gbAtY);
                JB2Bmp colBmp = genReg.getRegBmp();
                patterns = new List<JB2Bmp>(grayMax + 1);               // 3)
                for (int gray = 0; gray <= grayMax; gray++)
                {           // 4)
                    Rectangle roi = new(hdpWidth * gray, 0, hdpWidth, hdpHeight);
                    patterns.Add(colBmp.extract(roi));                  // 4) b)
                }
                return patterns;
            }
        }
        internal class SymbolDictionary : SegmentData
        {
            /** Symbol dictionary flags, 7.4.2.1.1 */
            private byte sdTemplate;
            private bool isCodeCtxRetained, isCodeCtxUsed, useRefineAggr, isHuffman;
            private int amtExpSymb, amtNewSymb, amtImpSymb, amtDecodedSymb, sbSymCodeLen, sdrTemplate,
                    sdHuffAggInstSel, sdHuffBMSizeSel, sdHuffDecodeWidthSel, sdHuffDecodeHeightSel;
            private short[] sdATX, sdATY;               /** Symbol dictionary AT flags, 7.4.2.1.2 */
            private short[] sdrATX, sdrATY;             /** Symbol dictionary refinement AT flags, 7.4.2.1.3 */
            private List<JB2Bmp> importSymbols;
            private JB2Bmp[] newSymbols;
            private HuffmanTable dhTable, dwTable, bmSizeTable, aggInstTable;
            private List<JB2Bmp> expSymbols, sbSymbols;
            private ArithmDecoder arithmeticDecoder;
            private TextRegion txtReg;
            private GenRegion genReg;
            private GenRefineRegion genRefineReg;
            internal CXStats cx, cxIADH, cxIADW, cxIAAI, cxIAEX, cxIARDX, cxIARDY, cxIADT, cxIAID;
            internal override List<JB2Bmp> getDictionary()
            {    // 6.5.5 Decoding the symbol dictionary
                if (expSymbols == null)
                {
                    if (useRefineAggr)
                    {
                        sbSymCodeLen = getSbSymCodeLen();
                    }

                    if (!isHuffman)
                    {
                        if (cxIADT == null)
                        {
                            (cxIADT, cxIADH, cxIADW, cxIAAI, cxIAEX, cx)
                                = (new CXStats(), new CXStats(), new CXStats(),
                                    new CXStats(), new CXStats(), new CXStats(65536));
                        }

                        if (useRefineAggr && cxIAID == null)
                        {
                            (cxIAID, cxIARDX, cxIARDY) = (new CXStats(1 << sbSymCodeLen),
                                        new CXStats(), new CXStats());
                        }

                        arithmeticDecoder ??= new ArithmDecoder(imgStrm);
                    }
                    newSymbols = new JB2Bmp[amtNewSymb];                    /* 6.5.5 1) */
                    int[] newSymbWidths = null;                         /* 6.5.5 2) */
                    if (isHuffman && !useRefineAggr)
                    {
                        newSymbWidths = new int[amtNewSymb];
                    }

                    setSymbolsArray();
                    int clsHeight = 0;                                              /* 6.5.5 3) */
                    for (amtDecodedSymb = 0; amtDecodedSymb < amtNewSymb;)
                    {          /* 6.5.5 4 a) */
                        clsHeight += (int)(isHuffman ? decodeHeightClassDeltaHeightWithHuffman() : arithmeticDecoder.decodeInt(cxIADH));
                        int symbWidth = 0, totalWidth = 0, htCls1SymbIdx = amtDecodedSymb;
                        for (; ; amtDecodedSymb++)
                        {                             /* 6.5.5 4 c) */
                            long difWidth = decodeDifferenceWidth();                /* 4 c) i) */
                            if (difWidth == long.MaxValue || amtDecodedSymb >= amtNewSymb)// Repeat until OOB - OOB sends a break;
                            {
                                break;
                            }

                            symbWidth += (int)difWidth;
                            totalWidth += symbWidth;
                            if (!isHuffman || useRefineAggr)                  /* 4 c) ii) */
                            {
                                if (!useRefineAggr)
                                {
                                    genReg ??= new GenRegion(imgStrm);
                                    genReg.setParameters(sdTemplate,                // Parameters set according to Table 16, page 35
                                        sdATX, sdATY, symbWidth, clsHeight, cx, arithmeticDecoder);
                                    addSymbol(genReg);
                                }
                                else
                                {
                                    long refAggrInst = isHuffman ? huffDecodeRefAggNInst() : arithmeticDecoder.decodeInt(cxIAAI);
                                    if (refAggrInst > 1)                            // 6.5.8.2 2)
                                    {
                                        decodeThroughTextRegion(symbWidth, clsHeight, refAggrInst);
                                    }
                                    else if (refAggrInst == 1)                      // 6.5.8.2 3) refers to 6.5.8.2.2
                                    {
                                        decodeRefinedSymbol(symbWidth, clsHeight);
                                    }
                                }
                            }
                            else if (isHuffman && !useRefineAggr)
                            {
                                newSymbWidths[amtDecodedSymb] = symbWidth;                       /* 4 c) iii) */
                            }
                        }
                        if (isHuffman && !useRefineAggr)
                        {                   /* 6.5.5 4 d) */
                            long bmSize = (sdHuffBMSizeSel != 0) ? huffDecodeBmSize()/* 6.5.9 */
                                            : HuffmanTable.getStdTbl(1).decode(imgStrm);
                            imgStrm.skipBits();
                            JB2Bmp htClsCollBmp = decodeHeightClassCollectiveBitmap(bmSize, clsHeight, totalWidth);
                            imgStrm.skipBits();
                            for (int i = htCls1SymbIdx; i < amtDecodedSymb; i++)
                            {
                                int colStart = 0;
                                for (int j = htCls1SymbIdx; j <= i - 1; j++)
                                {
                                    colStart += newSymbWidths[j];
                                }

                                Rectangle roi = new(colStart, 0, newSymbWidths[i], clsHeight);
                                JB2Bmp symbolBitmap = htClsCollBmp.extract(roi);
                                newSymbols[i] = symbolBitmap;
                                sbSymbols.Add(symbolBitmap);
                            }
                        }
                    }
                    int[] exFlags = getToExportFlags();                              /* 6.5.10 1) - 5) */
                    setExportedSymbols(exFlags);                                     /* 6.5.10 6) - 8) */
                }
                return expSymbols;
            }
            private long huffDecodeRefAggNInst()
            {
                if (sdHuffAggInstSel == 0)
                {
                    return HuffmanTable.getStdTbl(1).decode(imgStrm);
                }
                else if (sdHuffAggInstSel == 1)
                {
                    if (aggInstTable == null)
                    {
                        int ain = 0;
                        if (sdHuffDecodeHeightSel == 3)
                        {
                            ain++;
                        }

                        if (sdHuffDecodeWidthSel == 3)
                        {
                            ain++;
                        }

                        if (sdHuffBMSizeSel == 3)
                        {
                            ain++;
                        }

                        aggInstTable = getUserTable(ain);
                    }
                    return aggInstTable.decode(imgStrm);
                }
                return 0;
            }
            private void decodeThroughTextRegion(int symbolWidth, int heightClassHeight, long amountOfRefinementAggregationInstances)
            {
                if (txtReg == null)
                {
                    txtReg = new TextRegion { imgStrm = imgStrm, regInfo = new RegSegInfo(imgStrm) };
                    txtReg.setContexts(cx, new CXStats(), new CXStats(), new CXStats(), new CXStats(),
                                   cxIAID, new CXStats(), new CXStats(), new CXStats(), new CXStats());
                }
                setSymbolsArray();  // 6.5.8.2.4 Concatenating the array used as parameter later.
                                    // 6.5.8.2 2) Parameters set according to Table 17, page 36
                txtReg.setParameters(arithmeticDecoder, isHuffman, symbolWidth, heightClassHeight,
                    amountOfRefinementAggregationInstances, amtImpSymb + amtDecodedSymb,
                    sdrTemplate, sdrATX, sdrATY, sbSymbols, sbSymCodeLen);
                addSymbol(txtReg);
            }
            private void decodeRefinedSymbol(int symWidth, int hcHeight)
            {
                int id, rdx, rdy;
                if (isHuffman)
                {
                    id = imgStrm.readBits(sbSymCodeLen);
                    rdx = (int)HuffmanTable.getStdTbl(15).decode(imgStrm);
                    rdy = (int)HuffmanTable.getStdTbl(15).decode(imgStrm);
                    _ = HuffmanTable.getStdTbl(1).decode(imgStrm);
                    imgStrm.skipBits();
                }
                else
                {
                    id = (int)arithmeticDecoder.decodeIAID(sbSymCodeLen, cxIAID);
                    rdx = (int)arithmeticDecoder.decodeInt(cxIARDX);
                    rdy = (int)arithmeticDecoder.decodeInt(cxIARDY);
                }
                setSymbolsArray();                  /* 6) */
                JB2Bmp ibo = sbSymbols[id];
                if (genRefineReg == null)
                {
                    genRefineReg = new GenRefineRegion(imgStrm);
                    if (arithmeticDecoder == null)
                    {
                        (arithmeticDecoder, cx) = (new ArithmDecoder(imgStrm), new CXStats(65536));
                    }
                }
                genRefineReg.setParameters(cx, arithmeticDecoder,        // Parameters as shown in Table 18, page 36
                    sdrTemplate, symWidth, hcHeight, ibo, rdx, rdy, sdrATX, sdrATY);
                addSymbol(genRefineReg);
                if (isHuffman)               /* 7) */
                {
                    imgStrm.skipBits();
                }
            }   // Make sure that the processed bytes are equal to the value read in step 5 a)
            private void addSymbol(Region region) => sbSymbols.Add(newSymbols[amtDecodedSymb] = region.getRegBmp());
            private long decodeDifferenceWidth()
            {
                if (isHuffman)
                {
                    switch (sdHuffDecodeWidthSel)
                    {
                        case 0: return HuffmanTable.getStdTbl(2).decode(imgStrm);
                        case 1: return HuffmanTable.getStdTbl(3).decode(imgStrm);
                        case 3:
                            dwTable ??= getUserTable(sdHuffDecodeHeightSel == 3 ? 1 : 0);
                            return dwTable.decode(imgStrm);
                    }
                }
                else
                {
                    return arithmeticDecoder.decodeInt(cxIADW);
                }

                return 0;
            }
            private long decodeHeightClassDeltaHeightWithHuffman()
            {
                return sdHuffDecodeHeightSel switch
                {
                    0 => HuffmanTable.getStdTbl(4).decode(imgStrm),
                    1 => HuffmanTable.getStdTbl(5).decode(imgStrm),
                    3 => (dhTable ??= getUserTable(0)).decode(imgStrm),
                    _ => 0,
                };
            }
            private JB2Bmp decodeHeightClassCollectiveBitmap(long bmSize, int htCls, int totWidth)
            {
                if (bmSize == 0)
                {
                    JB2Bmp ret = new(totWidth, htCls);
                    for (int i = 0, len = ret.Length; i < len; i++)
                    {
                        ret.bitmap[i] = (byte)imgStrm.readSByte();
                    }

                    return ret;
                }
                genReg ??= new GenRegion(imgStrm);
                genReg.setParameters(imgStrm.Position, bmSize, htCls, totWidth);
                return genReg.getRegBmp();
            }
            private void setExportedSymbols(int[] toExportFlags)
            {
                expSymbols = new List<JB2Bmp>(amtExpSymb);
                for (int i = 0; i < amtImpSymb + amtNewSymb; i++)
                {
                    if (toExportFlags[i] == 1)
                    {
                        expSymbols.Add(i < amtImpSymb ? importSymbols[i] : newSymbols[i - amtImpSymb]);
                    }
                }
            }
            private int[] getToExportFlags()
            {
                int currentExportFlag = 0;
                long exRunLen;
                int[] exportFlags = new int[amtImpSymb + amtNewSymb];
                for (int expIdx = 0; expIdx < amtImpSymb + amtNewSymb; expIdx += (int)exRunLen)
                {
                    exRunLen = !isHuffman ? arithmeticDecoder.decodeInt(cxIAEX)
                                    : HuffmanTable.getStdTbl(1).decode(imgStrm);
                    if (exRunLen != 0)
                    {
                        for (int index = expIdx; index < expIdx + exRunLen; index++)
                        {
                            exportFlags[index] = currentExportFlag;
                        }
                    }

                    currentExportFlag = (currentExportFlag == 0) ? 1 : 0;
                }
                return exportFlags;
            }
            private long huffDecodeBmSize()
            {
                if (bmSizeTable == null)
                {
                    int bmNr = 0;
                    if (sdHuffDecodeHeightSel == 3)
                    {
                        bmNr++;
                    }

                    if (sdHuffDecodeWidthSel == 3)
                    {
                        bmNr++;
                    }

                    bmSizeTable = getUserTable(bmNr);
                }
                return bmSizeTable.decode(imgStrm);
            }
            private int getSbSymCodeLen()
            {
                double v = Math.Log(amtImpSymb + amtNewSymb) / Math.Log(2);
                return isHuffman ? (int)Math.Max(1, Math.Ceiling(v))
                            : (int)Math.Ceiling(Math.Log(amtImpSymb + amtNewSymb) / Math.Log(2));
            }
            private void setSymbolsArray()
            {
                if (importSymbols == null)
                {
                    retrieveImportSymbols();
                }

                sbSymbols ??= [.. importSymbols];
            }
            private void retrieveImportSymbols()
            {
                importSymbols = [];
                foreach (SegmentHeader referredToSegmentHeader in segHdr.getRtSegments())
                {
                    if (referredToSegmentHeader.segType == 0)
                    {
                        SymbolDictionary sd = (SymbolDictionary)referredToSegmentHeader.getSegData();
                        importSymbols.AddRange(sd.getDictionary());
                        amtImpSymb += sd.amtExpSymb;
                    }
                }
            }
            private HuffmanTable getUserTable(int tablePosition)
            {
                int tableCounter = 0;
                foreach (SegmentHeader referredToSegmentHeader in segHdr.getRtSegments())
                {
                    if (referredToSegmentHeader.segType == 53)
                    {
                        if (tableCounter == tablePosition)
                        {
                            return ((Table)referredToSegmentHeader.getSegData()).getHuffman();
                        }

                        tableCounter++;
                    }
                }

                return null;
            }
            internal override void init(SegmentHeader header, ImgStream sis)
            {
                base.init(header, sis);
                _ = imgStrm.readBits(3); // Dirty read... reserved bits must be 0
                sdrTemplate = imgStrm.readBits(1);              /* Bit 12 */
                sdTemplate = (byte)(imgStrm.readBits(2) & 0xf);/* Bit 10-11 */
                isCodeCtxRetained = imgStrm.readBits(1) == 1;     /* Bit 9 */
                isCodeCtxUsed = imgStrm.readBits(1) == 1;         /* Bit 8 */
                sdHuffAggInstSel = imgStrm.readBits(1);             /* Bit 7 */
                sdHuffBMSizeSel = imgStrm.readBits(1);              /* Bit 6 */
                sdHuffDecodeWidthSel = imgStrm.readBits(2) & 0xf;       /* Bit 4-5 */
                sdHuffDecodeHeightSel = imgStrm.readBits(2) & 0xf;      /* Bit 2-3 */
                useRefineAggr = imgStrm.readBits(1) == 1;           /* Bit 1 */
                isHuffman = imgStrm.readBits(1) == 1;         /* Bit 0 */
                if (!isHuffman)
                {
                    int noPix = sdTemplate == 0 ? 4 : 1;
                    (sdATX, sdATY) = (new short[noPix], new short[noPix]);
                    for (int i = 0; i < noPix; i++)
                    {
                        sdATX[i] = imgStrm.readSByte();
                        sdATY[i] = imgStrm.readSByte();
                    }
                }
                if (useRefineAggr && sdrTemplate == 0)
                {
                    (sdrATX, sdrATY) = (new short[2], new short[2]);
                    for (int i = 0; i < 2; i++)
                    {
                        sdrATX[i] = imgStrm.readSByte();
                        sdrATY[i] = imgStrm.readSByte();
                    }
                }
                amtExpSymb = imgStrm.readInt(4);
                amtNewSymb = imgStrm.readInt(4);
                if (segHdr.getRtSegments() != null)
                {
                    retrieveImportSymbols();
                }
                else
                {
                    importSymbols = [];
                }

                if (isCodeCtxUsed)
                {
                    SegmentHeader[] rtSegments = segHdr.getRtSegments();
                    for (int i = rtSegments.Length - 1; i >= 0; i--)
                    {
                        if (rtSegments[i].segType == 0)
                        {
                            SymbolDictionary sd = (SymbolDictionary)rtSegments[i].getSegData();
                            if (sd.isCodeCtxRetained)
                            {
                                arithmeticDecoder = sd.arithmeticDecoder;
                                isHuffman = sd.isHuffman;
                                useRefineAggr = sd.useRefineAggr;
                                (sdTemplate, sdrTemplate) = (sd.sdTemplate, sd.sdrTemplate);
                                (sdATX, sdrATX) = (sd.sdATX, sd.sdrATX);
                                (sdATY, sdrATY) = (sd.sdATY, sd.sdrATY);
                                cx = sd.cx;
                            }
                            break;
                        }
                    }
                }
                if (isHuffman)
                {
                    sdTemplate = 0;
                    if (!useRefineAggr)
                    {
                        isCodeCtxRetained = isCodeCtxUsed = false;
                    }
                }
                else
                {
                    sdHuffBMSizeSel = sdHuffDecodeWidthSel = sdHuffDecodeHeightSel = 0;
                }

                if (!useRefineAggr)
                {
                    sdrTemplate = 0;
                }

                if (!isHuffman || !useRefineAggr)
                {
                    sdHuffAggInstSel = 0;
                }
            }
        }
        internal class Region : SegmentData
        {
            internal RegSegInfo regInfo;    /** Region segment information field, 7.4.1 */
            internal JB2Bmp regBmp;        /** Decoded data as pixel values (use row stride/width to wrap line) */
            internal virtual JB2Bmp getRegBmp() => regBmp;
            internal override void init(SegmentHeader header, ImgStream sis)
            {
                base.init(header, sis);
                regInfo = new RegSegInfo(imgStrm);
                regInfo.parseHeader();
            }
        }
        internal class HalftoneRegion : Region
        {
            private byte hDefPix, hTemplate;
            private bool hSkipEnabled, isMMR;
            private int hGridWidth, hGridHeight;            /** Width of the gray-scale image, 7.4.5.1.2.1 */
            private int hGridX, hGridY;                     /** Horizontal offset of the grid, 7.4.5.1.2.3 */
            private int hRegX, hRegY;                       /** Halftone grid vector, 7.4.5.1.3 */
            private JB2Bmp halftoneRegBmp;                  /** Decoded data */
            // Previously decoded data from other regions or dictionaries, stored to use as patterns in this region.
            private List<JB2Bmp> patterns;
            // The procedure is described in JBIG2 ISO standard, 6.6.5.
            internal override JB2Bmp getRegBmp()
            {
                if (halftoneRegBmp != null)
                {
                    return halftoneRegBmp;
                }

                halftoneRegBmp = new JB2Bmp(regInfo.width, regInfo.height);
                if (patterns == null)
                {             /* 6.6.5, page 40 */
                    patterns = [];
                    foreach (SegmentHeader s in segHdr.getRtSegments())
                    {
                        patterns.AddRange((s.getSegData() as PatternDictionary).getDictionary());
                    }
                }
                if (hDefPix == 1)
                {
                    halftoneRegBmp.fillBitmap(0xff);
                }
                // 6.6.5.1 Computing hSkip - At the moment SKIP is not used... we are not able to test it.
                int bitsPerValue = (int)Math.Ceiling(Math.Log(patterns.Count) / Math.Log(2)); /* 3) */
                int[][] grayScaleValues = grayScaleDecoding(bitsPerValue);                      /* 4) */
                // 5), rendering the pattern, described in 6.6.5.2 
                for (int m = 0; m < hGridHeight; m++)               // 1)
                {
                    for (int n = 0; n < hGridWidth; n++)
                    {          // a)
                        int x = computeX(m, n) + hGridX, y = computeY(m, n) + hGridY;
                        int g = grayScaleValues[m][n];              // ii)
                        patterns[g].blit(halftoneRegBmp, x, y, comboOper);
                    }
                }

                return halftoneRegBmp;                                                        /* 6) */
            }
            // Gray-scale image decoding procedure is special for halftone region decoding and is described in Annex C.5 on page 98.
            private int[][] grayScaleDecoding(int bitsPerValue)
            {
                short[] gbAtX = null, gbAtY = null;
                if (!isMMR)
                {
                    short atx0 = (short)((hTemplate <= 1) ? 3 : (hTemplate >= 2 ? 2 : 0));
                    gbAtX = [atx0, -3, 2, -2];
                    gbAtY = [-1, -1, -2, -2];
                }
                JB2Bmp[] grayPlanes = new JB2Bmp[bitsPerValue];                // 1)
                GenRegion genReg = new(imgStrm);
                genReg.setParameters(this, isMMR, hGridHeight, hGridWidth, hTemplate, gbAtX, gbAtY);
                int j = bitsPerValue - 1;                                           // 2)
                grayPlanes[j] = genReg.getRegBmp();
                for (; j > 0;)
                {
                    genReg.regBmp = null;
                    grayPlanes[--j] = genReg.getRegBmp();         // 3) a)
                    for (int byteIndex = 0, y = 0; y < grayPlanes[j].height; y++)
                    {
                        for (int x = 0; x < grayPlanes[j].width; x += 8)
                        {
                            byte newValue = grayPlanes[j + 1].bitmap[byteIndex];
                            byte oldValue = grayPlanes[j].bitmap[byteIndex];
                            grayPlanes[j].bitmap[byteIndex++]
                                = JB2Bmp.combineBytes(oldValue, newValue, ComboOper.XOR);
                        }
                    }
                }
                // Gray-scale decoding procedure, page 98
                int[][] grayVals = new int[hGridHeight][];                   // 4)
                for (int y = 0; y < hGridHeight; y++)
                {
                    grayVals[y] = new int[hGridWidth];
                    for (int x = 0; x < hGridWidth; x += 8)
                    {
                        int minorWidth = hGridWidth - x > 8 ? 8 : hGridWidth - x;
                        int byteIndex = grayPlanes[0].getIdx(x, y);
                        for (int minorX = 0; minorX < minorWidth; minorX++)
                        {
                            int i = minorX + x;
                            grayVals[y][i] = 0;
                            for (j = 0; j < bitsPerValue; j++)
                            {
                                grayVals[y][i] += ((grayPlanes[j].bitmap[byteIndex] >> ((7 - i) & 7)) & 1) * (1 << j);
                            }
                        }
                    }
                }
                return grayVals;
            }
            private int computeX(int m, int n) => shiftAndFill(hGridX + (m * hRegY) + (n * hRegX));
            private int computeY(int m, int n) => shiftAndFill(hGridY + (m * hRegX) - (n * hRegY));
            private int shiftAndFill(int value)
            {
                value >>= 8;            // shift value by 8 and let the leftmost 8 bits be 0
                if (value < 0)
                {        // fill the leftmost 8 bits with 1
                    int bitPosition = (int)(Math.Log(highestOneBit(value)) / Math.Log(2));
                    for (int i = 1; i < 31 - bitPosition; i++) // bit flip
                    {
                        value |= 1 << (31 - i);
                    }
                }
                return value;
                static int highestOneBit(int i)
                {
                    i |= (i >> 1) | (i >> 2) | (i >> 4) | (i >> 8) | (i >> 16);
                    return i - (i >> 1);
                }
            }
            internal override void init(SegmentHeader header, ImgStream sis)
            {
                base.init(header, sis);
                hDefPix = (byte)imgStrm.readBits(1);                /* Bit 7 */
                comboOper = (ComboOper)(imgStrm.readBits(3) & 0xf); /* Bit 4-6 */
                hSkipEnabled = imgStrm.readBits(1) == 1;                 /* Bit 3 */
                hTemplate = (byte)(imgStrm.readBits(2) & 0xf);      /* Bit 1-2 */
                isMMR = imgStrm.readBits(1) == 1;                 /* Bit 0 */
                hGridWidth = imgStrm.readInt(4);
                hGridHeight = imgStrm.readInt(4);
                hGridX = imgStrm.readInt(4);
                hGridY = imgStrm.readInt(4);
                hRegX = imgStrm.readInt(2);
                hRegY = imgStrm.readInt(2);
                dataOffset = imgStrm.Position;
                dataLength = imgStrm.Length - dataOffset;
            }
        }
        internal class GenRefineRegion : Region
        {
            internal class Template0
            {
                internal int cxIdx;
                internal virtual int form(int c1, int c2, int c3, int c4, int c5) => (c1 << 10) | (c2 << 7) | (c3 << 4) | (c4 << 1) | c5;
            }
            internal class Template1 : Template0
            {
                internal override int form(int c1, int c2, int c3, int c4, int c5) => ((c1 & 0x02) << 8) | (c2 << 6) | ((c3 & 0x03) << 4) | (c4 << 1) | c5;
            }
            private static readonly Template0 T0 = new()
            { cxIdx = 0x100 };
            private static readonly Template1 T1 = new()
            { cxIdx = 0x080 };
            /** Generic refinement region segment flags, 7.4.7.2 */
            private bool isTPGROn, fOverride;
            private int refDX, refDY, templateID;
            //private Template0	template;
            private short[] grAtX, grAtY;   /** Generic refinement region segment AT flags, 7.4.7.3 */
            private JB2Bmp refBmp;          /** Decoded data as pixel values (use row stride/width to wrap line) */
            private bool[] grAtOverride;
            private ArithmDecoder arithDecoder;
            private CXStats cx;
            public GenRefineRegion() { }
            public GenRefineRegion(ImgStream subInputStream = null)
            {
                imgStrm = subInputStream;
                regInfo = new RegSegInfo(subInputStream);
            }
            internal override JB2Bmp getRegBmp()
            {
                if (regBmp != null)
                {
                    return regBmp;
                }

                int isLineTypicalPredicted = 0;     /* 6.3.5.6 - 1) */
                refBmp ??= getGrReference();
                arithDecoder ??= new ArithmDecoder(imgStrm);
                cx ??= new CXStats(8192);
                regBmp = new JB2Bmp(regInfo.width, regInfo.height); /* 6.3.5.6 - 2) */
                if (templateID == 0)                    // AT pixel may only occur in template 0
                {
                    updateOverride();
                }

                int padWidth = (regBmp.width + 7) & -8;
                int dxStride = isTPGROn ? -refDY * refBmp.stride : 0;
                Template0 template = templateID == 0 ? T0 : T1;
                for (int y = 0; y < regBmp.height; y++)
                {   /* 6.3.5.6 - 3 */
                    if (isTPGROn)                                       /* 6.3.5.6 - 3 b) */
                    {
                        isLineTypicalPredicted ^= arithDecoder.decodeBit(template.cxIdx, cx);
                    }

                    if (isLineTypicalPredicted == 0)                    /* 6.3.5.6 - 3 c) */
                    {
                        decodeOptimized(y, regBmp.width, regBmp.stride, refBmp.stride);
                    }
                    else                                                /* 6.3.5.6 - 3 d) */
                    {
                        decodeTypicalPredictedLine(y, regBmp.width, regBmp.stride, refBmp.stride, padWidth, dxStride);
                    }
                }
                return regBmp;                                      /* 6.3.5.6 - 4) */
            }
            private JB2Bmp getGrReference()
            {
                SegmentHeader[] segments = segHdr.getRtSegments();
                Region region = (Region)segments[0].getSegData();
                return region.getRegBmp();
            }
            private void decodeOptimized(int lnNum, int width, int stride, int refStride)
            {
                // Offset of the reference bitmap with respect to the bitmap being decoded
                // For example: if referenceDY = -1, y is 1 HIGHER that currY
                int curLine = lnNum - refDY;
                int refByteIdx = refBmp.getIdx(Math.Max(0, -refDX), curLine);
                int byteIdx = regBmp.getIdx(Math.Max(0, refDX), lnNum);
                Template0 template = templateID == 0 ? T0 : T1;
                int c1, c2, c3, c4, c5, w1 = 0, w2 = 0, w3 = 0, w4 = 0;
                if (curLine >= 1 && (curLine - 1) < refBmp.height)
                {
                    w1 = refBmp.getInt(refByteIdx - refStride);
                }

                if (curLine >= 0 && curLine < refBmp.height)
                {
                    w2 = refBmp.getInt(refByteIdx);
                }

                if (curLine >= -1 && curLine + 1 < refBmp.height)
                {
                    w3 = refBmp.getInt(refByteIdx + refStride);
                }

                refByteIdx++;
                if (lnNum >= 1)
                {
                    w4 = regBmp.getInt(byteIdx - stride);
                }

                byteIdx++;
                int modReferenceDX = refDX % 8, shiftOffset = 6 + modReferenceDX,
                    modRefByteIdx = refByteIdx % refStride;
                if (shiftOffset >= 0)
                {
                    c1 = (shiftOffset >= 8 ? 0 : (int)((uint)w1 >> shiftOffset)) & 0x07;
                    c2 = (shiftOffset >= 8 ? 0 : (int)((uint)w2 >> shiftOffset)) & 0x07;
                    c3 = (shiftOffset >= 8 ? 0 : (int)((uint)w3 >> shiftOffset)) & 0x07;
                    if (shiftOffset == 6 && modRefByteIdx > 1)
                    {
                        if (curLine >= 1 && (curLine - 1) < refBmp.height)
                        {
                            c1 |= (refBmp.getInt(refByteIdx - refStride - 2) << 2) & 0x04;
                        }

                        if (curLine >= 0 && curLine < refBmp.height)
                        {
                            c2 |= (refBmp.getInt(refByteIdx - 2) << 2) & 0x04;
                        }

                        if (curLine >= -1 && curLine + 1 < refBmp.height)
                        {
                            c3 |= (refBmp.getInt(refByteIdx + refStride - 2) << 2) & 0x04;
                        }
                    }
                    if (shiftOffset == 0)
                    {
                        w1 = w2 = w3 = 0;
                        if (modRefByteIdx < refStride - 1)
                        {
                            if (curLine >= 1 && (curLine - 1) < refBmp.height)
                            {
                                w1 = refBmp.getInt(refByteIdx - refStride);
                            }

                            if (curLine >= 0 && curLine < refBmp.height)
                            {
                                w2 = refBmp.getInt(refByteIdx);
                            }

                            if (curLine >= -1 && curLine + 1 < refBmp.height)
                            {
                                w3 = refBmp.getInt(refByteIdx + refStride);
                            }
                        }
                        refByteIdx++;
                    }
                }
                else
                {
                    c1 = (w1 << 1) & 0x07;
                    c2 = (w2 << 1) & 0x07;
                    c3 = (w3 << 1) & 0x07;
                    w1 = w2 = w3 = 0;
                    if (modRefByteIdx < refStride - 1)
                    {
                        if (curLine >= 1 && (curLine - 1) < refBmp.height)
                        {
                            w1 = refBmp.getInt(refByteIdx - refStride);
                        }

                        if (curLine >= 0 && curLine < refBmp.height)
                        {
                            w2 = refBmp.getInt(refByteIdx);
                        }

                        if (curLine >= -1 && curLine + 1 < refBmp.height)
                        {
                            w3 = refBmp.getInt(refByteIdx + refStride);
                        }

                        refByteIdx++;
                    }
                    c1 |= (int)((uint)w1 >> 7) & 0x07;
                    c2 |= (int)((uint)w2 >> 7) & 0x07;
                    c3 |= (int)((uint)w3 >> 7) & 0x07;
                }
                c4 = (int)((uint)w4 >> 6);
                c5 = 0;
                int modBitsToTrim = (2 - modReferenceDX) % 8;
                w1 <<= modBitsToTrim;
                w2 <<= modBitsToTrim;
                w3 <<= modBitsToTrim;
                w4 <<= 2;
                for (int x = 0; x < width; x++)
                {
                    int minX = x & 0x07;
                    int tval = template.form(c1, c2, c3, c4, c5);
                    int bit = arithDecoder.decodeBit(!fOverride ? tval
                        : overrideAtTemplate0(tval, x, lnNum, regBmp.bitmap[regBmp.getIdx(x, lnNum)], minX), cx);
                    regBmp.setPixel(x, lnNum, (byte)bit);
                    c1 = ((c1 << 1) | (0x01 & (int)((uint)w1 >> 7))) & 0x07;
                    c2 = ((c2 << 1) | (0x01 & (int)((uint)w2 >> 7))) & 0x07;
                    c3 = ((c3 << 1) | (0x01 & (int)((uint)w3 >> 7))) & 0x07;
                    c4 = ((c4 << 1) | (0x01 & (int)((uint)w4 >> 7))) & 0x07;
                    c5 = bit;
                    if ((x - refDX) % 8 == 5)
                    {
                        if (((x - refDX) / 8) + 1 >= refBmp.stride)
                        {
                            w1 = w2 = w3 = 0;
                        }
                        else
                        {
                            w1 = (curLine >= 1 && (curLine - 1 < refBmp.height))
                                ? refBmp.getInt(refByteIdx - refStride) : 0;
                            w2 = (curLine >= 0 && curLine < refBmp.height)
                                ? refBmp.getInt(refByteIdx) : 0;
                            w3 = (curLine >= -1 && (curLine + 1) < refBmp.height)
                                ? refBmp.getInt(refByteIdx + refStride) : 0;
                        }
                        refByteIdx++;
                    }
                    else
                    {
                        w1 <<= 1;
                        w2 <<= 1;
                        w3 <<= 1;
                    }
                    if (minX == 5 && lnNum >= 1)
                    {
                        w4 = ((x >> 3) + 1 >= regBmp.stride) ? 0 : regBmp.getInt(byteIdx - stride);
                        byteIdx++;
                    }
                    else
                    {
                        w4 <<= 1;
                    }
                }
            }
            private void updateOverride()
            {
                if (grAtX == null || grAtY == null || grAtX.Length != grAtY.Length)
                {
                    return;
                }

                grAtOverride = new bool[grAtX.Length];
                if (templateID == 0)
                {
                    if (grAtX[0] != -1 && grAtY[0] != -1)
                    {
                        grAtOverride[0] = fOverride = true;
                    }

                    if (grAtX[1] != -1 && grAtY[1] != -1)
                    {
                        grAtOverride[1] = fOverride = true;
                    }
                }
                else
                {
                    fOverride = false;
                }
            }
            private void decodeTypicalPredictedLine(int lnNum, int width,
                    int stride, int refStride, int pad, int dltRefStride)
            {
                int curLn = lnNum - refDY;                  // Offset of the reference bitmap with respect 
                int refByteIdx = refBmp.getIdx(0, curLn);   // to the bitmap being decoded. For example: 
                int byteIdx = regBmp.getIdx(0, lnNum);      // if grReferenceDY = -1, y is 1 HIGHER that currY
                if (templateID == 0)
                {
                    decodeTypicalPredictedLineTemplate0(lnNum, width, stride, refStride,
                                pad, dltRefStride, byteIdx, curLn, refByteIdx);
                }
                else
                {
                    decodeTypicalPredictedLineTemplate1(lnNum, width, stride, refStride,
                                pad, dltRefStride, byteIdx, curLn, refByteIdx);
                }
            }
            private void decodeTypicalPredictedLineTemplate0(int lnNum, int width, int rowStride,
                    int refRowStride, int padWidth, int deltaRefStride, int byteIdx, int lnCur, int refByteIdx)
            {
                int prevLn = (lnNum > 0) ? regBmp.getInt(byteIdx - rowStride) : 0;
                int prevRefLn = (lnCur > 0 && lnCur <= refBmp.height)
                    ? refBmp.getInt(refByteIdx - refRowStride + deltaRefStride) << 4 : 0;
                int curRefLn = (lnCur >= 0 && lnCur < refBmp.height)
                    ? refBmp.getInt(refByteIdx + deltaRefStride) << 1 : 0;
                int nxtRefLn = (lnCur > -2 && lnCur < (refBmp.height - 1))
                    ? refBmp.getInt(refByteIdx + refRowStride + deltaRefStride) : 0;
                int ctx = ((prevLn >> 5) & 0x6) | ((nxtRefLn >> 2) & 0x30)
                        | (curRefLn & 0x180) | (prevRefLn & 0xc00);
                int cxCtx, nextByte;
                for (int x = 0; x < padWidth; x = nextByte, refByteIdx++)
                {
                    nextByte = x + 8;
                    int res = 0, minorWidth = width - x > 8 ? 8 : width - x;
                    bool readNextByte = nextByte < width;
                    bool refReadNextByte = nextByte < refBmp.width;
                    int yOffset = deltaRefStride + 1;
                    if (lnNum > 0)
                    {
                        prevLn = (prevLn << 8)
                            | (readNextByte ? regBmp.getInt(byteIdx - rowStride + 1) : 0);
                    }

                    if (lnCur > 0 && lnCur <= refBmp.height)
                    {
                        prevRefLn = (prevRefLn << 8)
                            | (refReadNextByte ? refBmp.getInt(refByteIdx - refRowStride + yOffset) << 4 : 0);
                    }

                    if (lnCur >= 0 && lnCur < refBmp.height)
                    {
                        curRefLn = (curRefLn << 8)
                            | (refReadNextByte ? refBmp.getInt(refByteIdx + yOffset) << 1 : 0);
                    }

                    if (lnCur > -2 && lnCur < (refBmp.height - 1))
                    {
                        nxtRefLn = (nxtRefLn << 8)
                            | (refReadNextByte ? refBmp.getInt(refByteIdx + refRowStride + yOffset) : 0);
                    }

                    for (int minX = 0; minX < minorWidth; minX++)
                    {
                        bool isPixelTypicalPredicted = false;
                        int bit = 0, bmpVal = (ctx >> 4) & 0x1FF;               // i)
                        if (bmpVal == 0x1ff)
                        {
                            isPixelTypicalPredicted = true;
                            bit = 1;
                        }
                        else if (bmpVal == 0x00)
                        {
                            isPixelTypicalPredicted = true;
                            bit = 0;
                        }
                        if (!isPixelTypicalPredicted)
                        {
                            cxCtx = !fOverride ? ctx : overrideAtTemplate0(ctx, x + minX, lnNum, res, minX);
                            bit = arithDecoder.decodeBit(cxCtx, cx);
                        }
                        int toShift = 7 - minX;
                        res |= bit << toShift;
                        ctx = ((ctx & 0xdb6) << 1) | bit | ((prevLn >> (toShift + 5)) & 0x002)
                                | ((nxtRefLn >> (toShift + 2)) & 0x010)
                                | ((curRefLn >> toShift) & 0x080)
                                | ((prevRefLn >> toShift) & 0x400);
                    }
                    regBmp.bitmap[byteIdx++] = (byte)res;
                }
            }
            private void decodeTypicalPredictedLineTemplate1(int lnNum, int width, int stride,
                    int refStride, int pad, int dltRefStride, int byteIdx, int curLine, int refByteIdx)
            {
                int nextByte, prevLn = (lnNum > 0)
                    ? regBmp.getInt(byteIdx - stride) : 0;
                int prevRefLn = (curLine > 0 && curLine <= refBmp.height)
                    ? refBmp.getInt(byteIdx - refStride + dltRefStride) << 2 : 0;
                int curRefLn = (curLine >= 0 && curLine < refBmp.height)
                    ? refBmp.getInt(byteIdx + dltRefStride) : 0;
                int nxtRefLn = (curLine > -2 && curLine < (refBmp.height - 1))
                    ? refBmp.getInt(byteIdx + refStride + dltRefStride) : 0;
                int ctx = ((prevLn >> 5) & 0x6) | ((nxtRefLn >> 2) & 0x30)
                            | (curRefLn & 0xc0) | (prevRefLn & 0x200);
                int grRefVal = ((nxtRefLn >> 2) & 0x70) | (curRefLn & 0xc0)
                                | (prevRefLn & 0x700);
                for (int x = 0; x < pad; x = nextByte, refByteIdx++)
                {
                    nextByte = x + 8;
                    int result = 0, minWidth = width - x > 8 ? 8 : width - x, yOff = dltRefStride + 1;
                    bool readNextByte = nextByte < width, refReadNextByte = nextByte < refBmp.width;
                    if (lnNum > 0)
                    {
                        prevLn = (prevLn << 8)
                            | (readNextByte ? regBmp.getInt(byteIdx - stride + 1) : 0);
                    }

                    if (curLine > 0 && curLine <= refBmp.height)
                    {
                        prevRefLn = (prevRefLn << 8)
                            | (refReadNextByte ? refBmp.getInt(refByteIdx - refStride + yOff) << 2 : 0);
                    }

                    if (curLine >= 0 && curLine < refBmp.height)
                    {
                        curRefLn = (curRefLn << 8)
                            | (refReadNextByte ? refBmp.getInt(refByteIdx + yOff) : 0);
                    }

                    if (curLine > -2 && curLine < (refBmp.height - 1))
                    {
                        nxtRefLn = (nxtRefLn << 8)
                            | (refReadNextByte ? refBmp.getInt(refByteIdx + refStride + yOff) : 0);
                    }

                    for (int minorX = 0; minorX < minWidth; minorX++)
                    {
                        int bmpVal = (grRefVal >> 4) & 0x1ff;       // i)
                        int bit = (bmpVal == 0x1ff) ? 1
                            : (bmpVal == 0x00 ? 0 : arithDecoder.decodeBit(ctx, cx));
                        int toShift = 7 - minorX;
                        result |= bit << toShift;
                        ctx = ((ctx & 0x0d6) << 1) | bit | ((prevLn >> (toShift + 5)) & 0x002)
                                | ((nxtRefLn >> (toShift + 2)) & 0x010) | ((curRefLn >> toShift) & 0x040)
                                | ((prevRefLn >> toShift) & 0x200);
                        grRefVal = ((grRefVal & 0x0db) << 1) | ((nxtRefLn >> (toShift + 2)) & 0x010)
                                | ((curRefLn >> toShift) & 0x080) | ((prevRefLn >> toShift) & 0x400);
                    }
                    regBmp.bitmap[byteIdx++] = (byte)result;
                }
            }
            private int overrideAtTemplate0(int ctx, int x, int y, int result, int minX)
            {
                if (grAtOverride[0])
                {
                    ctx = (ctx & 0xfff7) | (grAtY[0] == 0 && grAtX[0] >= -minX
                            ? ((result >> (7 - (minX + grAtX[0]))) & 0x1) << 3
                            : getPixel(regBmp, x + grAtX[0], y + grAtY[0]) << 3);
                }

                if (grAtOverride[1])
                {
                    ctx = (ctx & 0xefff) | (grAtY[1] == 0 && grAtX[1] >= -minX
                        ? ((result >> (7 - (minX + grAtX[1]))) & 0x1) << 12
                        : getPixel(refBmp, x + grAtX[1] + refDX, y + grAtY[1] + refDY) << 12);
                }

                return ctx;
            }
            private byte getPixel(JB2Bmp b, int x, int y) => x < 0 || x >= b.width ? (byte)0 : y < 0 || y >= b.height ? (byte)0 : b.getPixel(x, y);
            internal override void init(SegmentHeader header, ImgStream sis)
            {
                base.init(header, sis);
                _ = imgStrm.readBits(6);                        // Dirty read...
                isTPGROn = imgStrm.readBits(1) == 1;        /* Bit 1 */
                templateID = imgStrm.readBits(1);           /* Bit 0 */
                if (templateID == 0)
                {
                    (grAtX, grAtY) = (new short[2], new short[2]);
                    grAtX[0] = imgStrm.readSByte();         /* Byte 0 */
                    grAtY[0] = imgStrm.readSByte();         /* Byte 1 */
                    grAtX[1] = imgStrm.readSByte();         /* Byte 2 */
                    grAtY[1] = imgStrm.readSByte();         /* Byte 3 */
                }
            }
            internal void setParameters(CXStats cx, ArithmDecoder arithmeticDecoder,
                    int grTemplate, int regionWidth, int regionHeight, JB2Bmp grReference, int grReferenceDX,
                    int grReferenceDY, short[] grAtX, short[] grAtY)
            {
                this.cx = cx ?? this.cx;
                arithDecoder = arithmeticDecoder ?? arithDecoder;
                (templateID, refBmp, regBmp, isTPGROn) = (grTemplate, grReference, null, false);
                (this.refDX, this.grAtX) = (grReferenceDX, grAtX);
                (this.refDY, this.grAtY) = (grReferenceDY, grAtY);
                (regInfo.width, regInfo.height) = (regionWidth, regionHeight);
            }
        }
        internal class TextRegion : Region
        {
            private int sbrTemplate, sbdsOffset, defPix, isTransposed, refCorner, logSBStrips, sbStrips, amtOfSymb;
            private bool useRefinement, isHuffman;
            private int sbHuffRSize, sbHuffRDY, sbHuffRDX,      /** Text region segment Huffman flags, 7.4.3.1.2 */
                    sbHuffRDHeight, sbHuffRDWidth, sbHuffDT, sbHuffDS, sbHuffFS;
            private short[] sbrATX, sbrATY;                     /** Text region refinement AT flags, 7.4.3.1.3 */
            /** Number of symbol instances, 7.4.3.1.4 */
            private long amtOfSymbInst, currentS;               /** Further parameters */
            private List<JB2Bmp> symbols = [];
            private ArithmDecoder arithmeticDecoder;
            private GenRefineRegion genRefineReg;
            private CXStats cxIADT, cxIAFS, cxIADS, cxIAIT, cxIARI, cxIARDW, cxIARDH, cxIAID, cxIARDX, cxIARDY, cx;
            private int symbolCodeLength;                       /** codeTable including a code to each symbol used in that region */
            private HuffmanTable symbolCodeTable;
            /** User-supplied tables * */
            private HuffmanTable fsTable, dsTable, table, rdwTable, rdhTable, rdxTable, rdyTable, rSizeTable;
            internal override JB2Bmp getRegBmp()
            {
                if (!isHuffman)
                {
                    cxIADT ??= new CXStats();
                    cxIAFS ??= new CXStats();
                    cxIADS ??= new CXStats();
                    cxIAIT ??= new CXStats();
                    cxIARI ??= new CXStats();
                    cxIARDW ??= new CXStats();
                    cxIARDH ??= new CXStats();
                    cxIAID ??= new CXStats(1 << symbolCodeLength);
                    cxIARDX ??= new CXStats();
                    cxIARDY ??= new CXStats();
                    arithmeticDecoder ??= new ArithmDecoder(imgStrm);
                }
                regBmp = new JB2Bmp(regInfo.width, regInfo.height);
                if (defPix != 0)                  /* 1) */
                {
                    regBmp.fillBitmap(0xff);
                }

                decodeSymbolInstances();
                return regBmp;                  /* 4) */
            }
            private long decodeStripT()
            {
                long stripT;
                if (!isHuffman)
                {
                    stripT = arithmeticDecoder.decodeInt(cxIADT);
                }
                else if (sbHuffDT != 3)
                {
                    stripT = HuffmanTable.getStdTbl(11 + sbHuffDT).decode(imgStrm);
                }
                else
                {       /* 6.4.6 */
                    if (table == null)
                    {    // TODO test user-specified table
                        int dtNr = 0;
                        if (sbHuffFS == 3)
                        {
                            dtNr++;
                        }

                        if (sbHuffDS == 3)
                        {
                            dtNr++;
                        }

                        table = getUserTable(dtNr);
                    }
                    stripT = table.decode(imgStrm);
                }
                return stripT * -sbStrips;
            }
            private void decodeSymbolInstances()
            {
                long stripT = decodeStripT(), firstS = 0, instanceCounter = 0; /* Last two sentences of 6.4.5 2) */
                while (instanceCounter < amtOfSymbInst)
                { /* 6.4.5 3 a) */
                    stripT += decodeDT();
                    bool first = true;                              /* 3 c) symbol instances in the strip */
                    for (currentS = 0; ; instanceCounter++)
                    {       // do until OOB
                        if (first)
                        {                               /* 3 c) i) - first symbol instance in the strip */
                            currentS = firstS += decodeDfS();/* 6.4.7 */
                            first = false;
                        }                                           /* 3 c) ii) - the remaining symbol instances in the strip */
                        else
                        {
                            long idS = decodeIdS();                 /* 6.4.8 */
                            if (idS == long.MaxValue || instanceCounter >= amtOfSymbInst)
                            {
                                break;
                            }

                            currentS += idS + sbdsOffset;
                        }
                        long t = stripT + decodeCurrentT();         /* 3 c) iii) */
                        long id = decodeID();                       /* 3 c) iv) */
                        long r = decodeRI();                        /* 3 c) v) */
                        JB2Bmp ib = decodeIb(r, id);                /* 6.4.11 */
                        blit(ib, t);                                /* vi) */
                    }
                }
            }
            private long decodeDT()
            {           /* 3) b)  6.4.6 */
                long dT = isHuffman
                    ? (sbHuffDT == 3) ? table.decode(imgStrm)
                        : HuffmanTable.getStdTbl(11 + sbHuffDT).decode(imgStrm)
                    : arithmeticDecoder.decodeInt(cxIADT);
                return dT * sbStrips;
            }
            private long decodeDfS()
            {
                return !isHuffman
                    ? arithmeticDecoder.decodeInt(cxIAFS)
                    : sbHuffFS != 3 ? HuffmanTable.getStdTbl(6 + sbHuffFS).decode(imgStrm) : (fsTable ??= getUserTable(0)).decode(imgStrm);
            }
            private long decodeIdS()
            {
                return !isHuffman
                    ? arithmeticDecoder.decodeInt(cxIADS)
                    : sbHuffDS != 3
                    ? HuffmanTable.getStdTbl(8 + sbHuffDS).decode(imgStrm)
                    : (dsTable ??= getUserTable((sbHuffFS == 3) ? 1 : 0)).decode(imgStrm);
            }
            private long decodeCurrentT() => sbStrips == 1 ? 0 : isHuffman ? imgStrm.readBits(logSBStrips) : arithmeticDecoder.decodeInt(cxIAIT);
            private long decodeID()
            {
                return !isHuffman
                    ? arithmeticDecoder.decodeIAID(symbolCodeLength, cxIAID)
                    : (symbolCodeTable == null) ? imgStrm.readBits(symbolCodeLength)
                                                 : symbolCodeTable.decode(imgStrm);
            }
            private long decodeRI() => !useRefinement ? 0 : isHuffman ? imgStrm.readBits(1) : arithmeticDecoder.decodeInt(cxIARI);
            private JB2Bmp decodeIb(long r, long id)
            {
                JB2Bmp ib;
                if (r == 0)
                {
                    ib = symbols[(int)id];
                }
                else
                {                                  /* 1) - 4) */
                    long rdw = decodeRdw(), rdh = decodeRdh(), rdx = decodeRdx(), rdy = decodeRdy();
                    if (isHuffman)
                    {             /* 5) */
                        _ = decodeSymInRefSize();
                        imgStrm.skipBits();
                    }
                    JB2Bmp ibo = symbols[(int)id];      /* 6) */
                    int wo = ibo.width, genRegRefDX = (int)((rdw >> 1) + rdx),
                        ho = ibo.height, genRegRefDY = (int)((rdh >> 1) + rdy);
                    genRefineReg ??= new GenRefineRegion(imgStrm);
                    genRefineReg.setParameters(cx, arithmeticDecoder, sbrTemplate,
                            (int)(wo + rdw), (int)(ho + rdh), ibo, genRegRefDX, genRegRefDY, sbrATX, sbrATY);
                    ib = genRefineReg.getRegBmp();
                    if (isHuffman)               /* 7 */
                    {
                        imgStrm.skipBits();
                    }
                }
                return ib;
            }
            private long decodeRdw()
            {
                if (!isHuffman)
                {
                    return arithmeticDecoder.decodeInt(cxIARDW);
                }

                if (sbHuffRDWidth != 3)
                {
                    return HuffmanTable.getStdTbl(14 + sbHuffRDWidth).decode(imgStrm);
                }

                if (rdwTable == null)
                { // TODO test user-specified table
                    int rdwNr = 0;
                    if (sbHuffFS == 3)
                    {
                        rdwNr++;
                    }

                    if (sbHuffDS == 3)
                    {
                        rdwNr++;
                    }

                    if (sbHuffDT == 3)
                    {
                        rdwNr++;
                    }

                    rdwTable = getUserTable(rdwNr);
                }
                return rdwTable.decode(imgStrm);
            }
            private long decodeRdh()
            {
                if (!isHuffman)
                {
                    return arithmeticDecoder.decodeInt(cxIARDH);
                }

                if (sbHuffRDHeight != 3)
                {
                    return HuffmanTable.getStdTbl(14 + sbHuffRDHeight).decode(imgStrm);
                }

                if (rdhTable == null)
                {
                    int rdhNr = 0;
                    if (sbHuffFS == 3)
                    {
                        rdhNr++;
                    }

                    if (sbHuffDS == 3)
                    {
                        rdhNr++;
                    }

                    if (sbHuffDT == 3)
                    {
                        rdhNr++;
                    }

                    if (sbHuffRDWidth == 3)
                    {
                        rdhNr++;
                    }

                    rdhTable = getUserTable(rdhNr);
                }
                return rdhTable.decode(imgStrm);
            }
            private long decodeRdx()
            {
                if (!isHuffman)
                {
                    return arithmeticDecoder.decodeInt(cxIARDX);
                }

                if (sbHuffRDX != 3)
                {
                    return HuffmanTable.getStdTbl(14 + sbHuffRDX).decode(imgStrm);
                }

                if (rdxTable == null)
                {
                    int rdxNr = 0;
                    if (sbHuffFS == 3)
                    {
                        rdxNr++;
                    }

                    if (sbHuffDS == 3)
                    {
                        rdxNr++;
                    }

                    if (sbHuffDT == 3)
                    {
                        rdxNr++;
                    }

                    if (sbHuffRDWidth == 3)
                    {
                        rdxNr++;
                    }

                    if (sbHuffRDHeight == 3)
                    {
                        rdxNr++;
                    }

                    rdxTable = getUserTable(rdxNr);
                }
                return rdxTable.decode(imgStrm);
            }
            private long decodeRdy()
            {
                if (!isHuffman)
                {
                    return arithmeticDecoder.decodeInt(cxIARDY);
                }

                if (sbHuffRDY != 3)
                {
                    return HuffmanTable.getStdTbl(14 + sbHuffRDY).decode(imgStrm);
                }

                if (rdyTable == null)
                {
                    int rdyNr = 0;
                    if (sbHuffFS == 3)
                    {
                        rdyNr++;
                    }

                    if (sbHuffDS == 3)
                    {
                        rdyNr++;
                    }

                    if (sbHuffDT == 3)
                    {
                        rdyNr++;
                    }

                    if (sbHuffRDWidth == 3)
                    {
                        rdyNr++;
                    }

                    if (sbHuffRDHeight == 3)
                    {
                        rdyNr++;
                    }

                    if (sbHuffRDX == 3)
                    {
                        rdyNr++;
                    }

                    rdyTable = getUserTable(rdyNr);
                }
                return rdyTable.decode(imgStrm);
            }
            private long decodeSymInRefSize()
            {
                if (sbHuffRSize == 0)
                {
                    return HuffmanTable.getStdTbl(1).decode(imgStrm);
                }

                if (rSizeTable == null)
                {
                    int rSizeNr = 0;
                    if (sbHuffFS == 3)
                    {
                        rSizeNr++;
                    }

                    if (sbHuffDS == 3)
                    {
                        rSizeNr++;
                    }

                    if (sbHuffDT == 3)
                    {
                        rSizeNr++;
                    }

                    if (sbHuffRDWidth == 3)
                    {
                        rSizeNr++;
                    }

                    if (sbHuffRDHeight == 3)
                    {
                        rSizeNr++;
                    }

                    if (sbHuffRDX == 3)
                    {
                        rSizeNr++;
                    }

                    if (sbHuffRDY == 3)
                    {
                        rSizeNr++;
                    }

                    rSizeTable = getUserTable(rSizeNr);
                }
                return rSizeTable.decode(imgStrm);
            }
            private void blit(JB2Bmp ib, long t)
            {
                if (isTransposed == 0 && (refCorner == 2 || refCorner == 3))
                {
                    currentS += ib.width - 1;
                }
                else if (isTransposed == 1 && (refCorner == 0 || refCorner == 2))
                {
                    currentS += ib.height - 1;
                }

                long s = currentS;                      /* vii) */
                if (isTransposed == 1)                  /* viii) */
                {
                    (t, s) = (s, t);
                }

                if (refCorner != 1)
                {
                    if (refCorner == 0)         // BL
                    {
                        t -= ib.height - 1;
                    }
                    else if (refCorner == 2)
                    {  // BR
                        t -= ib.height - 1;
                        s -= ib.width - 1;
                    }
                    else if (refCorner == 3)        // TR
                    {
                        s -= ib.width - 1;
                    }
                }
                ib.blit(regBmp, (int)s, (int)t, comboOper); /* x) */
                if (isTransposed == 0 && (refCorner == 0 || refCorner == 1))
                {
                    currentS += ib.width - 1;
                }

                if (isTransposed == 1 && (refCorner == 1 || refCorner == 3))
                {
                    currentS += ib.height - 1;
                }
            }
            private void initSymbols()
            {
                foreach (SegmentHeader segment in segHdr.getRtSegments())
                {
                    if (segment.segType == 0)
                    {
                        SymbolDictionary sd = (SymbolDictionary)segment.getSegData();
                        sd.cxIAID = cxIAID;
                        symbols.AddRange(sd.getDictionary());
                    }
                }

                amtOfSymb = symbols.Count;
            }
            private HuffmanTable getUserTable(int tablePosition)
            {
                int tableCounter = 0;
                foreach (SegmentHeader referredToSegmentHeader in segHdr.getRtSegments())
                {
                    if (referredToSegmentHeader.segType == 53)
                    {
                        if (tableCounter == tablePosition)
                        {
                            return ((Table)referredToSegmentHeader.getSegData()).getHuffman();
                        }
                        else
                        {
                            tableCounter++;
                        }
                    }
                }

                return null;
            }
            private void symbolIDCodeLengths()
            {
                List<HuffmanTable.Code> runCodeTable = [];         /* 1) - 2) */
                for (int i = 0; i < 35; i++)
                {
                    int prefLen = imgStrm.readBits(4) & 0xf;
                    if (prefLen > 0)
                    {
                        runCodeTable.Add(new HuffmanTable.Code(prefLen, 0, i, false));
                    }
                }
                HuffmanTable ht = new(runCodeTable);
                long prevLen = 0;                        /* 3) - 5) */
                List<HuffmanTable.Code> sbSymCodes = [];
                for (int cnt = 0; cnt < amtOfSymb;)
                {
                    long code = ht.decode(imgStrm);
                    if (code < 32)
                    {
                        if (code > 0)
                        {
                            sbSymCodes.Add(new HuffmanTable.Code((int)code, 0, cnt, false));
                        }

                        prevLen = code;
                        cnt++;
                    }
                    else
                    {
                        long runLen = 0, curLen = 0;
                        if (code == 32)
                        {
                            runLen = 3 + imgStrm.readBits(2);
                            if (cnt > 0)
                            {
                                curLen = prevLen;
                            }
                        }
                        else if (code == 33)
                        {
                            runLen = 3 + imgStrm.readBits(3);
                        }
                        else if (code == 34)
                        {
                            runLen = 11 + imgStrm.readBits(7);
                        }

                        for (int j = 0; j < runLen; j++, cnt++)
                        {
                            if (curLen > 0)
                            {
                                sbSymCodes.Add(new HuffmanTable.Code((int)curLen, 0, cnt, false));
                            }
                        }
                    }
                }
                imgStrm.skipBits();                     /* 6) - Skip remaining bits in the last Byte */
                symbolCodeTable = new HuffmanTable(sbSymCodes); /* 7) */
            }
            internal override void init(SegmentHeader header, ImgStream sis)
            {
                base.init(header, sis);
                sbrTemplate = imgStrm.readBits(1);              /* Bit 15 */
                sbdsOffset = imgStrm.readBits(5);               /* Bit 10-14 */
                if (sbdsOffset > 0x0f)
                {
                    sbdsOffset -= 0x20;
                }

                defPix = imgStrm.readBits(1);               /* Bit 9 */
                comboOper = (ComboOper)(imgStrm.readBits(2) & 0x3); /* Bit 7-8 */
                isTransposed = imgStrm.readBits(1);             /* Bit 6 */
                refCorner = imgStrm.readBits(2) & 0x3;      /* Bit 4-5 */
                logSBStrips = imgStrm.readBits(2) & 0x3;        /* Bit 2-3 */
                sbStrips = 1 << logSBStrips;
                useRefinement = imgStrm.readBits(1) == 1;           /* Bit 1 */
                isHuffman = imgStrm.readBits(1) == 1;               /* Bit 0 */
                if (isHuffman)
                {
                    _ = imgStrm.readBits(1);                            // Bit 15 Dirty read...
                    sbHuffRSize = imgStrm.readBits(1);              /* Bit 14 */
                    sbHuffRDY = imgStrm.readBits(2) & 0xf;          /* Bit 12-13 */
                    sbHuffRDX = imgStrm.readBits(2) & 0xf;          /* Bit 10-11 */
                    sbHuffRDHeight = imgStrm.readBits(2) & 0xf;     /* Bit 8-9 */
                    sbHuffRDWidth = imgStrm.readBits(2) & 0xf;      /* Bit 6-7 */
                    sbHuffDT = imgStrm.readBits(2) & 0xf;           /* Bit 4-5 */
                    sbHuffDS = imgStrm.readBits(2) & 0xf;           /* Bit 2-3 */
                    sbHuffFS = imgStrm.readBits(2) & 0xf;           /* Bit 0-1 */
                }
                if (useRefinement && sbrTemplate == 0)
                {
                    (sbrATX, sbrATY) = (new short[2], new short[2]);
                    sbrATX[0] = imgStrm.readSByte();                /* Byte 0 */
                    sbrATY[0] = imgStrm.readSByte();                /* Byte 1 */
                    sbrATX[1] = imgStrm.readSByte();                /* Byte 2 */
                    sbrATY[1] = imgStrm.readSByte();                /* Byte 3 */
                }
                amtOfSymbInst = imgStrm.readInt(4);
                long pixels = regInfo.width * regInfo.height;
                if (pixels < amtOfSymbInst)             // don't decode more than 1 symbol/pixel
                {
                    amtOfSymbInst = pixels;
                }

                if (segHdr.getRtSegments() != null)
                {
                    initSymbols();                                  /* 7.4.3.1.7 */
                }

                if (isHuffman)
                {
                    symbolIDCodeLengths();
                }
                else
                {
                    symbolCodeLength = (int)Math.Ceiling(Math.Log(amtOfSymb) / Math.Log(2));
                }

                if (!useRefinement)
                {
                    sbrTemplate = 0;
                }

                if (sbHuffFS == 2 || sbHuffRDWidth == 2 || sbHuffRDHeight == 2 || sbHuffRDX == 2 || sbHuffRDY == 2)
                {
                    throw new Exception("Huffman flag value of text region segment is not permitted");
                }

                if (!useRefinement)
                {
                    sbHuffRSize = sbHuffRDY = sbHuffRDX = sbHuffRDWidth = sbHuffRDHeight = 0;
                }
            }
            internal void setContexts(CXStats cx, CXStats cxIADT, CXStats cxIAFS, CXStats cxIADS,
                    CXStats cxIAIT, CXStats cxIAID, CXStats cxIARDW, CXStats cxIARDH, CXStats cxIARDX, CXStats cxIARDY)
            {
                this.cx = cx;
                this.cxIADT = cxIADT;
                this.cxIAFS = cxIAFS;
                this.cxIADS = cxIADS;
                this.cxIAIT = cxIAIT;
                this.cxIAID = cxIAID;
                this.cxIARDW = cxIARDW;
                this.cxIARDH = cxIARDH;
                this.cxIARDX = cxIARDX;
                this.cxIARDY = cxIARDY;
            }
            internal void setParameters(ArithmDecoder arithmeticDecoder, bool isHuffman, int sbw,
                    int sbh, long sbNumInstances, int sbNumSyms, int sbrTemplate,
                    short[] sbrATX, short[] sbrATY, List<JB2Bmp> sbSyms, int sbSymCodeLen)
            {
                this.arithmeticDecoder = arithmeticDecoder;
                this.isHuffman = isHuffman;
                this.regInfo.width = sbw;
                this.regInfo.height = sbh;
                this.amtOfSymbInst = sbNumInstances;
                this.amtOfSymb = sbNumSyms;
                this.sbrTemplate = sbrTemplate;
                this.sbrATX = sbrATX;
                this.sbrATY = sbrATY;
                this.symbols = sbSyms;
                this.symbolCodeLength = sbSymCodeLen;
                this.useRefinement = true;
                this.comboOper = 0;
                this.refCorner = this.sbStrips = 1;
                defPix = isTransposed = sbdsOffset = sbHuffFS = sbHuffDS = sbHuffDT
                    = sbHuffRDWidth = sbHuffRDHeight = sbHuffRDX = sbHuffRDY = sbHuffRSize = 0;
            }
        }
        internal class GenRegion : Region
        {
            internal bool useExtTemplates, isTPGDon;            /** Generic region segment flags, 7.4.6.2 */
            private int gbTemplate;
            private short[] gbAtX, gbAtY;                               /** Generic region segment AT flags, 7.4.6.3 */
            private bool[] gbAtOverride;
            private bool fOverride, isMMR;
            private ArithmDecoder arithDecoder;
            private CXStats cx;
            private MMRDecompressor mmrDecompressor;
            public GenRegion()
            {
            }
            public GenRegion(ImgStream subInputStream)
            {
                this.imgStrm = subInputStream;
                this.regInfo = new RegSegInfo(subInputStream);
            }
            internal override JB2Bmp getRegBmp()
            {
                if (regBmp != null)
                {
                    return regBmp;
                }

                if (isMMR)
                {
                    mmrDecompressor ??= new MMRDecompressor(regInfo.width, regInfo.height, imgStrm, dataLength);

                    regBmp = mmrDecompressor.uncompress();  /* 6.2.6 */
                }
                else
                {
                    updateOverrideFlags();                          // ARITHMETIC DECODER PROCEDURE for generic region segments
                    arithDecoder ??= new ArithmDecoder(imgStrm);
                    cx ??= new CXStats(65536);                      /* 6.2.5.7 - 2) */
                    regBmp = new JB2Bmp(regInfo.width, regInfo.height);
                    int paddedWidth = (regBmp.width + 7) & -8;
                    for (int ltp = 0, line = 0; line < regBmp.height; line++)
                    {
                        if (isTPGDon)                               /* 6.2.5.7 - 3 b) */
                        {
                            ltp ^= decodeSLTP();
                        }

                        if (ltp != 1)                               /* 6.2.5.7 - 3 c) */
                        {
                            decodeLine(line, regBmp.width, regBmp.stride, paddedWidth);
                        }
                        else if (line > 0)
                        {
                            copyLineAbove(line);
                        }
                    }
                }
                return regBmp;
            }
            private int decodeSLTP()
            {
                int ctx = 0;
                switch (gbTemplate)
                {
                    case 0: ctx = 0x9b25; break;
                    case 1: ctx = 0x795; break;
                    case 2: ctx = 0xe5; break;
                    case 3: ctx = 0x195; break;
                }
                return arithDecoder.decodeBit(ctx, cx);
            }
            private void decodeLine(int lnNo, int width, int stride, int padWidth)
            {
                int byteIndex = regBmp.getIdx(0, lnNo), idx = byteIndex - stride;
                switch (gbTemplate)
                {
                    case 0:
                        if (!useExtTemplates)
                        {
                            decodeTemplate0a(lnNo, width, stride, padWidth, byteIndex, idx);
                        }
                        else
                        {
                            decodeTemplate0b(lnNo, width, stride, padWidth, byteIndex, idx);
                        }

                        break;
                    case 1: decodeTemplate1(lnNo, width, stride, padWidth, byteIndex, idx); break;
                    case 2: decodeTemplate2(lnNo, width, stride, padWidth, byteIndex, idx); break;
                    case 3: decodeTemplate3(lnNo, width, padWidth, byteIndex, idx); break;
                }
            }
            private void copyLineAbove(int lineNumber)
            {
                int targetByteIndex = lineNumber * regBmp.stride;
                int sourceByteIndex = targetByteIndex - regBmp.stride;
                for (int i = 0; i < regBmp.stride; i++)
                {
                    regBmp.bitmap[targetByteIndex++] = regBmp.bitmap[sourceByteIndex++];
                }
            }
            private void decodeTemplate0a(int lineNo, int width, int stride, int padWidth, int byteIdx, int idx)
            {
                int line1 = (lineNo >= 1) ? regBmp.getInt(idx) : 0;
                int line2 = (lineNo >= 2) ? regBmp.getInt(idx - stride) << 6 : 0;
                int ctx = (line1 & 0xf0) | (line2 & 0x3800), nextByte;
                for (int x = 0; x < padWidth; x = nextByte, idx++)
                {
                    nextByte = x + 8;
                    int minorWidth = width - x > 8 ? 8 : width - x, res = 0;
                    if (lineNo > 0)
                    {
                        line1 = (line1 << 8)
                            | (nextByte < width ? regBmp.getInt(idx + 1) : 0);
                    }

                    if (lineNo > 1)
                    {
                        line2 = (line2 << 8)
                            | (nextByte < width ? regBmp.getInt(idx - stride + 1) << 6 : 0);
                    }

                    for (int minX = 0; minX < minorWidth; minX++)
                    {
                        int toShift = 7 - minX;
                        int cxIdx = !fOverride ? ctx
                            : overrideAtTemplate0a(ctx, x + minX, lineNo, res, minX, toShift);
                        int bit = arithDecoder.decodeBit(cxIdx, cx);
                        res |= bit << toShift;
                        ctx = ((ctx & 0x7bf7) << 1) | bit | ((line1 >> toShift) & 0x10)
                                | ((line2 >> toShift) & 0x800);
                    }
                    regBmp.bitmap[byteIdx++] = (byte)res;
                }
            }
            private void decodeTemplate0b(int lineNo, int width, int stride, int padWidth, int byteIdx, int idx)
            {
                int line1 = (lineNo >= 1) ? regBmp.getInt(idx) : 0;
                int line2 = (lineNo >= 2) ? regBmp.getInt(idx - stride) << 6 : 0;
                int ctx = (line1 & 0xf0) | (line2 & 0x3800), nextByte;
                for (int x = 0; x < padWidth; x = nextByte, idx++)
                {
                    nextByte = x + 8;
                    int minorWidth = width - x > 8 ? 8 : width - x, res = 0;    /* 6.2.5.7 3d */
                    if (lineNo > 0)
                    {
                        line1 = (line1 << 8)
                            | (nextByte < width ? regBmp.getInt(idx + 1) : 0);
                    }

                    if (lineNo > 1)
                    {
                        line2 = (line2 << 8)
                            | (nextByte < width ? regBmp.getInt(idx - stride + 1) << 6 : 0);
                    }

                    for (int minX = 0; minX < minorWidth; minX++)
                    {
                        int toShift = 7 - minX;
                        int cxIdx = (!fOverride) ? ctx
                            : overrideAtTemplate0b(ctx, x + minX, lineNo, res, minX, toShift);
                        int bit = arithDecoder.decodeBit(cxIdx, cx);
                        res |= bit << toShift;
                        ctx = ((ctx & 0x7bf7) << 1) | bit | ((line1 >> toShift) & 0x10)
                                | ((line2 >> toShift) & 0x800);
                    }
                    regBmp.bitmap[byteIdx++] = (byte)res;
                }
            }
            private void decodeTemplate1(int lineNo, int width, int stride, int padWidth, int byteIdx, int idx)
            {
                int line1 = (lineNo >= 1) ? regBmp.getInt(idx) : 0;
                int line2 = (lineNo >= 2) ? regBmp.getInt(idx - stride) << 5 : 0;
                int ctx = ((line1 >> 1) & 0x1f8) | ((line2 >> 1) & 0x1e00);
                int cxCtx, nextByte;
                for (int x = 0; x < padWidth; x = nextByte, idx++)
                {
                    nextByte = x + 8;
                    int minorWidth = width - x > 8 ? 8 : width - x, res = 0;    /* 6.2.5.7 3d */
                    if (lineNo >= 1)
                    {
                        line1 = (line1 << 8)
                            | (nextByte < width ? regBmp.getInt(idx + 1) : 0);
                    }

                    if (lineNo >= 2)
                    {
                        line2 = (line2 << 8)
                            | (nextByte < width ? regBmp.getInt(idx - stride + 1) << 5 : 0);
                    }

                    for (int minX = 0; minX < minorWidth; minX++)
                    {
                        cxCtx = !fOverride ? ctx
                            : overrideAtTemplate1(ctx, x + minX, lineNo, res, minX);
                        int bit = arithDecoder.decodeBit(cxCtx, cx);
                        res |= bit << (7 - minX);
                        int toShift = 8 - minX;
                        ctx = ((ctx & 0xefb) << 1) | bit | ((line1 >> toShift) & 0x8)
                                | ((line2 >> toShift) & 0x200);
                    }
                    regBmp.bitmap[byteIdx++] = (byte)res;
                }
            }
            private void decodeTemplate2(int lnNum, int width, int stride, int padWidth, int byteIdx, int idx)
            {
                int line1 = (lnNum >= 1) ? regBmp.getInt(idx) : 0;
                int line2 = (lnNum >= 2) ? regBmp.getInt(idx - stride) << 4 : 0;
                int ctx = ((line1 >> 3) & 0x7c) | ((line2 >> 3) & 0x380), nextByte;
                for (int x = 0; x < padWidth; x = nextByte, idx++)
                {
                    nextByte = x + 8;
                    int minorWidth = width - x > 8 ? 8 : width - x, res = 0;    /* 6.2.5.7 3d */
                    if (lnNum >= 1)
                    {
                        line1 = (line1 << 8)
                            | (nextByte < width ? regBmp.getInt(idx + 1) : 0);
                    }

                    if (lnNum >= 2)
                    {
                        line2 = (line2 << 8)
                            | (nextByte < width ? regBmp.getInt(idx - stride + 1) << 4 : 0);
                    }

                    for (int minX = 0; minX < minorWidth; minX++)
                    {
                        int cxCtx = !fOverride ? ctx : overrideAtTemplate2(ctx, x + minX, lnNum, res, minX);
                        int bit = arithDecoder.decodeBit(cxCtx, cx);
                        res |= bit << (7 - minX);
                        int toShift = 10 - minX;
                        ctx = ((ctx & 0x1bd) << 1) | bit | ((line1 >> toShift) & 0x4) | ((line2 >> toShift) & 0x80);
                    }
                    regBmp.bitmap[byteIdx++] = (byte)res;
                }
            }
            private void decodeTemplate3(int lnNo, int width, int padWidth, int byteIdx, int idx)
            {
                int line1 = (lnNo >= 1) ? regBmp.getInt(idx) : 0;
                int ctx = (line1 >> 1) & 0x70, nxtByte;
                for (int x = 0; x < padWidth; x = nxtByte, idx++)
                {
                    nxtByte = x + 8;
                    int minorWidth = width - x > 8 ? 8 : width - x, res = 0;    /* 6.2.5.7 3d */
                    if (lnNo >= 1)
                    {
                        line1 = (line1 << 8) | (nxtByte < width ? regBmp.getInt(idx + 1) : 0);
                    }

                    for (int minX = 0; minX < minorWidth; minX++)
                    {
                        int cxCtx = !fOverride ? ctx : overrideAtTemplate3(ctx, x + minX, lnNo, res, minX);
                        int bit = arithDecoder.decodeBit(cxCtx, cx);
                        res |= bit << (7 - minX);
                        ctx = ((ctx & 0x1f7) << 1) | bit | ((line1 >> (8 - minX)) & 0x010);
                    }
                    regBmp.bitmap[byteIdx++] = (byte)res;
                }
            }
            private void updateOverrideFlags()
            {
                if (gbAtX == null || gbAtY == null)
                {
                    return;
                }

                if (gbAtX.Length != gbAtY.Length)
                {
                    return;
                }

                gbAtOverride = new bool[gbAtX.Length];
                switch (gbTemplate)
                {
                    case 0:
                        if (!useExtTemplates)
                        {
                            if (gbAtX[0] != 3 || gbAtY[0] != -1)
                            {
                                setOverrideFlag(0);
                            }

                            if (gbAtX[1] != -3 || gbAtY[1] != -1)
                            {
                                setOverrideFlag(1);
                            }

                            if (gbAtX[2] != 2 || gbAtY[2] != -2)
                            {
                                setOverrideFlag(2);
                            }

                            if (gbAtX[3] != -2 || gbAtY[3] != -2)
                            {
                                setOverrideFlag(3);
                            }
                        }
                        else
                        {
                            if (gbAtX[0] != -2 || gbAtY[0] != 0)
                            {
                                setOverrideFlag(0);
                            }

                            if (gbAtX[1] != 0 || gbAtY[1] != -2)
                            {
                                setOverrideFlag(1);
                            }

                            if (gbAtX[2] != -2 || gbAtY[2] != -1)
                            {
                                setOverrideFlag(2);
                            }

                            if (gbAtX[3] != -1 || gbAtY[3] != -2)
                            {
                                setOverrideFlag(3);
                            }

                            if (gbAtX[4] != 1 || gbAtY[4] != -2)
                            {
                                setOverrideFlag(4);
                            }

                            if (gbAtX[5] != 2 || gbAtY[5] != -1)
                            {
                                setOverrideFlag(5);
                            }

                            if (gbAtX[6] != -3 || gbAtY[6] != 0)
                            {
                                setOverrideFlag(6);
                            }

                            if (gbAtX[7] != -4 || gbAtY[7] != 0)
                            {
                                setOverrideFlag(7);
                            }

                            if (gbAtX[8] != 2 || gbAtY[8] != -2)
                            {
                                setOverrideFlag(8);
                            }

                            if (gbAtX[9] != 3 || gbAtY[9] != -1)
                            {
                                setOverrideFlag(9);
                            }

                            if (gbAtX[10] != -2 || gbAtY[10] != -2)
                            {
                                setOverrideFlag(10);
                            }

                            if (gbAtX[11] != -3 || gbAtY[11] != -1)
                            {
                                setOverrideFlag(11);
                            }
                        }
                        break;
                    case 1: if (gbAtX[0] != 3 || gbAtY[0] != -1) { setOverrideFlag(0); } break;
                    case 2: if (gbAtX[0] != 2 || gbAtY[0] != -1) { setOverrideFlag(0); } break;
                    case 3: if (gbAtX[0] != 2 || gbAtY[0] != -1) { setOverrideFlag(0); } break;
                }
            }
            private void setOverrideFlag(int index) => gbAtOverride[index] = fOverride = true;
            private int overrideAtTemplate0a(int ctx, int x, int y, int res, int minX, int toShift)
            {
                if (gbAtOverride[0])
                {
                    ctx = (ctx & 0xffef) | (gbAtY[0] == 0 && gbAtX[0] >= -minX
                                                ? ((res >> (toShift - gbAtX[0])) & 0x1) << 4
                                                : getPixel(x + gbAtX[0], y + gbAtY[0]) << 4);
                }

                if (gbAtOverride[1])
                {
                    ctx = (ctx & 0xfbff) | (gbAtY[1] == 0 && gbAtX[1] >= -minX
                                                ? ((res >> (toShift - gbAtX[1])) & 0x1) << 10
                                                : getPixel(x + gbAtX[1], y + gbAtY[1]) << 10);
                }

                if (gbAtOverride[2])
                {
                    ctx = (ctx & 0xf7ff) | (gbAtY[2] == 0 && gbAtX[2] >= -minX
                                                ? ((res >> (toShift - gbAtX[2])) & 0x1) << 11
                                                : getPixel(x + gbAtX[2], y + gbAtY[2]) << 11);
                }

                if (gbAtOverride[3])
                {
                    ctx = (ctx & 0x7fff) | (gbAtY[3] == 0 && gbAtX[3] >= -minX
                                                ? ((res >> (toShift - gbAtX[3])) & 0x1) << 15
                                                : getPixel(x + gbAtX[3], y + gbAtY[3]) << 15);
                }

                return ctx;
            }
            private int overrideAtTemplate0b(int ctx, int x, int y, int res, int minX, int shft)
            {
                if (gbAtOverride[0])
                {
                    ctx = (ctx & 0xfffd) | (gbAtY[0] == 0 && gbAtX[0] >= -minX
                                            ? ((res >> (shft - gbAtX[0])) & 0x1) << 1
                                            : getPixel(x + gbAtX[0], y + gbAtY[0]) << 1);
                }

                if (gbAtOverride[1])
                {
                    ctx = (ctx & 0xdfff) | (gbAtY[1] == 0 && gbAtX[1] >= -minX
                                            ? ((res >> (shft - gbAtX[1])) & 0x1) << 13
                                            : getPixel(x + gbAtX[1], y + gbAtY[1]) << 13);
                }

                if (gbAtOverride[2])
                {
                    ctx = (ctx & 0xfdff) | (gbAtY[2] == 0 && gbAtX[2] >= -minX
                                            ? ((res >> (shft - gbAtX[2])) & 0x1) << 9
                                            : getPixel(x + gbAtX[2], y + gbAtY[2]) << 9);
                }

                if (gbAtOverride[3])
                {
                    ctx = (ctx & 0xbfff) | (gbAtY[3] == 0 && gbAtX[3] >= -minX
                                            ? ((res >> (shft - gbAtX[3])) & 0x1) << 14
                                            : getPixel(x + gbAtX[3], y + gbAtY[3]) << 14);
                }

                if (gbAtOverride[4])
                {
                    ctx = (ctx & 0xefff) | (gbAtY[4] == 0 && gbAtX[4] >= -minX
                                            ? ((res >> (shft - gbAtX[4])) & 0x1) << 12
                                            : getPixel(x + gbAtX[4], y + gbAtY[4]) << 12);
                }

                if (gbAtOverride[5])
                {
                    ctx = (ctx & 0xffdf) | (gbAtY[5] == 0 && gbAtX[5] >= -minX
                                            ? ((res >> (shft - gbAtX[5])) & 0x1) << 5
                                            : getPixel(x + gbAtX[5], y + gbAtY[5]) << 5);
                }

                if (gbAtOverride[6])
                {
                    ctx = (ctx & 0xfffb) | (gbAtY[6] == 0 && gbAtX[6] >= -minX
                                            ? ((res >> (shft - gbAtX[6])) & 0x1) << 2
                                            : getPixel(x + gbAtX[6], y + gbAtY[6]) << 2);
                }

                if (gbAtOverride[7])
                {
                    ctx = (ctx & 0xfff7) | (gbAtY[7] == 0 && gbAtX[7] >= -minX
                                            ? ((res >> (shft - gbAtX[7])) & 0x1) << 3
                                            : getPixel(x + gbAtX[7], y + gbAtY[7]) << 3);
                }

                if (gbAtOverride[8])
                {
                    ctx = (ctx & 0xf7ff) | (gbAtY[8] == 0 && gbAtX[8] >= -minX
                                            ? ((res >> (shft - gbAtX[8])) & 0x1) << 11
                                            : getPixel(x + gbAtX[8], y + gbAtY[8]) << 11);
                }

                if (gbAtOverride[9])
                {
                    ctx = (ctx & 0xffef) | (gbAtY[9] == 0 && gbAtX[9] >= -minX
                                            ? ((res >> (shft - gbAtX[9])) & 0x1) << 4
                                            : getPixel(x + gbAtX[9], y + gbAtY[9]) << 4);
                }

                if (gbAtOverride[10])
                {
                    ctx = (ctx & 0x7fff) | (gbAtY[10] == 0 && gbAtX[10] >= -minX
                                            ? ((res >> (shft - gbAtX[10])) & 0x1) << 15
                                            : getPixel(x + gbAtX[10], y + gbAtY[10]) << 15);
                }

                if (gbAtOverride[11])
                {
                    ctx = (ctx & 0xfdff) | (gbAtY[11] == 0 && gbAtX[11] >= -minX
                                            ? ((res >> (shft - gbAtX[11])) & 0x1) << 10
                                            : getPixel(x + gbAtX[11], y + gbAtY[11]) << 10);
                }

                return ctx;
            }
            private int overrideAtTemplate1(int ctx, int x, int y, int res, int minX)
            {
                ctx &= 0x1ff7;
                return (gbAtY[0] == 0 && gbAtX[0] >= -minX)
                    ? (ctx | (((res >> (7 - (minX + gbAtX[0]))) & 0x1) << 3))
                    : (ctx | (getPixel(x + gbAtX[0], y + gbAtY[0]) << 3));
            }
            private int overrideAtTemplate2(int ctx, int x, int y, int res, int minX)
            {
                ctx &= 0x3fb;
                return (gbAtY[0] == 0 && gbAtX[0] >= -minX)
                    ? (ctx | (((res >> (7 - (minX + gbAtX[0]))) & 0x1) << 2))
                    : (ctx | (getPixel(x + gbAtX[0], y + gbAtY[0]) << 2));
            }
            private int overrideAtTemplate3(int ctx, int x, int y, int res, int minX)
            {
                ctx &= 0x3ef;
                return (gbAtY[0] == 0 && gbAtX[0] >= -minX)
                    ? (ctx | (((res >> (7 - (minX + gbAtX[0]))) & 0x1) << 4))
                    : (ctx | (getPixel(x + gbAtX[0], y + gbAtY[0]) << 4));
            }
            private byte getPixel(int x, int y) => x < 0 || x >= regBmp.width ? (byte)0 : y < 0 || y >= regBmp.height ? (byte)0 : regBmp.getPixel(x, y);
            internal void setParameters(long dataOffset, long dataLength, int gbh, int gbw)
            {
                this.isMMR = true;
                this.dataOffset = dataOffset;
                this.dataLength = dataLength;
                this.regInfo.height = gbh;
                this.regInfo.width = gbw;
                this.mmrDecompressor = null;
                this.regBmp = null;
            }
            internal void setParameters(byte sdTemplate, short[] sdATX, short[] sdATY,
                    int symWidth, int hcHeight, CXStats cx, ArithmDecoder arithmeticDecoder)
            {
                this.isTPGDon = this.isMMR = false;
                this.gbTemplate = sdTemplate;
                this.gbAtX = sdATX;
                this.gbAtY = sdATY;
                this.regInfo.width = symWidth;
                this.regInfo.height = hcHeight;
                this.cx = cx ?? this.cx;
                this.arithDecoder = arithmeticDecoder ?? this.arithDecoder;
                this.mmrDecompressor = null;
                this.regBmp = null;
            }
            internal void setParameters(SegmentData seg, bool mmr, int gbh, int gbw, int gbTemplate, short[] gbAtX, short[] gbAtY)
            {
                this.dataOffset = seg.dataOffset;
                this.dataLength = seg.dataLength;
                this.isMMR = mmr;
                this.regInfo = new RegSegInfo { height = gbh, width = gbw };
                this.gbTemplate = gbTemplate;
                this.isTPGDon = false;
                this.gbAtX = gbAtX;
                this.gbAtY = gbAtY;
            }
            internal override void init(SegmentHeader header, ImgStream sis)
            {
                base.init(header, sis);
                _ = imgStrm.readBits(3); // Dirty read...
                useExtTemplates = imgStrm.readBits(1) == 1;       /* Bit 4 */
                isTPGDon = imgStrm.readBits(1) == 1;              /* Bit 3 */
                gbTemplate = (byte)(imgStrm.readBits(2) & 0xf);     /* Bit 1-2 */
                isMMR = imgStrm.readBits(1) == 1;          /* Bit 0 */
                if (!isMMR)
                {
                    int amtOfGbAt = (gbTemplate == 0) ? (useExtTemplates ? 12 : 4) : 1;
                    (gbAtX, gbAtY) = (new short[amtOfGbAt], new short[amtOfGbAt]);
                    for (int i = 0; i < amtOfGbAt; i++)
                    {
                        gbAtX[i] = imgStrm.readSByte();
                        gbAtY[i] = imgStrm.readSByte();
                    }
                }
                dataOffset = imgStrm.Position;        /* Segment data structure */
                dataLength = imgStrm.Length - dataOffset;
            }
        }
        internal class JBIG2Page
        {
            internal Dictionary<int, SegmentHeader> segments = [];
            private JB2Bmp pgBmp;
            private readonly int pageNumber;
            private readonly PBoxJBig2 document;
            internal JBIG2Page(PBoxJBig2 document, int pageNumber)
            {
                this.document = document;
                this.pageNumber = pageNumber;
            }
            internal SegmentHeader getSegment(int number) => segments.ContainsKey(number) ? segments[number] : (document?.getGlobalSegment(number));
            internal JB2Bmp getBitmap()
            {
                HashSet<int> regTypes = [6, 7, 22, 23, 38, 39, 42, 43];
                if (pgBmp != null || pageNumber < 1)
                {
                    return pgBmp;
                }

                PageInformation pi = (PageInformation)segments.Values
                                            .FirstOrDefault(s => s.segType == 48)?.getSegData();
                if (!pi.isStriped || pi.height != -1)
                {
                    pgBmp = new JB2Bmp(pi.width, pi.height);
                    if (pi.defPix != 0)
                    {
                        pgBmp.fillBitmap(0xff);
                    }

                    foreach (SegmentHeader s in segments.Values)
                    {
                        if (regTypes.Contains(s.segType))
                        {
                            Region r = (Region)s.getSegData();
                            JB2Bmp bmp = r.getRegBmp();
                            if (segments.Values.Count(x => regTypes.Contains(x.segType)) == 1
                            && pi.defPix == 0 && pi.width == bmp.width && pi.height == bmp.height)
                            {
                                pgBmp = bmp;
                            }
                            else
                            {
                                RegSegInfo ri = r.regInfo;
                                bmp.blit(pgBmp, ri.xLoc, ri.yLoc, getComboOper(pi, ri.getComboOper()));
                            }
                        }
                    }
                }
                else
                {
                    int finalHeight = segments.Values.Where(x => x.segType == 50)
                        .Select(x => ((EndOfStripe)x.getSegData()).lineNum + 1).Last(), startLine = 0;
                    pgBmp = new JB2Bmp(pi.width, finalHeight);
                    foreach (SegmentHeader s in segments.Values)
                    {
                        if (s.segType == 50)
                        {
                            startLine = ((EndOfStripe)s.getSegData()).lineNum + 1;
                        }
                        else if (regTypes.Contains(s.segType))
                        {
                            Region r = (Region)s.getSegData();
                            RegSegInfo ri = r.regInfo;
                            r.getRegBmp().blit(pgBmp, ri.xLoc, startLine, getComboOper(pi, ri.getComboOper()));
                        }
                    }
                }
                segments = null;                // allow GC
                return pgBmp;
            }
            private ComboOper getComboOper(PageInformation pi, ComboOper newOperator) => pi.isComboOperOverrideAllow() ? newOperator : pi.getComboOper();
        }
        internal Dictionary<int, JBIG2Page> pages = [];
        internal Dictionary<int, SegmentHeader> globalSegments = [];
        internal SegmentHeader getGlobalSegment(int segmentNr) => globalSegments?[segmentNr];
        internal JBIG2Page getPage(int pageNumber) => pages.ContainsKey(pageNumber) ? pages[pageNumber] : null;
        private void mapStream()
        {
            List<SegmentHeader> segments = [];
            long offset = 0;
            bool typeRand = false;
            bufStr.Seek(0);
            int[] sig = [0x97, 0x4A, 0x42, 0x32, 0x0D, 0x0A, 0x1A, 0x0A];
            if (sig.All(x => x == bufStr.ReadByte()))
            {
                _ = bufStr.readBits(5);                         /* D.4.2 Header flag (1 byte) */
                _ = bufStr.readBits(1);                         // Bit 2 - Indicates if extended templates are used
                bool fPgNums = bufStr.readBits(1) == 1;   // numPagesKnown 
                typeRand = bufStr.readBits(1) == 0;         // Bit 0 - Indicates file organisation type
                if (!fPgNums)                                // D.4.3 Number of pages (field is only present 
                {
                    _ = bufStr.readInt();                       // if amount of pages are 'NOT unknown')
                }

                offset = bufStr.Position;
            }
            globalSegments ??= [];
            for (int segmentType = 0; segmentType != 51 && !bufStr.EOF(4); bufStr.Seek(offset))
            {
                SegmentHeader segment = new(this, bufStr, offset);
                segmentType = segment.segType;
                if (segment.pgAssoc != 0)
                {
                    JBIG2Page page = getPage(segment.pgAssoc);
                    if (page == null)
                    {
                        pages[segment.pgAssoc] = page = new JBIG2Page(this, segment.pgAssoc);
                    }

                    page.segments[segment.segNo] = segment;
                }
                else
                {
                    globalSegments[segment.segNo] = segment;
                }

                segments.Add(segment);
                offset = bufStr.Position;
                if (!typeRand)
                {
                    offset += segment.DataLen;
                }
            }
            if (typeRand)                                           // Random: Data part starts after all the headers
            {
                foreach (SegmentHeader s in segments)
                {
                    s.DataStart = offset;
                    offset += s.DataLen;
                }
            }
        }
        public PBoxJBig2(byte[] input, byte[] glob = null)
        {
            if (input == null)
            {
                throw new Exception("imageInputStream must not be null");
            }

            bufStr = new ImgStream(input);
            if (glob != null)
            {
                globalSegments = new PBoxJBig2(glob).globalSegments;
            }

            mapStream();
        }
        public Image decodeImage(int page = 1)
        {
            JBIG2Page jBIG2Page = getPage(page);
            return jBIG2Page?.getBitmap().ToImage();
        }
    }
}
