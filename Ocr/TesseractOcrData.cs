namespace Ocr;

public class TesseractOcrData : InpcBase
{
    private bool ısEnabled = true;
    private string ocrLangName;
    private string ocrName;
    private double progressValue;

    public bool IsEnabled
    {
        get => ısEnabled;

        set
        {
            if (ısEnabled != value)
            {
                ısEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }
    }

    public string OcrLangName
    {
        get => ocrLangName;
        set
        {
            if (ocrLangName != value)
            {
                ocrLangName = value;
                OnPropertyChanged(nameof(OcrLangName));
            }
        }
    }

    public string OcrName
    {
        get => ocrName;

        set
        {
            if (ocrName != value)
            {
                ocrName = value;
                OnPropertyChanged(nameof(OcrName));
            }
        }
    }

    public double ProgressValue
    {
        get => progressValue;

        set
        {
            if (progressValue != value)
            {
                progressValue = value;
                OnPropertyChanged(nameof(ProgressValue));
            }
        }
    }
}