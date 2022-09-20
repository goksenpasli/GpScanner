using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Extensions;

namespace GpScanner.ViewModel
{
    public class TranslateViewModel : InpcBase
    {
        public TranslateViewModel()
        {
            Sıfırla = new RelayCommand<object>(parameter =>
            {
                Metin = "";
                Çeviri = "";
            }, parameter => true);
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
                    _ = Task.Factory.StartNew(() => Çeviri = Extensions.TranslateViewModel.DileÇevir(metin, MevcutDil, ÇevrilenDil), CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
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

        public ICommand Sıfırla { get; }

        private string çeviri;

        private string çevrilenDil = "en";

        private string metin;

        private string mevcutDil = "auto";
    }
}