using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace Extensions;

public class RegexTextBox : TextBox
{
    public static readonly DependencyPropertyKey IsValidProperty = DependencyProperty.RegisterReadOnly("IsValid", typeof(bool), typeof(RegexTextBox), new PropertyMetadata(false));
    public static readonly DependencyProperty MaskProperty = DependencyProperty.Register("Mask", typeof(string), typeof(RegexTextBox), new PropertyMetadata(string.Empty));
    private static readonly Dictionary<string, Regex> _compiledRegexCache = [];

    static RegexTextBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(RegexTextBox), new FrameworkPropertyMetadata(typeof(RegexTextBox)));
        TextProperty.OverrideMetadata(typeof(RegexTextBox), new FrameworkPropertyMetadata(null, Changed));
    }

    public bool IsValid => (bool)GetValue(IsValidProperty.DependencyProperty);

    public string Mask { get => (string)GetValue(MaskProperty); set => SetValue(MaskProperty, value); }

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RegexTextBox maskedTextBox && !DesignerProperties.GetIsInDesignMode(maskedTextBox) && !string.IsNullOrWhiteSpace(maskedTextBox.Mask))
        {
            maskedTextBox.SetValue(IsValidProperty, GetCompiledRegex(maskedTextBox.Mask).IsMatch(maskedTextBox.Text));
        }
    }

    private static Regex GetCompiledRegex(string pattern)
    {
        if (!_compiledRegexCache.TryGetValue(pattern, out Regex regex))
        {
            regex = new Regex(pattern, RegexOptions.Compiled);
            _compiledRegexCache[pattern] = regex;
        }
        return regex;
    }
}