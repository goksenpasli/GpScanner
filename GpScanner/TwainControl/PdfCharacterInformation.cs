using System.Drawing;
using TwainControl.Properties;

namespace TwainControl;

public partial class TwainCtrl
{
    public static void SaveSettings() => Settings.Default.Save();

    internal struct PdfCharacterInformation
    {
        public RectangleF Bounds { get; set; }

        public char Character { get; set; }

        public double FontSize { get; set; }

        public string Word { get; set; }
    }
}