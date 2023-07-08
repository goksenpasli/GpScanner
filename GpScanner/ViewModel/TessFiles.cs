using Extensions;

namespace GpScanner.ViewModel;

public class TessFiles : InpcBase
{
    private bool @checked;
    private string name;

    public bool Checked
    {
        get => @checked;

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
        get => name;

        set
        {
            if(name != value)
            {
                name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }
}