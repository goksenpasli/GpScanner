﻿using System.Collections.Generic;
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
            TtsDilleri = synthesizer.GetInstalledVoices().Select(z => z.VoiceInfo.Name);
            OkumaDili = TtsDilleri?.FirstOrDefault();

            Sıfırla = new RelayCommand<object>(parameter =>
            {
                Metin = "";
                Çeviri = "";
            }, parameter => true);

            Oku = new RelayCommand<object>(parameter =>
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
                    _ = synthesizer.SpeakAsync(Çeviri);
                    return;
                }
            }, parameter => !string.IsNullOrEmpty(Çeviri) && !string.IsNullOrEmpty(OkumaDili));
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

        public IEnumerable<string> TtsDilleri { get; }

        private readonly SpeechSynthesizer synthesizer = new() { Volume = 100 };

        private string çeviri;

        private string çevrilenDil = "en";

        private string metin;

        private string mevcutDil = "auto";

        private string okumaDili;
    }
}