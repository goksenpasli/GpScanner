using Extensions;

namespace GpScanner.ViewModel;

public class BatchTxtOcr : InpcBase
{
    public string FilePath
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FilePath));
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