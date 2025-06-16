using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace Extensions
{
    public class RegexTextBox : TextBox
    {
        public static readonly DependencyProperty MaskProperty = DependencyProperty.Register(nameof(Mask), typeof(string), typeof(RegexTextBox), new PropertyMetadata(string.Empty, OnMaskChanged));
        private static readonly ConcurrentDictionary<string, Regex> _regexCache = new();
        private static readonly DependencyPropertyKey IsValidPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsValid), typeof(bool), typeof(RegexTextBox), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.None));
        public static readonly DependencyProperty IsValidProperty = IsValidPropertyKey.DependencyProperty;

        static RegexTextBox() { DefaultStyleKeyProperty.OverrideMetadata(typeof(RegexTextBox), new FrameworkPropertyMetadata(typeof(RegexTextBox))); }
        public RegexTextBox() { TextChanged += (s, e) => Validate(); }

        public bool IsValid => (bool)GetValue(IsValidProperty);

        public string Mask { get => (string)GetValue(MaskProperty); set => SetValue(MaskProperty, value); }

        private static void OnMaskChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RegexTextBox txt)
            {
                txt.Validate();
            }
        }

        private void Validate()
        {
            if (DesignerProperties.GetIsInDesignMode(this) || string.IsNullOrWhiteSpace(Mask))
            {
                SetValue(IsValidPropertyKey, true);
                return;
            }

            Regex regex = _regexCache.GetOrAdd(Mask, pattern => new Regex(pattern, RegexOptions.Compiled));
            bool match = regex.IsMatch(Text ?? string.Empty);
            SetValue(IsValidPropertyKey, match);
        }
    }
}
