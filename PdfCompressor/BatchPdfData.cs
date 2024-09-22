using Extensions;

namespace PdfCompressor;

public class BatchPdfData : InpcBase
{
    private bool completed;
    private double compressionRatio;
    private string filename;
    private bool ısChecked = true;

    public bool Completed {
        get => completed;
        set {
            if (completed != value)
            {
                completed = value;
                OnPropertyChanged(nameof(Completed));
            }
        }
    }

    public double CompressionRatio {
        get => compressionRatio;
        set {
            if (compressionRatio != value)
            {
                compressionRatio = value;
                OnPropertyChanged(nameof(CompressionRatio));
            }
        }
    }

    public bool IsChecked {
        get => ısChecked;
        set {
            if (ısChecked != value)
            {
                ısChecked = value;
                OnPropertyChanged(nameof(IsChecked));
            }
        }
    }

    public string Filename {
        get => filename;
        set {
            if (filename != value)
            {
                filename = value;
                OnPropertyChanged(nameof(Filename));
            }
        }
    }
}
