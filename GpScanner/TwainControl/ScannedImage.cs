using System.Windows.Media.Imaging;
using Extensions;

namespace TwainControl
{
    public class ScannedImage : InpcBase
    {
        public BitmapFrame Resim
        {
            get => resim;

            set
            {
                if (resim != value)
                {
                    resim = value;
                    OnPropertyChanged(nameof(Resim));
                }
            }
        }

        public bool Seçili
        {
            get => seçili;

            set
            {
                if (seçili != value)
                {
                    seçili = value;
                    OnPropertyChanged(nameof(Seçili));
                }
            }
        }

        private BitmapFrame resim;

        private bool seçili;
    }
}