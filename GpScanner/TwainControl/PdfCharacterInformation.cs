using System.Drawing;

namespace TwainControl;

public partial class TwainCtrl
{
    internal struct PdfCharacterInformation
    {
        public RectangleF Bounds { get; set; }

        public char Character { get; set; }

        public double FontSize { get; set; }

        public string Word { get; set; }
    }
}