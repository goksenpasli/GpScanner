namespace TwainWpf.TwainNative
{
    /// <summary>
    /// Twain spec ICAP_COMPRESSION values.
    /// </summary>
    public enum Compression : short
    {
        None = 0,

        PackBits = 1,

        Group31d = 2,

        Group31dEol = 3,

        Group32d = 4,

        Group4 = 5,

        Jpeg = 6,

        Lzw = 7,

        Jbig = 8,

        Png = 9,

        Rle4 = 10,

        Rle8 = 11,

        BitFields = 12
    }
}