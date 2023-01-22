using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using System.Windows.Input;
using Extensions;

namespace GpScanner.ViewModel
{
    public class TranslateViewModel : InpcBase
    {
        public TranslateViewModel()
        {
            SpeechSynthesizer = new SpeechSynthesizer() { Volume = 100 };
            if (SpeechSynthesizer is not null)
            {
                TtsDilleri = SpeechSynthesizer.GetInstalledVoices().Select(z => z.VoiceInfo.Name);
                OkumaDili = TtsDilleri?.FirstOrDefault();
            }

            Sıfırla = new RelayCommand<object>(parameter =>
            {
                Metin = "";
                Çeviri = "";
            }, parameter => true);

            Oku = new RelayCommand<object>(parameter =>
            {
                if (parameter is string metin)
                {
                    SpeechSynthesizer.SelectVoice(OkumaDili);
                    if (SpeechSynthesizer.State == SynthesizerState.Speaking)
                    {
                        SpeechSynthesizer.Pause();
                        return;
                    }
                    if (SpeechSynthesizer.State == SynthesizerState.Paused)
                    {
                        SpeechSynthesizer.Resume();
                        return;
                    }
                    if (SpeechSynthesizer.State == SynthesizerState.Ready)
                    {
                        _ = SpeechSynthesizer.SpeakAsync(metin);
                    }
                }
            }, parameter => !string.IsNullOrEmpty(OkumaDili));
        }

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

        public string Metin
        {
            get
            {
                if (!string.IsNullOrEmpty(metin))
                {
                    _ = Task.Run(() => Çeviri = Extensions.TranslateViewModel.DileÇevir(metin, MevcutDil, ÇevrilenDil));
                }
                return metin;
            }

            set
            {
                if (metin != value)
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

        public SpeechSynthesizer SpeechSynthesizer
        {
            get => speechSynthesizer; set

            {
                if (speechSynthesizer != value)
                {
                    speechSynthesizer = value;
                    OnPropertyChanged(nameof(SpeechSynthesizer));
                }
            }
        }

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

        public IEnumerable<string> TtsDilleri { get; }

        private string çeviri;

        private string çevrilenDil = "en";

        private string metin;

        private bool metinBoxIsreadOnly;

        private string mevcutDil = "auto";

        private string okumaDili;

        private SpeechSynthesizer speechSynthesizer;

        private ObservableCollection<string> taramaGeçmiş = new();
    }
}