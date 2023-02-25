using InpcBase = Extensions.InpcBase;

namespace GpScanner.ViewModel
{
    public class BatchTxtOcr : InpcBase
    {
        public string FilePath {
            get => filePath; set {

                if (filePath != value)
                {
                    filePath = value;
                    OnPropertyChanged(nameof(FilePath));
                }
            }
        }

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

        private string filePath;

        private double progressValue;
    }
}