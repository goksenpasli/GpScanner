using Extensions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GpScanner.ViewModel;

public class Data : InpcBase
{
    private string fileContent = string.Empty;
    private string fileName;
    private int ıd;
    private string qrData;

    public string FileContent
    {
        get => fileContent;

        set
        {
            if (fileContent != value)
            {
                fileContent = value;
                OnPropertyChanged(nameof(FileContent));
            }
        }
    }

    public string FileName
    {
        get => fileName;

        set
        {
            if (fileName != value)
            {
                fileName = value;
                OnPropertyChanged(nameof(FileName));
            }
        }
    }

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id
    {
        get => ıd;

        set
        {
            if (ıd != value)
            {
                ıd = value;
                OnPropertyChanged(nameof(Id));
            }
        }
    }

    public string QrData
    {
        get => qrData;

        set
        {
            if (qrData != value)
            {
                qrData = value;
                OnPropertyChanged(nameof(QrData));
            }
        }
    }
}
