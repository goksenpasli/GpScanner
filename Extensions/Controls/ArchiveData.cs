using System;

namespace Extensions;

public class ArchiveData : InpcBase
{
    public long Boyut
    {
        get { return boyut; }

        set
        {
            if(boyut != value)
            {
                boyut = value;
                OnPropertyChanged(nameof(Boyut));
            }
        }
    }

    public string DosyaAdı
    {
        get { return dosyaAdı; }

        set
        {
            if(dosyaAdı != value)
            {
                dosyaAdı = value;
                OnPropertyChanged(nameof(DosyaAdı));
            }
        }
    }

    public DateTime DüzenlenmeZamanı
    {
        get { return düzenlenmeZamanı; }

        set
        {
            if(düzenlenmeZamanı != value)
            {
                düzenlenmeZamanı = value;
                OnPropertyChanged(nameof(DüzenlenmeZamanı));
            }
        }
    }

    public double Oran
    {
        get { return oran; }

        set
        {
            if(oran != value)
            {
                oran = value;
                OnPropertyChanged(nameof(Oran));
            }
        }
    }

    public long SıkıştırılmışBoyut
    {
        get { return sıkıştırılmışBoyut; }

        set
        {
            if(sıkıştırılmışBoyut != value)
            {
                sıkıştırılmışBoyut = value;
                OnPropertyChanged(nameof(SıkıştırılmışBoyut));
            }
        }
    }

    public string TamYol
    {
        get { return tamYol; }

        set
        {
            if(tamYol != value)
            {
                tamYol = value;
                OnPropertyChanged(nameof(TamYol));
            }
        }
    }

    private long boyut;

    private string dosyaAdı;

    private DateTime düzenlenmeZamanı;

    private double oran;

    private long sıkıştırılmışBoyut;

    private string tamYol;
}