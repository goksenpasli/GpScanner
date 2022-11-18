using System.ComponentModel;
using System.Windows.Media.Imaging;
using Extensions;

namespace TwainControl
{
    public class ScannedImage : InpcBase
    {
        public ScannedImage()
        {
            PropertyChanged += ScannedImage_PropertyChanged;
        }

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

        public double RotationAngle
        {
            get => rotationAngle;

            set
            {
                if (rotationAngle != value)
                {
                    rotationAngle = value;
                    OnPropertyChanged(nameof(RotationAngle));
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

        private double rotationAngle;

        private bool seçili;

        private void ScannedImage_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "RotationAngle" && RotationAngle != 0)
            {
                Resim = Resim.RotateImage(RotationAngle);
                RotationAngle = 0;
            }
        }
    }
}