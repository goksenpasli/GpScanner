namespace Ocr {
    public class Paper : InpcBase {
        public double Height {
            get => height;

            set {
                if (height != value) {
                    height = value;
                    OnPropertyChanged(nameof(Height));
                }
            }
        }

        public string PaperType {
            get => paperType;

            set {
                if (paperType != value) {
                    paperType = value;
                    OnPropertyChanged(nameof(PaperType));
                }
            }
        }

        public double Width {
            get => width;

            set {
                if (width != value) {
                    width = value;
                    OnPropertyChanged(nameof(Width));
                }
            }
        }

        private double height;

        private string paperType;

        private double width;
    }
}