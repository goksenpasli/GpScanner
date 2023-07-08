using System.Windows;

namespace GpScanner.ViewModel;

public class AllPdfViewerControl : DependencyObject
{
    public static readonly DependencyProperty AllPageNumberProperty =
                DependencyProperty.RegisterAttached("AllPageNumber", typeof(int), typeof(AllPdfViewerControl), new PropertyMetadata(1, AllPageNumberChanged));

    public static int GetAllPageNumber(DependencyObject obj) { return (int)obj.GetValue(AllPageNumberProperty); }
    public static void SetAllPageNumber(DependencyObject obj, int value) { obj.SetValue(AllPageNumberProperty, value); }

    private static void AllPageNumberChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if(d is PdfViewer.PdfViewer pdfviewer)
        {
            pdfviewer.Sayfa = (int)e.NewValue;
        }
    }
}