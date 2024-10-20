using Extensions;
using System.Collections.Generic;

namespace GpScanner.ViewModel;

public class ExtendedContributionData : ContributionData
{
    public IEnumerable<string> Name
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
