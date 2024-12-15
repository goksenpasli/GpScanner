using System.Drawing;

namespace TwainControl;

public partial class TwainCtrl
{
    internal struct PdfCharacterInformation
    {
        public RectangleF Bounds { get; set; }

        public string Word { get; set; }
    }
}