using Extensions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GpScanner.ViewModel;

public class TranslateViewModel : InpcBase
{
    private static SpeechSynthesizer speechSynthesizer;

    private string çeviri;

    private string çevrilenDil = "en";

    private string metin;

    private bool metinBoxIsreadOnly;

    private string mevcutDil = "auto";

    private string okumaDili;

    private ObservableCollection<string> taramaGeçmiş = new();

    static TranslateViewModel()
    {
        speechSynthesizer = new SpeechSynthesizer();
        TtsDilleri = speechSynthesizer.GetInstalledVoices().Select(z => z.VoiceInfo.Name).ToList();
    }

    public TranslateViewModel()
    {
        PropertyChanged += TranslateViewModel_PropertyChanged;

        if(speechSynthesizer is not null)
        {
            OkumaDili = TtsDilleri?.FirstOrDefault();
        }

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
                if(parameter is string metin)
                {
                    speechSynthesizer?.SelectVoice(OkumaDili);
                    if(speechSynthesizer.State == SynthesizerState.Speaking)
                    {
                        speechSynthesizer.Pause();
                        return;
                    }

                    if(speechSynthesizer.State == SynthesizerState.Paused)
                    {
                        speechSynthesizer.Resume();
                        return;
                    }

                    if(speechSynthesizer.State == SynthesizerState.Ready)
                    {
                        _ = speechSynthesizer.SpeakAsync(metin);
                    }
                }
            },
            parameter => !string.IsNullOrEmpty(OkumaDili));
    }

    private void TranslateViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if(e.PropertyName is "OkumaDili" && !string.IsNullOrEmpty(OkumaDili))
        {
            speechSynthesizer ??= new SpeechSynthesizer();
        }
    }

    public string Çeviri
    {
        get => çeviri;

        set
        {
            if(çeviri != value)
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
            if(çevrilenDil != value)
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
        get
        {
            if(!string.IsNullOrEmpty(metin))
            {
                _ = Task.Run(async () => Çeviri = await Extensions.TranslateViewModel.DileÇevirAsync(metin, MevcutDil, ÇevrilenDil));
            }

            return metin;
        }

        set
        {
            if(metin != value)
            {
                metin = value;
                OnPropertyChanged(nameof(Metin));
                OnPropertyChanged(nameof(Çeviri));
            }
        }
    }

    public bool MetinBoxIsreadOnly
    {
        get => metinBoxIsreadOnly;

        set
        {
            if(metinBoxIsreadOnly != value)
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
            if(mevcutDil != value)
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
            if(okumaDili != value)
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
            if(taramaGeçmiş != value)
            {
                taramaGeçmiş = value;
                OnPropertyChanged(nameof(TaramaGeçmiş));
            }
        }
    }

    public static List<string> TtsDilleri { get; set; }
}