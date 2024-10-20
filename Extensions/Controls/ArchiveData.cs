using System;
using static Extensions.ShellIcon;

namespace Extensions;

public class ArchiveData : InpcBase
{
    private string dosyaTipi;

    public long Boyut
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Boyut));
            }
        }
    }

    public string Crc
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Crc));
            }
        }
    }

    public string DosyaAdı
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DosyaAdı));
            }
        }
    }

    public string DosyaTipi
    {
        get => GetFileType(DosyaAdı, new SHFILEINFO());
        set
        {
            if (dosyaTipi != value)
            {
                dosyaTipi = value;
                OnPropertyChanged(nameof(DosyaTipi));
            }
        }
    }

    public DateTime DüzenlenmeZamanı
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DüzenlenmeZamanı));
            }
        }
    }

    public bool IsChecked
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(IsChecked));
            }
        }
    }

    public float Oran
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Oran));
            }
        }
    }

    public long SıkıştırılmışBoyut
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SıkıştırılmışBoyut));
            }
        }
    }

    public string TamYol
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(TamYol));
            }
        }
    }
}