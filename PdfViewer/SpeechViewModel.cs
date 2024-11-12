using Extensions;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Speech.Synthesis;
using System.Windows.Input;

namespace PdfViewer
{
    public class SpeechViewModel : InpcBase
    {
        private SpeechSynthesizer speechSynthesizer;

        public SpeechViewModel(PdfViewer pdfViewer)
        {
            PropertyChanged += Speech_PropertyChanged;
            speechSynthesizer = new SpeechSynthesizer();
            TtsDilleri = speechSynthesizer?.GetInstalledVoices()?.Select(z => z.VoiceInfo?.Name)?.ToList();

            Oku = new RelayCommand<object>(
                parameter =>
                {
                    switch (speechSynthesizer.State)
                    {
                        case SynthesizerState.Speaking:
                            speechSynthesizer.Pause();
                            return;
                        case SynthesizerState.Paused:
                            speechSynthesizer.Resume();
                            return;
                        case SynthesizerState.Ready:
                            _ = SpeechAllPages ? speechSynthesizer.SpeakAsync(string.Concat(pdfViewer?.PdfAllPagesContent?.SelectMany(z => z.Values))) : speechSynthesizer.SpeakAsync(pdfViewer?.PdfTextContent);
                            break;
                    }
                },
                parameter => !string.IsNullOrEmpty(OkumaDili) && !string.IsNullOrWhiteSpace(pdfViewer?.PdfTextContent));

            Dur = new RelayCommand<object>(parameter => speechSynthesizer?.SpeakAsyncCancelAll(), parameter => !string.IsNullOrEmpty(OkumaDili));
        }

        public ICommand Dur { get; }

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

        public bool SpeechAllPages
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(SpeechAllPages));
                }
            }
        }

        public List<string> TtsDilleri { get; set; }

        private void Speech_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "OkumaDili" && !string.IsNullOrEmpty(OkumaDili))
            {
                speechSynthesizer ??= new SpeechSynthesizer();
                speechSynthesizer.SpeakAsyncCancelAll();
                speechSynthesizer.SelectVoice(OkumaDili);
            }
        }
    }
}
