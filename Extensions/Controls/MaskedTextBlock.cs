using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Extensions;

public class MaskedTextBlock : TextBlock
{
    public MaskedTextBlock()
    {
        Loaded += MaskedTextBlock_Loaded;
    }

    public string Mask {
        get => (string)GetValue(MaskProperty);
        set => SetValue(MaskProperty, value);
    }

    public char PromptChar {
        get => (char)GetValue(PromptCharProperty);
        set => SetValue(PromptCharProperty, value);
    }

    public string UnmaskedText {
        get => (string)GetValue(UnmaskedTextProperty);
        set => SetValue(UnmaskedTextProperty, value);
    }

    public static readonly DependencyProperty MaskProperty =
                        DependencyProperty.Register("Mask", typeof(string), typeof(MaskedTextBlock), null);

    public static readonly DependencyProperty PromptCharProperty =
        DependencyProperty.Register("PromptChar", typeof(char), typeof(MaskedTextBlock), new PropertyMetadata('_'));

    public static readonly DependencyProperty UnmaskedTextProperty = DependencyProperty.Register(
        "UnmaskedText",
        typeof(string),
        typeof(MaskedTextBlock),
        new UIPropertyMetadata(string.Empty, Changed));

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        MaskedTextBlock maskedTextBlock = d as MaskedTextBlock;
        maskedTextBlock._provider = new MaskedTextProvider(maskedTextBlock.Mask, CultureInfo.CurrentCulture);
        _ = maskedTextBlock._provider.Set(string.IsNullOrWhiteSpace(maskedTextBlock.UnmaskedText)
            ? string.Empty
            : e.NewValue as string);
        maskedTextBlock.Text = maskedTextBlock._provider.ToDisplayString();
    }

    private void MaskedTextBlock_Loaded(object sender, RoutedEventArgs e)
    {
        _provider = new MaskedTextProvider(Mask, CultureInfo.CurrentCulture);
        _ = _provider.Set(string.IsNullOrWhiteSpace(UnmaskedText) ? string.Empty : UnmaskedText);
        _provider.PromptChar = PromptChar;
        Text = _provider.ToDisplayString();
    }

    private MaskedTextProvider _provider;
}