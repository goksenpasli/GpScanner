using System.Windows;

namespace GpScanner.ViewModel;

public class ThumbnailTranslateViewModel : TranslateViewModel
{
    public static readonly DependencyProperty AttachedTextProperty = DependencyProperty.RegisterAttached(
        "AttachedText",
        typeof(string),
        typeof(ThumbnailTranslateViewModel),
        new PropertyMetadata(null, AttachedTextChanged));

    public static string GetAttachedText(DependencyObject obj) { return (string)obj.GetValue(AttachedTextProperty); }
    public static void SetAttachedText(DependencyObject obj, string value) { obj.SetValue(AttachedTextProperty, value); }

    private static void AttachedTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TranslateView translateView && translateView.DataContext is TranslateViewModel translateViewModel)
        {
            translateViewModel.MetinBoxIsreadOnly = true;
            translateViewModel.Çeviri = string.Empty;
            translateViewModel.Metin = e.NewValue as string;
        }
    }
}