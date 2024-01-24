using Extensions;
using GpScanner.Properties;
using System;
using System.Collections.Generic;

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
        };
        private const string basePath = "pack://application:,,,/GpScanner;component/Resources/";
        private Uri flagUri;

        public SplashViewModel() { FlagUri = GetFlag(Settings.Default.DefaultLang); }

        public Uri FlagUri
        {
            get => flagUri;
            set
            {
                if (flagUri != value)
                {
                    flagUri = value;
                    OnPropertyChanged(nameof(FlagUri));
                }
            }
        }

        public static Uri GetFlag(string language)
        {
            if (LanguageFlags.TryGetValue(language.ToUpper(), out string flagPath))
            {
                string fullPath = $"{basePath}{flagPath}";
                return new Uri(fullPath);
            }
            return new Uri($"{basePath}{LanguageFlags["DEFAULT"]}");
        }
    }
}
