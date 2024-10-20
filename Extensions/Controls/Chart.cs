using System.Windows.Media;

namespace Extensions;

public class Chart : InpcBase
{
    public Brush ChartBrush
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ChartBrush));
            }
        }
    } = Brushes.Gray;

    public double ChartValue
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ChartValue));
            }
        }
    }

    public string Description
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Description));
            }
        }
    } = string.Empty;
}