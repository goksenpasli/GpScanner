namespace Tesseract
{
    public class FontInfo
    {
        public int Id { get; }

        public bool IsBold { get; }

        public bool IsFixedPitch { get; }

        public bool IsFraktur { get; }

        public bool IsItalic { get; }

        public bool IsSerif { get; }

        public string Name { get; }

        internal FontInfo(string name, int id, bool isItalic, bool isBold, bool isFixedPitch, bool isSerif,
                                                                    bool isFraktur = false)
        {
            Name = name;
            Id = id;

            IsItalic = isItalic;
            IsBold = isBold;
            IsFixedPitch = isFixedPitch;
            IsSerif = isSerif;
            IsFraktur = isFraktur;
        }
    }
}