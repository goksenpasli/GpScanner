using System;
using static Extensions.ShellIcon;

namespace Extensions;

public class ArchiveData : InpcBase
{
    private long boyut;
    private string crc;
    private string dosyaAdı;
    private string dosyaTipi;
    private DateTime düzenlenmeZamanı;
    private float oran;
    private long sıkıştırılmışBoyut;
    private string tamYol;

    public long Boyut
    {
        get => boyut;

        set
        {
            if (boyut != value)
            {
                boyut = value;
                OnPropertyChanged(nameof(Boyut));
            }
        }
    }

    public string Crc
    {
        get => crc;
        set
        {
            if (crc != value)
            {
                crc = value;
                OnPropertyChanged(nameof(Crc));
            }
        }
    }

    public string DosyaAdı
    {
        get => dosyaAdı;

        set
        {
            if (dosyaAdı != value)
            {
                dosyaAdı = value;
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
        get => düzenlenmeZamanı;

        set
        {
            if (düzenlenmeZamanı != value)
            {
                düzenlenmeZamanı = value;
                OnPropertyChanged(nameof(DüzenlenmeZamanı));
            }
        }
    }

    public float Oran
    {
        get => oran;

        set
        {
            if (oran != value)
            {
                oran = value;
                OnPropertyChanged(nameof(Oran));
            }
        }
    }

    public long SıkıştırılmışBoyut
    {
        get => sıkıştırılmışBoyut;

        set
        {
            if (sıkıştırılmışBoyut != value)
            {
                sıkıştırılmışBoyut = value;
                OnPropertyChanged(nameof(SıkıştırılmışBoyut));
            }
        }
    }

    public string TamYol
    {
        get => tamYol;

        set
        {
            if (tamYol != value)
            {
                tamYol = value;
                OnPropertyChanged(nameof(TamYol));
            }
        }
    }
}