using System.Windows;

namespace Ocr;

public class Paper : InpcBase
{
    public string Category
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Category));
            }
        }
    }

    public double Height
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Height));
            }
        }
    }

    public string PaperType
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PaperType));
            }
        }
    }

    public Visibility WidespreadPaper
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(WidespreadPaper));
            }
        }
    } = Visibility.Collapsed;

    public double Width
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Width));
            }
        }
    }
}