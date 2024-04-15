using System.Windows;
using System.Windows.Controls;

namespace Extensions.Controls;

/// <summary>
/// Interaction logic for TranslateView.xaml
/// </summary>
public partial class TranslateView : UserControl
{
    public static readonly DependencyProperty DestLangIsEnabledProperty = DependencyProperty.Register("DestLangIsEnabled", typeof(bool), typeof(TranslateView), new PropertyMetadata(true));
    public static readonly DependencyProperty SourceLangIsEnabledProperty = DependencyProperty.Register("SourceLangIsEnabled", typeof(bool), typeof(TranslateView), new PropertyMetadata(true));

    public bool DestLangIsEnabled {
        get => (bool)GetValue(DestLangIsEnabledProperty);
        set => SetValue(DestLangIsEnabledProperty, value);
    }

    public bool SourceLangIsEnabled {
        get => (bool)GetValue(SourceLangIsEnabledProperty);
        set => SetValue(SourceLangIsEnabledProperty, value);
    }

    public TranslateView() { InitializeComponent(); }
}