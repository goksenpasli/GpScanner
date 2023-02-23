using InpcBase = Extensions.InpcBase;

namespace GpScanner.ViewModel
{
    public class BatchTxtOcr : InpcBase
    {
        public double ProgressValue {
            get => progressValue;

            set {
                if (progressValue != value)
                {
                    progressValue = value;
                    OnPropertyChanged(nameof(ProgressValue));
                }
            }
        }

        private double progressValue;
    }
}