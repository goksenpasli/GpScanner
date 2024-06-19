using System.Windows;

namespace Ocr;

public class Paper : InpcBase
{
    private string category;
    private double height;
    private string paperType;
    private Visibility widespreadPaper = Visibility.Collapsed;
    private double width;

    public string Category
    {
        get => category;

        set
        {
            if (category != value)
            {
                category = value;
                OnPropertyChanged(nameof(Category));
            }
        }
    }

    public double Height
    {
        get => height;

        set
        {
            if (height != value)
            {
                height = value;
                OnPropertyChanged(nameof(Height));
            }
        }
    }

    public string PaperType
    {
        get => paperType;

        set
        {
            if (paperType != value)
            {
                paperType = value;
                OnPropertyChanged(nameof(PaperType));
            }
        }
    }

    public Visibility WidespreadPaper
    {
        get => widespreadPaper;
        set
        {
            if (widespreadPaper != value)
            {
                widespreadPaper = value;
                OnPropertyChanged(nameof(WidespreadPaper));
            }
        }
    }

    public double Width
    {
        get => width;

        set
        {
            if (width != value)
            {
                width = value;
                OnPropertyChanged(nameof(Width));
            }
        }
    }
}