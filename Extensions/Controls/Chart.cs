using System.Windows.Media;

namespace Extensions
{
    public class Chart : InpcBase
    {
        public Brush ChartBrush {
            get => chartBrush;

            set {
                if (chartBrush != value)
                {
                    chartBrush = value;
                    OnPropertyChanged(nameof(ChartBrush));
                }
            }
        }

        public double ChartValue {
            get => chartValue;

            set {
                if (chartValue != value)
                {
                    chartValue = value;
                    OnPropertyChanged(nameof(ChartValue));
                }
            }
        }

        public string Description {
            get => description;

            set {
                if (description != value)
                {
                    description = value;
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        private Brush chartBrush = Brushes.Gray;

        private double chartValue;

        private string description = string.Empty;
    }
}