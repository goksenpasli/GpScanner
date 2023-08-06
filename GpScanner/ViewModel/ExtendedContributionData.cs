using Extensions;
using System.Collections.Generic;

namespace GpScanner.ViewModel;

public class ExtendedContributionData : ContributionData
{
    private IEnumerable<string> name;

    public IEnumerable<string> Name
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
