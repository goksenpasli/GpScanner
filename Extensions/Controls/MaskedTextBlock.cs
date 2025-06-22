using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Extensions
{
    public class MaskedTextBlock : TextBlock
    {
        public static readonly DependencyProperty MaskProperty =
            DependencyProperty.Register(nameof(Mask), typeof(string), typeof(MaskedTextBlock), new PropertyMetadata(null, OnMaskOrTextChanged));
        public static readonly DependencyProperty PromptCharProperty =
            DependencyProperty.Register(nameof(PromptChar), typeof(char), typeof(MaskedTextBlock), new PropertyMetadata('_', OnMaskOrTextChanged));
        public static readonly DependencyProperty UnmaskedTextProperty =
            DependencyProperty.Register(nameof(UnmaskedText), typeof(string), typeof(MaskedTextBlock), new PropertyMetadata(string.Empty, OnMaskOrTextChanged));
        private MaskedTextProvider _provider;

        public MaskedTextBlock() { Loaded += MaskedTextBlock_Loaded; }

        public string Mask { get => (string)GetValue(MaskProperty); set => SetValue(MaskProperty, value); }

        public char PromptChar { get => (char)GetValue(PromptCharProperty); set => SetValue(PromptCharProperty, value); }

        public string UnmaskedText { get => (string)GetValue(UnmaskedTextProperty); set => SetValue(UnmaskedTextProperty, value); }

        private static void OnMaskOrTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not MaskedTextBlock maskedTextBlock)
            {
                return;
            }

            maskedTextBlock.UpdateText();
        }

        private void MaskedTextBlock_Loaded(object sender, RoutedEventArgs e) => UpdateText();

        private void UpdateText()
        {
            if (string.IsNullOrEmpty(Mask))
            {
                Text = UnmaskedText ?? string.Empty;
                return;
            }

            _provider ??= new MaskedTextProvider(Mask, CultureInfo.CurrentCulture);
            _provider.Clear();

            _provider.PromptChar = PromptChar;

            if (!string.IsNullOrEmpty(UnmaskedText))
            {
                _ = _provider.Set(UnmaskedText);
            }

            Text = _provider.ToDisplayString();
        }
    }
}
