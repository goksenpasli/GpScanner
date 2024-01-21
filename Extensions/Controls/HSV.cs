using System;

namespace Extensions;

public static class HSV
{
    public static RGB[] GetSpectrum()
    {
        RGB[] rgbs = new RGB[360];

        for (int h = 0; h < 360; h++)
        {
            rgbs[h] = RGBFromHSV(h, 1f, 1f);
        }

        return rgbs;
    }

    public static RGB[] GradientSpectrum()
    {
        RGB[] rgbs = new RGB[7];

        for (int h = 0; h < 7; h++)
        {
            rgbs[h] = RGBFromHSV(h * 60, 1f, 1f);
        }

        return rgbs;
    }

    public static RGB RGBFromHSV(double h, double s, double v)
    {
        if (h > 360 || h < 0 || s > 1 || s < 0 || v > 1 || v < 0)
        {
            return null;
        }

        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60 % 2) - 1));
        double m = v - c;

        double r = 0, g = 0, b = 0;

        if (h < 60)
        {
            r = c;
            g = x;
        }
        else if (h < 120)
        {
            r = x;
            g = c;
        }
        else if (h < 180)
        {
            g = c;
            b = x;
        }
        else if (h < 240)
        {
            g = x;
            b = c;
        }
        else if (h < 300)
        {
            r = x;
            b = c;
        }
        else if (h <= 360)
        {
            r = c;
            b = x;
        }

        return new RGB((r + m) * 255, (g + m) * 255, (b + m) * 255);
    }
}
