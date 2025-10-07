namespace TwainControl;

public class ExtendedPdfData : PdfData
{
    public string FileName
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FileName));
            }
        }
    }
}
