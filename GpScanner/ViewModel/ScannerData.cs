using Extensions;
using System.Collections.ObjectModel;

namespace GpScanner.ViewModel;

public class ScannerData : InpcBase
{
    public ObservableCollection<ReminderData> GörülenReminder
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(GörülenReminder));
            }
        }
    } = [];

    public ObservableCollection<ReminderData> Reminder
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Reminder));
            }
        }
    } = [];
}