using Extensions;
using System.Windows.Media;

namespace TwainControl;

public class PdfData : InpcBase, IIndexable
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

    public int Index
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Index));
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