using Extensions;

namespace TwainControl;

public class PdfData : InpcBase
{
    public int PageNumber {
        get { return pageNumber; }

        set {
            if (pageNumber != value)
            {
                pageNumber = value;
                OnPropertyChanged(nameof(PageNumber));
            }
        }
    }

    public bool Selected {
        get { return selected; }

        set {
            if (selected != value)
            {
                selected = value;
                OnPropertyChanged(nameof(Selected));
            }
        }
    }

    private int pageNumber;

    private bool selected;
}