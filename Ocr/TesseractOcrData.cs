namespace Ocr;

public class TesseractOcrData : InpcBase
{
    public bool IsEnabled
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }
    } = true;

    public double OcrFileSize
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(OcrFileSize));
            }
        }
    }

    public string OcrLangName
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(OcrLangName));
            }
        }
    }

    public string OcrName
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(OcrName));
            }
        }
    }

    public double ProgressValue
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ProgressValue));
            }
        }
    }
}