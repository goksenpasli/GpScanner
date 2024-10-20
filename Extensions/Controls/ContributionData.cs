using System;
using System.Windows.Media;

namespace Extensions
{
    public class ContributionData : InpcBase
    {
        public DateTime? ContrubutionDate
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(ContrubutionDate));
                }
            }
        }

        public int Count
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(Count));
                }
            }
        }

        public Brush Stroke
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(Stroke));
                }
            }
        }
    }
}
