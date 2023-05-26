using Extensions;
using System.Xml.Serialization;

namespace GpScanner.ViewModel;

[XmlRoot(ElementName = "Data")]
public class Data : InpcBase
{
    [XmlAttribute(AttributeName = "FileContent")]
    public string FileContent {
        get => fileContent;

        set {
            if (fileContent != value)
            {
                fileContent = value;
                OnPropertyChanged(nameof(FileContent));
            }
        }
    }

    [XmlAttribute(AttributeName = "FileName")]
    public string FileName {
        get => fileName;

        set {
            if (fileName != value)
            {
                fileName = value;
                OnPropertyChanged(nameof(FileName));
            }
        }
    }

    [XmlAttribute(AttributeName = "Id")]
    public int Id {
        get => ıd;

        set {
            if (ıd != value)
            {
                ıd = value;
                OnPropertyChanged(nameof(Id));
            }
        }
    }

    [XmlAttribute(AttributeName = "QrData")]
    public string QrData {
        get => qrData;

        set {
            if (qrData != value)
            {
                qrData = value;
                OnPropertyChanged(nameof(QrData));
            }
        }
    }

    private string fileContent;

    private string fileName;

    private int ıd;

    private string qrData;
}