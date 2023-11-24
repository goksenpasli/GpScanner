using Extensions;

namespace GpScanner.ViewModel;

public class TessFiles : InpcBase
{
    private bool @checked;
    private string displayName;
    private double fileSize;
    private string name;

    public bool Checked
    {
        get => @checked;

        set
        {
            if (@checked != value)
            {
                @checked = value;
                OnPropertyChanged(nameof(Checked));
            }
        }
    }

    public string DisplayName
    {
        get => displayName;
        set
        {
            if (displayName != value)
            {
                displayName = value;
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public double FileSize
    {
        get => fileSize;
        set
        {
            if (fileSize != value)
            {
                fileSize = value;
                OnPropertyChanged(nameof(FileSize));
            }
        }
    }

    public string Name
    {
        get => name;

        set
        {
            if (name != value)
            {
                name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }
}