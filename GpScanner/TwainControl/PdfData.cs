using Extensions;

namespace TwainControl;

public class PdfData : InpcBase
{
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