using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TwainControl
{
    public abstract class Deskew
    {
        public static unsafe double GetDeskewAngle(BitmapSource image)
        {
            double scale = Math.Min(1.0, 600.0 / Math.Max(image.PixelWidth, image.PixelHeight));
            TransformedBitmap bmp = new(image, new ScaleTransform(scale, scale));
            FormatConvertedBitmap gray = new(bmp, PixelFormats.Gray8, null, 0);
            gray.Freeze();

            int w = gray.PixelWidth, h = gray.PixelHeight;
            int stride = w;
            int bufferSize = h * stride;
            IntPtr unmanaged = IntPtr.Zero;

            try
            {
                unmanaged = Marshal.AllocHGlobal(bufferSize);
                gray.CopyPixels(new Int32Rect(0, 0, w, h), unmanaged, bufferSize, stride);

                byte* p = (byte*)unmanaged.ToPointer();

                double angle1 = SearchOptimizedUnsafe(p, w, h, -10, 10, 0.8);
                return SearchOptimizedUnsafe(p, w, h, angle1 - 1.5, angle1 + 1.5, 0.2);
            }
            finally
            {
                if (unmanaged != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(unmanaged);
                }
            }
        }

        private static unsafe double SearchOptimizedUnsafe(byte* pixels, int w, int h, double from, double to, double step)
        {
            double cx = w * 0.5;
            double cy = h * 0.5;
            const int yStep = 2;
            const int xStep = 2;

            int sampleStride = Math.Max(1, Math.Min(5000, w * h) / 50);
            int avg = 0, samples = 0;
            for (int i = 0; i < w * h; i += sampleStride)
            {
                avg += pixels[i];
                samples++;
            }
            avg /= Math.Max(1, samples);
            int threshold = 255 - avg;

            int angleCount = (int)Math.Ceiling((to - from) / step) + 1;
            float[] sins = new float[angleCount];
            float[] coss = new float[angleCount];

            for (int i = 0; i < angleCount; i++)
            {
                double a = from + (i * step);
                double rad = a * Math.PI / 180.0;
                sins[i] = (float)Math.Sin(rad);
                coss[i] = (float)Math.Cos(rad);
            }

            object sync = new();
            double bestAngleGlobal = 0;
            double bestScoreGlobal = double.NegativeInfinity;

            _ = Parallel.For(
                0,
                angleCount,
                () => new ThreadState { projection = new long[h], bestScore = double.NegativeInfinity, bestAngle = 0.0 },
                (ai, loopState, local) =>
                {
                    float sin = sins[ai];
                    float cos = coss[ai];

                    Array.Clear(local.projection, 0, h);

                    for (int y = 0; y < h; y += yStep)
                    {
                        int yw = y * w;
                        float dy = y - (float)cy;

                        for (int x = 0; x < w; x += xStep)
                        {
                            int intensity = 255 - pixels[yw + x];
                            if (intensity < threshold)
                            {
                                continue;
                            }

                            float dx = x - (float)cx;
                            int row = (int)((dx * sin) + (dy * cos) + cy);

                            if ((uint)row < (uint)h)
                            {
                                local.projection[row] += intensity;
                            }
                        }
                    }

                    long sum = 0, sumSq = 0;
                    int cnt = 0;
                    for (int i = 0; i < h; i++)
                    {
                        long v = local.projection[i];
                        if (v > 0)
                        {
                            sum += v;
                            sumSq += v * v;
                            cnt++;
                        }
                    }

                    if (cnt > 0)
                    {
                        double mean = (double)sum / cnt;
                        double score = (sumSq / (double)cnt) - (mean * mean);

                        if (score > local.bestScore)
                        {
                            local.bestScore = score;
                            local.bestAngle = from + (ai * step);
                        }
                    }

                    return local;
                },
                localFinal =>
                {
                    lock (sync)
                    {
                        if (localFinal.bestScore > bestScoreGlobal)
                        {
                            bestScoreGlobal = localFinal.bestScore;
                            bestAngleGlobal = localFinal.bestAngle;
                        }
                    }
                });

            return bestAngleGlobal;
        }

        private class ThreadState
        {
            public double bestAngle;
            public double bestScore;
            public long[] projection;
        }
    }
}
