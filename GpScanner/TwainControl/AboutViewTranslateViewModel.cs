using Extensions;

namespace TwainControl
{
    public class AboutViewTranslateViewModel : InpcBase
    {
        private string çevrilenDil;
        private string çeviri;

        public AboutViewTranslateViewModel()
        {
            PropertyChanged += AboutViewTranslateViewModel_PropertyChanged;
        }

        private async void AboutViewTranslateViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "ÇevrilenDil")
            {
                string licensetext = Translation.GetResStringValue("LICENSE").Replace("\r", string.Empty).Replace("\n", string.Empty);
                Çeviri = await TranslateViewModel.DileÇevirAsync(licensetext, "auto", ÇevrilenDil);
            }
        }

        public string MevcutDil { get; set; } = "auto";

        public string ÇevrilenDil {
            get => çevrilenDil;
            set {
                if (çevrilenDil != value)
                {
                    çevrilenDil = value;
                    OnPropertyChanged(nameof(ÇevrilenDil));
                }
            }
        }

        public string Çeviri {
            get => çeviri;
            set {
                if (çeviri != value)
                {
                    çeviri = value;
                    OnPropertyChanged(nameof(Çeviri));
                }
            }
        }
    }
}
