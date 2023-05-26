using Extensions;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace GpScanner.ViewModel;

[XmlRoot(ElementName = "ScannerData")]
public class ScannerData : InpcBase
{
    [XmlElement(ElementName = "Data")]
    public ObservableCollection<Data> Data {
        get => data;

        set {
            if (data != value)
            {
                data = value;
                OnPropertyChanged(nameof(Data));
            }
        }
    }

    private ObservableCollection<Data> data = new();
}