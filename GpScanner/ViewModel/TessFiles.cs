using Extensions;

namespace GpScanner.ViewModel;

public class TessFiles : InpcBase
{
    public bool Checked
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Checked));
            }
        }
    }

    public string DisplayName
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public bool Enabled
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Enabled));
            }
        }
    } = true;

    public double FileSize
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FileSize));
            }
        }
    }

    public string Name
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }
}