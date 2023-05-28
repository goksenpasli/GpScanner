using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TwainControl;

public class Deskew
{
    public Deskew(BitmapSource bmp) { cBmp = bmp; }

    public double GetAlpha(int Index) { return cAlphaStart + (Index * cAlphaStep); }

    public unsafe Color GetPixelColor(WriteableBitmap wb, int x, int y)
    {
        Pixel* data = (Pixel*)wb.BackBuffer;
        int stride = wb.BackBufferStride / 4;
        wb.Lock();
        Pixel pixel = *(data + (y * stride) + x);
        wb.Unlock();
        return Color.FromRgb(pixel.R, pixel.G, pixel.B);
    }

    public double GetSkewAngle(bool fast = false)
    {
        double sum = 0;
        int count = 0;

        Calc(fast);

        HougLine[] hl = GetTop(20);
        int i;

        for(i = 0; i <= 19; i++)
        {
            sum += hl[i].Alpha;
            count++;
        }

        return sum / count;
    }

    private void Calc(bool fast = false)
    {
        int hMin = cBmp.PixelHeight / 4;
        int hMax = cBmp.PixelHeight * 3 / 4;
        int pixelwidth = fast ? (int)cBmp.Width : cBmp.PixelWidth;
        WriteableBitmap wb = new(cBmp);
        Init();
        int y;
        for(y = hMin; y <= hMax; y++)
        {
            int x;
            for(x = 1; x <= pixelwidth - 2; x++)
            {
                if(IsBlack(x, y, wb) && !IsBlack(x, y + 1, wb))
                {
                    Calc(x, y);
                }
            }
        }
    }

    private void Calc(int x, int y)
    {
        int alpha;
        for(alpha = 0; alpha <= cSteps - 1; alpha++)
        {
            double d = (y * cCosA[alpha]) - (x * cSinA[alpha]);
            int dIndex = CalcDIndex(d);
            int Index = (dIndex * cSteps) + alpha;
            try
            {
                cHMatrix[Index]++;
            } catch(Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }
    }

    private int CalcDIndex(double d) { return Convert.ToInt32(d - cDMin); }

    private HougLine[] GetTop(int Count)
    {
        HougLine[] hl = new HougLine[Count + 1];
        int i;
        for(i = 0; i <= Count - 1; i++)
        {
            hl[i] = new HougLine();
        }

        for(i = 0; i <= cHMatrix.Length - 1; i++)
        {
            if(cHMatrix[i] > hl[Count - 1].Count)
            {
                hl[Count - 1].Count = cHMatrix[i];
                hl[Count - 1].Index = i;
                int j = Count - 1;
                while(j > 0 && hl[j].Count > hl[j - 1].Count)
                {
                    HougLine tmp = hl[j];
                    hl[j] = hl[j - 1];
                    hl[j - 1] = tmp;
                    j--;
                }
            }
        }

        for(i = 0; i <= Count - 1; i++)
        {
            int dIndex = hl[i].Index / cSteps;
            int AlphaIndex = hl[i].Index - (dIndex * cSteps);
            hl[i].Alpha = GetAlpha(AlphaIndex);
            hl[i].d = dIndex + cDMin;
        }

        return hl;
    }

    private void Init()
    {
        cSinA = new double[cSteps];
        cCosA = new double[cSteps];
        int i;
        for(i = 0; i <= cSteps - 1; i++)
        {
            double angle = GetAlpha(i) * Math.PI / 180.0;
            cSinA[i] = Math.Sin(angle);
            cCosA[i] = Math.Cos(angle);
        }

        cDMin = -cBmp.PixelWidth;
        cDCount = (int)(2 * (cBmp.PixelWidth + cBmp.PixelHeight) / cDStep);
        cHMatrix = new int[(cDCount * cSteps) + 1];
    }

    private bool IsBlack(int x, int y, WriteableBitmap wb)
    {
        Color c = GetPixelColor(wb, x, y);
        double luminance = (c.R * 0.299) + (c.G * 0.587) + (c.B * 0.114);
        return luminance < 140;
    }

    private readonly double cAlphaStart = -20;

    private readonly double cAlphaStep = 0.2;

    private readonly BitmapSource cBmp;

    private double[] cCosA;

    private int cDCount;

    private double cDMin;

    private readonly double cDStep = 1;

    private int[] cHMatrix;

    private double[] cSinA;

    private readonly int cSteps = 40 * 5;

    public class HougLine
    {
        public double Alpha;

        public int Count;

        public double d;

        public int Index;
    }

    [StructLayout(LayoutKind.Explicit)]
    protected struct Pixel
    {
        [FieldOffset(0)] public byte B;

        [FieldOffset(1)] public byte G;

        [FieldOffset(2)] public byte R;

        [FieldOffset(3)] public byte A;
    }
}