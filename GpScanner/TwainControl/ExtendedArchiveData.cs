using Extensions;
using System.IO;

namespace TwainControl;

public class ExtendedArchiveData : ArchiveData
{
    private FileAttributes attributes;
    private string hostOs;
    private string method;

    public FileAttributes Attributes
    {
        get => attributes;
        set
        {
            if (attributes != value)
            {
                attributes = value;
                OnPropertyChanged(nameof(Attributes));
            }
        }
    }

    public string HostOs
    {
        get => hostOs;
        set
        {
            if (hostOs != value)
            {
                hostOs = value;
                OnPropertyChanged(nameof(HostOs));
            }
        }
    }

    public string Method
    {
        get => method;
        set
        {
            if (method != value)
            {
                method = value;
                OnPropertyChanged(nameof(Method));
            }
        }
    }
}
