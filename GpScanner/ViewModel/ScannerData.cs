using Extensions;
using System.Collections.ObjectModel;

namespace GpScanner.ViewModel;

public class ScannerData : InpcBase
{
    private ObservableCollection<ReminderData> görülenReminder = [];
    private ObservableCollection<ReminderData> reminder = [];

    public ObservableCollection<ReminderData> GörülenReminder
    {
        get => görülenReminder;
        set
        {
            if (görülenReminder != value)
            {
                görülenReminder = value;
                OnPropertyChanged(nameof(GörülenReminder));
            }
        }
    }

    public ObservableCollection<ReminderData> Reminder
    {
        get => reminder;
        set
        {
            if (reminder != value)
            {
                reminder = value;
                OnPropertyChanged(nameof(Reminder));
            }
        }
    }
}