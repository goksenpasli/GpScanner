using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TwainControl;

public abstract class Deskew
{
    public static double GetDeskewAngle(BitmapSource image)
    {
        double scale = Math.Min(1.0, 600.0 / Math.Max(image.PixelWidth, image.PixelHeight));
        TransformedBitmap bmp = new(image, new ScaleTransform(scale, scale));
        FormatConvertedBitmap gray = new(bmp, PixelFormats.Gray8, null, 0);
        gray.Freeze();

        int w = gray.PixelWidth, h = gray.PixelHeight;
        int stride = w;
        byte[] pixels = new byte[h * stride];
        gray.CopyPixels(pixels, stride, 0);

        double angle1 = Search(pixels, w, h, -10, 10, 0.8, out _);

        return Search(pixels, w, h, angle1 - 1.5, angle1 + 1.5, 0.1, out _);
    }

    private static double Search(byte[] pixels, int w, int h, double from, double to, double step, out double bestScore)
    {
        double cx = w / 2.0, cy = h / 2.0;
        bestScore = double.NegativeInfinity;
        double bestAngle = 0;

        int avg = 0;
        for (int i = 0; i < pixels.Length; i += 5000)
        {
            avg += pixels[i];
        }

        avg /= Math.Max(1, pixels.Length / 5000);
        int threshold = 255 - avg;

        for (double a = from; a <= to + 1e-6; a += step)
        {
            double rad = a * Math.PI / 180.0;
            double sin = Math.Sin(rad), cos = Math.Cos(rad);
            long[] projection = new long[h];

            for (int y = 0; y < h; y += 2)
            {
                int yw = y * w;
                for (int x = 0; x < w; x += 2)
                {
                    int intensity = 255 - pixels[yw + x];
                    if (intensity < threshold)
                    {
                        continue;
                    }

                    double dx = x - cx;
                    double dy = y - cy;

                    int row = (int)Math.Round((dx * sin) + (dy * cos) + cy);

                    if ((uint)row < (uint)h)
                    {
                        projection[row] += intensity;
                    }
                }
            }

            long sum = 0, sumSq = 0;
            int cnt = 0;
            for (int i = 0; i < h; i++)
            {
                long v = projection[i];
                if (v > 0)
                {
                    sum += v;
                    sumSq += v * v;
                    cnt++;
                }
            }

            if (cnt == 0)
            {
                continue;
            }

            double mean = (double)sum / cnt;
            double score = (sumSq / (double)cnt) - (mean * mean);

            if (score > bestScore)
            {
                bestScore = score;
                bestAngle = a;
            }
        }

        return bestAngle;
    }
}
