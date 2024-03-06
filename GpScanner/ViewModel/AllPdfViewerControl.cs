using System.Windows;
using System.Windows.Controls;

namespace GpScanner.ViewModel;

public class AllPdfViewerControl : DependencyObject
{
    public static readonly DependencyProperty AllPageNumberProperty = DependencyProperty.RegisterAttached("AllPageNumber", typeof(int), typeof(AllPdfViewerControl), new PropertyMetadata(1, AllPageNumberChanged));
    public static readonly DependencyProperty HideAllPdfControlsProperty = DependencyProperty.RegisterAttached("HideAllPdfControls", typeof(Visibility), typeof(AllPdfViewerControl), new PropertyMetadata(Visibility.Visible, HideAllPdfControlsChanged));

    public static int GetAllPageNumber(DependencyObject obj) => (int)obj.GetValue(AllPageNumberProperty);

    public static Visibility GetHideAllPdfControls(DependencyObject obj) => (Visibility)obj.GetValue(HideAllPdfControlsProperty);

    public static void SetAllPageNumber(DependencyObject obj, int value) => obj.SetValue(AllPageNumberProperty, value);

    public static void SetHideAllPdfControls(DependencyObject obj, Visibility value) => obj.SetValue(HideAllPdfControlsProperty, value);

    private static void AllPageNumberChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PdfViewer.PdfViewer pdfviewer)
        {
            pdfviewer.Sayfa = (int)e.NewValue;
        }
    }

    private static void HideAllPdfControlsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PdfViewer.PdfViewer pdfviewer)
        {
            pdfviewer.ToolBarVisibility = (Visibility)e.NewValue;
        }
        if (d is WrapPanel wrapPanel)
        {
            wrapPanel.Visibility = (Visibility)e.NewValue;
        }
    }
}