using InpcBase = Extensions.InpcBase;

namespace GpScanner.ViewModel;

public class TessFiles : InpcBase
{
    public bool Checked
    {
        get { return @checked; }
        set
        {
            if(@checked != value)
            {
                @checked = value;
                OnPropertyChanged(nameof(Checked));
            }
        }
    }

    public string Name
    {
        get { return name; }
        set
        {
            if(name != value)
            {
                name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    private bool @checked;

    private string name;
}
