using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TwainControl;

public abstract class Deskew()
{
    public static double GetDeskewAngle(BitmapSource image)
    {
        BitmapSource grayscaleImage = ConvertToGrayscale(image);
        ImageMoments moments = CalculateImageMoments(grayscaleImage);
        return CalculateSkewAngle(moments);
    }

    private static ImageMoments CalculateImageMoments(BitmapSource image)
    {
        int width = image.PixelWidth;
        int height = image.PixelHeight;
        double m00 = 0, m10 = 0, m01 = 0;

        byte[] pixels = new byte[width * height];
        image.CopyPixels(pixels, width, 0);

        for(int y = 0; y < height; y++)
        {
            for(int x = 0; x < width; x++)
            {
                byte grayValue = pixels[(y * width) + x];

                m00 += grayValue;
                m10 += x * grayValue;
                m01 += y * grayValue;
            }
        }

        double xCenter = m10 / m00;
        double yCenter = m01 / m00;

        double mu20 = 0, mu02 = 0, mu11 = 0;
        for(int y = 0; y < height; y++)
        {
            for(int x = 0; x < width; x++)
            {
                byte grayValue = pixels[(y * width) + x];

                double xShift = x - xCenter;
                double yShift = y - yCenter;

                mu20 += xShift * xShift * grayValue;
                mu02 += yShift * yShift * grayValue;
                mu11 += xShift * yShift * grayValue;
            }
        }

        return new ImageMoments(mu20 / m00, mu02 / m00, mu11 / m00);
    }

    private static double CalculateSkewAngle(ImageMoments moments)
    {
        double skewAngleRad = Math.Atan2(2 * moments.Mu11, moments.Mu20 - moments.Mu02) / 2;
        double skewAngleDeg = skewAngleRad * (180 / Math.PI);
        return skewAngleDeg > 0 ? 90 - skewAngleDeg : -(90 + skewAngleDeg);
    }

    private static BitmapSource ConvertToGrayscale(BitmapSource image) { return new FormatConvertedBitmap(image, PixelFormats.Gray8, null, 0); }

    public class ImageMoments(double mu20, double mu02, double mu11)
    {
        public double Mu02 { get; } = mu02;

        public double Mu11 { get; } = mu11;

        public double Mu20 { get; } = mu20;
    }
}