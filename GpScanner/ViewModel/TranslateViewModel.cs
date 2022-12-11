using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Extensions;

namespace GpScanner.ViewModel
{
    public class TranslateViewModel : InpcBase
    {
        public static readonly DependencyProperty AttachedTextProperty = DependencyProperty.RegisterAttached("AttachedText", typeof(string), typeof(TranslateViewModel), new PropertyMetadata(string.Empty, Changed));

        public TranslateViewModel()
        {
            TtsDilleri = synthesizer.GetInstalledVoices().Select(z => z.VoiceInfo.Name);
            OkumaDili = TtsDilleri?.FirstOrDefault();

            Sıfırla = new RelayCommand<object>(parameter =>
            {
                Metin = "";
                Çeviri = "";
            }, parameter => true);

            Oku = new RelayCommand<object>(parameter =>
            {
                if (parameter is string metin)
                {
                    synthesizer.SelectVoice(OkumaDili);
                    if (synthesizer.State == SynthesizerState.Speaking)
                    {
                        synthesizer.Pause();
                        return;
                    }
                    if (synthesizer.State == SynthesizerState.Paused)
                    {
                        synthesizer.Resume();
                        return;
                    }
                    if (synthesizer.State == SynthesizerState.Ready)
                    {
                        _ = synthesizer.SpeakAsync(metin);
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

        public bool MetinBoxEnabled
        {
            get => metinBoxEnabled;

            set
            {
                if (metinBoxEnabled != value)
                {
                    metinBoxEnabled = value;
                    OnPropertyChanged(nameof(MetinBoxEnabled));
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

        public IEnumerable<string> TtsDilleri { get; }

        public static string GetAttachedText(DependencyObject obj)
        {
            return (string)obj.GetValue(AttachedTextProperty);
        }

        public static void SetAttachedText(DependencyObject obj, string value)
        {
            obj.SetValue(AttachedTextProperty, value);
        }

        private readonly SpeechSynthesizer synthesizer = new() { Volume = 100 };

        private string çeviri;

        private string çevrilenDil = "en";

        private string metin;

        private bool metinBoxEnabled = true;

        private string mevcutDil = "auto";

        private string okumaDili;

        private ObservableCollection<string> taramaGeçmiş = new();

        private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TranslateView translateView && translateView.DataContext is TranslateViewModel translateViewModel)
            {
                translateViewModel.MetinBoxEnabled = false;
                translateViewModel.Metin = e.NewValue as string;
            }
        }
    }
}