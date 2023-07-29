using System;
using System.Windows.Media;

namespace Extensions
{
    public class ContributionData : InpcBase
    {
        private DateTime? contrubutionDate;
        private int count;
        private Brush stroke;

        public DateTime? ContrubutionDate
        {
            get => contrubutionDate;
            set
            {
                if(contrubutionDate != value)
                {
                    contrubutionDate = value;
                    OnPropertyChanged(nameof(ContrubutionDate));
                }
            }
        }

        public int Count
        {
            get => count;
            set
            {
                if(count != value)
                {
                    count = value;
                    OnPropertyChanged(nameof(Count));
                }
            }
        }

        public Brush Stroke
        {
            get => stroke;
            set
            {
                if(stroke != value)
                {
                    stroke = value;
                    OnPropertyChanged(nameof(Stroke));
                }
            }
        }
    }
}
