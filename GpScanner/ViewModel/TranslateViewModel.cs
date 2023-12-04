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
    private string çeviri;
    private string çevrilenDil = Settings.Default?.DestinationTranslateLanguage;
    private string metin;
    private bool metinBoxIsreadOnly;
    private string mevcutDil = Settings.Default?.CurrentTranslateLanguage;
    private string okumaDili;
    private ObservableCollection<string> taramaGeçmiş = [];

    static TranslateViewModel()
    {
        speechSynthesizer = new SpeechSynthesizer();
        TtsDilleri = speechSynthesizer.GetInstalledVoices()?.Select(z => z.VoiceInfo.Name)?.ToList();
    }

    public TranslateViewModel()
    {
        PropertyChanged += TranslateViewModel_PropertyChanged;
        Sıfırla = new RelayCommand<object>(
            parameter =>
            {
                Metin = string.Empty;
                Çeviri = string.Empty;
            },
            parameter => true);

        Değiştir = new RelayCommand<object>(
            parameter =>
            {
                string current = MevcutDil;
                string translated = ÇevrilenDil;
                ÇevrilenDil = current;
                MevcutDil = translated;
            },
            parameter => ÇevrilenDil != MevcutDil);

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

    public string Çeviri
    {
        get => çeviri;

        set
        {
            if (çeviri != value)
            {
                çeviri = value;
                OnPropertyChanged(nameof(Çeviri));
            }
        }
    }

    public string ÇevrilenDil
    {
        get => çevrilenDil;

        set
        {
            if (çevrilenDil != value)
            {
                çevrilenDil = value;
                OnPropertyChanged(nameof(ÇevrilenDil));
                OnPropertyChanged(nameof(Metin));
            }
        }
    }

    public ICommand Değiştir { get; }

    public string Metin
    {
        get => metin;

        set
        {
            if (metin != value)
            {
                metin = value;
                OnPropertyChanged(nameof(Metin));
            }
        }
    }

    public bool MetinBoxIsreadOnly
    {
        get => metinBoxIsreadOnly;

        set
        {
            if (metinBoxIsreadOnly != value)
            {
                metinBoxIsreadOnly = value;
                OnPropertyChanged(nameof(MetinBoxIsreadOnly));
            }
        }
    }

    public string MevcutDil
    {
        get => mevcutDil;

        set
        {
            if (mevcutDil != value)
            {
                mevcutDil = value;
                OnPropertyChanged(nameof(MevcutDil));
                OnPropertyChanged(nameof(Metin));
            }
        }
    }

    public ICommand Oku { get; }

    public string OkumaDili
    {
        get => okumaDili;

        set
        {
            if (okumaDili != value)
            {
                okumaDili = value;
                OnPropertyChanged(nameof(OkumaDili));
            }
        }
    }

    public ICommand Sıfırla { get; }

    public ObservableCollection<string> TaramaGeçmiş
    {
        get => taramaGeçmiş;

        set
        {
            if (taramaGeçmiş != value)
            {
                taramaGeçmiş = value;
                OnPropertyChanged(nameof(TaramaGeçmiş));
            }
        }
    }

    private async void TranslateViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "Metin")
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