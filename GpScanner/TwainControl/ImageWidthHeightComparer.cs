using System.Collections.Generic;

namespace TwainControl;

public class ImageWidthHeightComparer : IEqualityComparer<ScannedImage>
{
    public bool Equals(ScannedImage x, ScannedImage y) => (x is null && y is null) || (x is not null && y is not null && x?.Resim?.PixelHeight == y?.Resim?.PixelHeight && x?.Resim?.PixelWidth == y?.Resim?.PixelWidth);

    public int GetHashCode(ScannedImage obj) => new { obj?.Resim?.PixelWidth, obj?.Resim?.PixelHeight }.GetHashCode();
}
