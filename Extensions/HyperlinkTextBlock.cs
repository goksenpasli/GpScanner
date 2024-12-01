using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Extensions
{
    public static class HyperlinkTextBlock
    {
        public static readonly DependencyProperty AdressProperty = DependencyProperty.RegisterAttached("Adress", typeof(string), typeof(HyperlinkTextBlock), new PropertyMetadata("https://www.google.com/search?q=", OnTextWithLinksChanged));
        public static readonly DependencyProperty IsHyperlinkedProperty = DependencyProperty.RegisterAttached("IsHyperlinked", typeof(bool), typeof(HyperlinkTextBlock), new PropertyMetadata(false, OnTextWithLinksChanged));
        public static readonly DependencyProperty TextWithLinksProperty = DependencyProperty.RegisterAttached("TextWithLinks", typeof(string), typeof(HyperlinkTextBlock), new PropertyMetadata(string.Empty, OnTextWithLinksChanged));

        public static string GetAdress(DependencyObject obj) => (string)obj.GetValue(AdressProperty);

        public static bool GetIsHyperlinked(DependencyObject obj) => (bool)obj.GetValue(IsHyperlinkedProperty);

        public static string GetTextWithLinks(DependencyObject obj) => (string)obj.GetValue(TextWithLinksProperty);

        public static void SetAdress(DependencyObject obj, string value) => obj.SetValue(AdressProperty, value);

        public static void SetIsHyperlinked(DependencyObject obj, bool value) => obj.SetValue(IsHyperlinkedProperty, value);

        public static void SetTextWithLinks(DependencyObject obj, string value) => obj.SetValue(TextWithLinksProperty, value);

        private static void OnTextWithLinksChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock textBlock)
            {
                string text = GetTextWithLinks(textBlock);
                string adress = GetAdress(textBlock);
                bool isHyperlinked = GetIsHyperlinked(textBlock);

                textBlock.Inlines.Clear();

                if (isHyperlinked && !string.IsNullOrEmpty(text))
                {
                    foreach (string word in text.Split(' '))
                    {
                        Run run = new($"{word} ");
                        Hyperlink hyperlink = new(run) { NavigateUri = new Uri($"{adress}{word}"), TextDecorations = null, Foreground = textBlock.Foreground };
                        hyperlink.RequestNavigate += (sender, args) => Process.Start(new ProcessStartInfo(args.Uri.ToString()) { UseShellExecute = true });
                        textBlock.Inlines.Add(hyperlink);
                    }
                }
                else
                {
                    textBlock.Inlines.Add(new Run(text));
                }
            }
        }
    }
}
