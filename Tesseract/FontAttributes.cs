namespace Tesseract
{
    public class FontAttributes
    {
        public FontAttributes(FontInfo fontInfo, bool isUnderlined, bool isSmallCaps, int pointSize)
        {
            FontInfo = fontInfo;
            IsUnderlined = isUnderlined;
            IsSmallCaps = isSmallCaps;
            PointSize = pointSize;
        }

        public FontInfo FontInfo { get; }

        public bool IsSmallCaps { get; }

        public bool IsUnderlined { get; }

        public int PointSize { get; }
    }
}