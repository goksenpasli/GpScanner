using Extensions;
using System.IO;

namespace TwainControl;

public class ExtendedArchiveData : ArchiveData
{
    public FileAttributes Attributes
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Attributes));
            }
        }
    }

    public bool Encrypted
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Encrypted));
            }
        }
    }

    public string HostOs
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(HostOs));
            }
        }
    }

    public string Method
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Method));
            }
        }
    }
}
