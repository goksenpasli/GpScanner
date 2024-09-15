using Extensions;
using System;
using System.ComponentModel;

namespace TwainControl
{
    public class AboutViewTranslateViewModel : InpcBase
    {
        private string çeviri;
        private string çevrilenDil;

        public AboutViewTranslateViewModel() { PropertyChanged += AboutViewTranslateViewModel_PropertyChanged; }

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
                }
            }
        }

        public string MevcutDil { get; set; } = "auto";

        private async void AboutViewTranslateViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName is "ÇevrilenDil")
                {
                    string licensetext = Translation.GetResStringValue("LICENSE").Replace("\r", string.Empty).Replace("\n", string.Empty);
                    Çeviri = await TranslateViewModel.DileÇevirAsync(licensetext, "auto", ÇevrilenDil);
                }
            }
            catch (Exception)
            {
                Çeviri = string.Empty;
            }
        }
    }
}
