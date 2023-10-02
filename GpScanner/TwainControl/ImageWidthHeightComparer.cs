using System.Collections.Generic;

namespace TwainControl;

public partial class TwainCtrl
{
    private class ImageWidthHeightComparer : IEqualityComparer<ScannedImage>
    {
        public bool Equals(ScannedImage x, ScannedImage y) => (x == null && y == null) || (x != null && y != null && x.Resim.PixelHeight == y.Resim.PixelHeight && x.Resim.PixelWidth == y.Resim.PixelWidth);

        public int GetHashCode(ScannedImage obj) => new { obj.Resim.PixelWidth, obj.Resim.PixelHeight }.GetHashCode();
    }
}