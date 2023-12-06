using Extensions;
using System.Collections.ObjectModel;

namespace GpScanner.ViewModel;

public class ScannerData : InpcBase
{
    private ObservableCollection<Data> data = [];
    private ObservableCollection<ReminderData> görülenReminder = [];
    private ObservableCollection<ReminderData> reminder = [];

    public ObservableCollection<Data> Data
    {
        get => data;

        set
        {
            if (data != value)
            {
                data = value;
                OnPropertyChanged(nameof(Data));
            }
        }
    }

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