using Extensions;
using GpScanner.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using TwainControl;

namespace GpScanner.ViewModel
{
    public class SplashViewModel : InpcBase
    {
        public static readonly Dictionary<string, string> LanguageFlags = new()
        {
            { "TÜRKÇE", "flag-of-Turkey.png" },
            { "ENGLISH", "flag-of-United-States-of-America.png" },
            { "FRANÇAIS", "flag-of-France.png" },
            { "ITALIANO", "flag-of-Italy.png" },
            { "عربي", "flag-of-Saudi-Arabia.png" },
            { "РУССКИЙ", "flag-of-Russia.png" },
            { "DEUTSCH", "flag-of-Germany.png" },
            { "日本", "flag-of-Japan.png" },
            { "DUTCH", "flag-of-Netherlands.png" },
            { "CZECH", "flag-of-Czech.png" },
            { "ESPAÑOL", "flag-of-Spain.png" },
            { "中國人", "flag-of-China.png" },
            { "УКРАЇНСЬКА", "flag-of-Ukraina.png" },
            { "ΕΛΛΗΝΙΚΑ", "flag-of-Greece.png" },
            { "فلسطين", "flag-of-Palestine.png" },
            { "AZƏRBAYCAN", "flag-of-Azərbaycan.png" },
            { "HRVATSKI", "flag-of-Croatian.png" },
            { "DANSK", "flag-of-Danish.png" },
            { "БЕЛАРУСКАЯ", "flag-of-Belarusian.png" },
            { "БЪЛГАРСКИ", "flag-of-Bulgarian.png" },
            { "भारतीय", "flag-of-India.png" },
            { "PORTUGUÊS", "flag-of-Portuguese.png" },
            { "INDONESIA", "flag-of-indonesia.png" },
            { "ՀԱՅԵՐԵՆ", "flag-of-armenia.png" },
            { "ROMÂNĂ", "flag-of-romania.png" },
            { "MAGYAR", "flag-of-hungary.png" },
            { "لبنان", "flag-of-Lebanon.png" },
            { "BELGIË", "flag-of-belgium.png" },
            { "SVENSKA", "flag-of-sweden.png" },
            { "SUOMI", "flag-of-finland.png" },
            { "MALAYSIAN", "Flag_of_Malaysia.png" },
            { "ایرانی", "flag_of_iran.png" },
            { "МАКЕДОНСКИ", "Flag_of_North_Macedonia.png" },
            { "ქართველი", "Flag-of-Georgia.png" },
            { "한국인", "flag-of-korea.png" },
            { "TÜRKMEN", "Flag_of_Turkmenistan.png" },
            { "UZBEK", "Flag_of_Uzbekistan.png" },
        };
        private const string basePath = "pack://application:,,,/GpScanner;component/Resources/";
        private DispatcherTimer flaganimationtimer;

        public SplashViewModel()
        {
            FlagUri = GetFlag(Settings.Default.DefaultLang);
            GenerateFlagAnimation();
            TranslationSource.Instance.CurrentCulture = ChangeApplicationLanguage(Settings.Default.DefaultLang);
            SplashText = Translation.GetResStringValue("SPLASHTEXT");
        }

        public int FlagProgress
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(FlagProgress));
                }
            }
        }

        public Uri FlagUri
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(FlagUri));
                }
            }
        }

        public string SplashText
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(SplashText));
                }
            }
        }

        public static CultureInfo ChangeApplicationLanguage(string lang)
        {
            return lang switch
            {
                "" or "TÜRKÇE" => new CultureInfo("tr-TR"),
                "ENGLISH" => new CultureInfo("en-US"),
                "FRANÇAIS" => new CultureInfo("fr-FR"),
                "ITALIANO" => new CultureInfo("it-IT"),
                "عربي" or "فلسطين" or "لبنان" => new CultureInfo("ar-AR"),
                "РУССКИЙ" => new CultureInfo("ru-RU"),
                "DEUTSCH" => new CultureInfo("de-DE"),
                "日本" => new CultureInfo("ja-JP"),
                "DUTCH" or "BELGIË" => new CultureInfo("nl-NL"),
                "CZECH" => new CultureInfo("cs-CZ"),
                "ESPAÑOL" => new CultureInfo("es-ES"),
                "中國人" => new CultureInfo("zh-CN"),
                "УКРАЇНСЬКА" => new CultureInfo("uk-UA"),
                "ΕΛΛΗΝΙΚΑ" => new CultureInfo("el"),
                "AZƏRBAYCAN" => new CultureInfo("az"),
                "БЕЛАРУСКАЯ" => new CultureInfo("be"),
                "БЪЛГАРСКИ" => new CultureInfo("bg"),
                "DANSK" => new CultureInfo("da"),
                "HRVATSKI" => new CultureInfo("hr"),
                "भारतीय" => new CultureInfo("gu"),
                "PORTUGUÊS" => new CultureInfo("pt"),
                "INDONESIA" => new CultureInfo("id"),
                "ՀԱՅԵՐԵՆ" => new CultureInfo("hy"),
                "ROMÂNĂ" => new CultureInfo("ro"),
                "MAGYAR" => new CultureInfo("hu"),
                "SVENSKA" => new CultureInfo("sv"),
                "SUOMI" => new CultureInfo("fi"),
                "MALAYSIAN" => new CultureInfo("ms"),
                "ایرانی" => new CultureInfo("fa"),
                "МАКЕДОНСКИ" => new CultureInfo("mk"),
                "ქართველი" => new CultureInfo("ka"),
                "한국인" => new CultureInfo("ko"),
                "UZBEK" => new CultureInfo("uz"),
                "TÜRKMEN" => new CultureInfo("tk"),
                _ => new CultureInfo("en-US")
            };
        }

        public static Uri GetFlag(string language)
        {
            if (LanguageFlags.TryGetValue(language.ToUpper(), out string flagPath))
            {
                string fullPath = $"{basePath}{flagPath}";
                return new Uri(fullPath);
            }
            return new Uri($"{basePath}{LanguageFlags["TÜRKÇE"]}");
        }

        private void GenerateFlagAnimation()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                return;
            }
            int direction = 1;
            flaganimationtimer = new(DispatcherPriority.SystemIdle) { Interval = TimeSpan.FromMilliseconds(25) };
            flaganimationtimer.Tick += (sender, e) =>
                                       {
                                           if (FlagProgress >= 85)
                                           {
                                               direction = -1;
                                           }
                                           if (FlagProgress <= 15)
                                           {
                                               direction = 1;
                                           }
                                           FlagProgress += direction;
                                       };
            flaganimationtimer.Start();
        }
    }
}
