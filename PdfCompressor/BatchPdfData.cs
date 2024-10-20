using Extensions;

namespace PdfCompressor;

public class BatchPdfData : InpcBase
{
    public bool Completed
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Completed));
            }
        }
    }

    public double CompressionRatio
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CompressionRatio));
            }
        }
    }

    public string Filename
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Filename));
            }
        }
    }

    public bool IsChecked
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(IsChecked));
            }
        }
    } = true;
}
