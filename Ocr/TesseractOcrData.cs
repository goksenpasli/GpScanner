using System.Windows;
using Extensions;

namespace Ocr
{
    public class TesseractOcrData : InpcBase
    {
        public bool IsEnabled
        {
            get => ısEnabled;

            set
            {
                if (ısEnabled != value)
                {
                    ısEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public Visibility IsVisible
        {
            get => ısVisible;

            set
            {
                if (ısVisible != value)
                {
                    ısVisible = value;
                    OnPropertyChanged(nameof(IsVisible));
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

        private bool ısEnabled = true;

        private Visibility ısVisible = Visibility.Collapsed;

        private string ocrName;

        private double progressValue;
    }
}
