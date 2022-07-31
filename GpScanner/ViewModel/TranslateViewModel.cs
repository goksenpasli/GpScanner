using Extensions;
using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Input;

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
                    _ = Task.Factory.StartNew(() => Çeviri = DileÇevir(metin, MevcutDil, ÇevrilenDil), CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
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

        private static bool BağlantıVarmı()
        {
            try
            {
                IPHostEntry i = Dns.GetHostEntry("www.google.com");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string DileÇevir(string text, string from = "auto", string to = "en")
        {
            try
            {
                if (BağlantıVarmı())
                {
                    WebClient wc = new();
                    wc.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0");
                    wc.Headers.Add(HttpRequestHeader.AcceptCharset, "UTF-8");
                    wc.Encoding = Encoding.UTF8;
                    string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={from}&tl={to}&dt=t&q={Uri.EscapeUriString(text)}";
                    string page = wc.DownloadString(url);
                    JavaScriptSerializer JSS = new();
                    object parsedObj = JSS.DeserializeObject(page);
                    string çeviri = "";
                    object[] data = parsedObj as object[];
                    object firstnode = data.FirstOrDefault();
                    for (int i = 0; i < (firstnode as object[]).Length; i++)
                    {
                        çeviri += ((data.FirstOrDefault() as object[])?[i] as object[])?.ElementAt(0).ToString();
                    }
                    return çeviri;
                }
                return string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}