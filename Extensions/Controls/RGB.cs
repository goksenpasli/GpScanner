using System;
using System.Windows.Media;

namespace Extensions;

public class RGB
{
    public RGB()
    {
        R = 0xff;
        G = 0xff;
        B = 0xff;
    }

    public RGB(double r, double g, double b)
    {
        if (r > 255 || g > 255 || b > 255)
        {
            throw new ArgumentException("RGB must be under 255 (1byte)");
        }

        R = (byte)r;
        G = (byte)g;
        B = (byte)b;
    }

    public byte B { get; set; }

    public byte G { get; set; }

    public byte R { get; set; }

    public Color Color() => new() { R = R, G = G, B = B, A = 255 };

    public string Hex(byte Alpha) => BitConverter.ToString([Alpha, R, G, B]).Replace("-", string.Empty);
}