using Extensions;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace GpScanner.ViewModel;

[XmlRoot(ElementName = "ScannerData")]
public class ScannerData : InpcBase
{
    private ObservableCollection<Data> data = new();
    private ObservableCollection<ReminderData> reminder = new();

    [XmlElement(ElementName = "Data")]
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

    [XmlElement(ElementName = "Reminder")]
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