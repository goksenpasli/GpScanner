namespace Ocr;

public class Paper : InpcBase
{
    public double Height
    {
        get { return height; }

        set
        {
            if(height != value)
            {
                height = value;
                OnPropertyChanged(nameof(Height));
            }
        }
    }

    public string PaperType
    {
        get { return paperType; }

        set
        {
            if(paperType != value)
            {
                paperType = value;
                OnPropertyChanged(nameof(PaperType));
            }
        }
    }

    public double Width
    {
        get { return width; }

        set
        {
            if(width != value)
            {
                width = value;
                OnPropertyChanged(nameof(Width));
            }
        }
    }

    private double height;

    private string paperType;

    private double width;
}