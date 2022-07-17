using Extensions;

namespace GpScanner.ViewModel
{
    public class OcrData : InpcBase
    {
        public string DisplayName
        {
            get => displayName;

            set
            {
                if (displayName != value)
                {
                    displayName = value;
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        public string OcrName
        {
            get => ocrName; set

            {
                if (ocrName != value)
                {
                    ocrName = value;
                    OnPropertyChanged(nameof(OcrName));
                }
            }
        }

        public double ProgressValue
        {
            get => progressValue;

            set
            {
                if (progressValue != value)
                {
                    progressValue = value;
                    OnPropertyChanged(nameof(ProgressValue));
                }
            }
        }

        private string displayName;

        private string ocrName;

        private double progressValue;
    }
}