using System.Windows;

namespace Ocr;

public class OcrData : InpcBase
{
    private Rect rect;

    public Rect Rect
    {
        get => rect;

        set
        {
            if (rect != value)
            {
                rect = value;
                OnPropertyChanged(nameof(Rect));
            }
        }
    }

    public string Text
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Text));
            }
        }
    }
}