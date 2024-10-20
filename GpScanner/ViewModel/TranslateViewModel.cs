using Extensions;
using GpScanner.Properties;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Speech.Synthesis;
using System.Windows.Input;

namespace GpScanner.ViewModel;

public class TranslateViewModel : InpcBase
{
    private static SpeechSynthesizer speechSynthesizer;

    static TranslateViewModel()
    {
        speechSynthesizer = new SpeechSynthesizer();
        TtsDilleri = speechSynthesizer.GetInstalledVoices()?.Select(z => z.VoiceInfo?.Name)?.ToList();
    }

    public TranslateViewModel()
    {
        PropertyChanged += TranslateViewModel_PropertyChanged;
        Sıfırla = new RelayCommand<object>(
            parameter =>
            {
                TaramaGeçmiş?.Add(Metin);
                Metin = string.Empty;
                Çeviri = string.Empty;
            },
            parameter => !string.IsNullOrWhiteSpace(Metin));

        Değiştir = new RelayCommand<object>(
            parameter =>
            {
                string current = MevcutDil;
                string translated = ÇevrilenDil;
                ÇevrilenDil = current;
                MevcutDil = translated;
            },
            parameter => ÇevrilenDil != MevcutDil);

        Aktar = new RelayCommand<object>(parameter => Metin = parameter as string, parameter => parameter is string oldtext && !string.IsNullOrWhiteSpace(oldtext));

        Oku = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is not string metin)
                {
                    return;
                }
                switch (speechSynthesizer.State)
                {
                    case SynthesizerState.Speaking:
                        speechSynthesizer.Pause();
                        return;
                    case SynthesizerState.Paused:
                        speechSynthesizer.Resume();
                        return;
                    case SynthesizerState.Ready:
                        _ = speechSynthesizer.SpeakAsync(metin);
                        break;
                }
            },
            parameter => !string.IsNullOrEmpty(OkumaDili));
    }

    public static List<string> TtsDilleri { get; set; }

    public RelayCommand<object> Aktar { get; }

    public string Çeviri
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Çeviri));
            }
        }
    }

    public string ÇevrilenDil
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ÇevrilenDil));
                OnPropertyChanged(nameof(Metin));
            }
        }
    } = Settings.Default?.DestinationTranslateLanguage;

    public ICommand Değiştir { get; }

    public string Metin
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Metin));
            }
        }
    }

    public bool MetinBoxIsreadOnly
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(MetinBoxIsreadOnly));
            }
        }
    }

    public string MevcutDil
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(MevcutDil));
                OnPropertyChanged(nameof(Metin));
            }
        }
    } = Settings.Default?.CurrentTranslateLanguage;

    public ICommand Oku { get; }

    public string OkumaDili
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(OkumaDili));
            }
        }
    }

    public ICommand Sıfırla { get; }

    public ObservableCollection<string> TaramaGeçmiş
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(TaramaGeçmiş));
            }
        }
    } = [];

    private async void TranslateViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "Metin" && Metin is not null)
        {
            Çeviri = await Extensions.TranslateViewModel.DileÇevirAsync(Metin, MevcutDil, ÇevrilenDil);
        }
        if (e.PropertyName is "OkumaDili" && !string.IsNullOrEmpty(OkumaDili))
        {
            speechSynthesizer ??= new SpeechSynthesizer();
            speechSynthesizer.SelectVoice(OkumaDili);
        }
        if (e.PropertyName is "MevcutDil" or "ÇevrilenDil")
        {
            Settings.Default.CurrentTranslateLanguage = MevcutDil;
            Settings.Default.DestinationTranslateLanguage = ÇevrilenDil;
            Settings.Default.Save();
        }
    }
}