using Extensions;
using System.Windows.Media;

namespace TwainControl;

public class PdfData : InpcBase
{
    public Brush BorderBrush
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(BorderBrush));
            }
        }
    }

    public int PageNumber
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PageNumber));
            }
        }
    }

    public bool Selected
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Selected));
            }
        }
    }
}