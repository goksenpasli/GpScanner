using Extensions;

namespace PdfCompressor;

public class BatchPdfData : InpcBase
{
    private bool completed;
    private double compressionRatio;
    private string filename;

    public bool Completed
    {
        get => completed;
        set
        {
            if (completed != value)
            {
                completed = value;
                OnPropertyChanged(nameof(Completed));
            }
        }
    }

    public double CompressionRatio
    {
        get => compressionRatio;
        set
        {
            if (compressionRatio != value)
            {
                compressionRatio = value;
                OnPropertyChanged(nameof(CompressionRatio));
            }
        }
    }

    public string Filename
    {
        get => filename;
        set
        {
            if (filename != value)
            {
                filename = value;
                OnPropertyChanged(nameof(Filename));
            }
        }
    }
}
