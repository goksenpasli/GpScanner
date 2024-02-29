using Extensions;
using PdfiumViewer;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;

namespace TwainControl
{
    public static class ScrollBarHelper
    {
        public static readonly DependencyProperty PdfFilePathProperty = DependencyProperty.RegisterAttached("PdfFilePath", typeof(string), typeof(ScrollBarHelper), new PropertyMetadata(null));
        public static readonly DependencyProperty PdfToolTipProperty = DependencyProperty.RegisterAttached("PdfToolTip", typeof(bool), typeof(ScrollBarHelper), new PropertyMetadata(false, OnPdfToolTipChanged));
        private static readonly ToolTip tooltip = new();

        public static string GetPdfFilePath(DependencyObject obj) => (string)obj.GetValue(PdfFilePathProperty);

        public static bool GetPdfToolTip(DependencyObject obj) => (bool)obj.GetValue(PdfToolTipProperty);

        public static void SetPdfFilePath(DependencyObject obj, string value) => obj.SetValue(PdfFilePathProperty, value);

        public static void SetPdfToolTip(DependencyObject obj, bool value) => obj.SetValue(PdfToolTipProperty, value);

        private static async Task Generatethumb(Control control, int pagenumber)
        {
            if (tooltip.IsOpen)
            {
                return;
            }
            PdfDocument pdfDoc = PdfDocument.Load(GetPdfFilePath(control));
            using Bitmap bitmap = pdfDoc.Render(pagenumber, 72, 72, false) as Bitmap;
            BitmapImage bitmapImage = bitmap.ToBitmapImage(ImageFormat.Jpeg, 210);
            bitmapImage.Freeze();
            tooltip.Content = new System.Windows.Controls.Image { Source = bitmapImage, Width = 210, Height = 210 };
            control.ToolTip = tooltip;
            tooltip.IsOpen = true;
            await Task.Delay(150);
            tooltip.IsOpen = false;
            tooltip.Content = null;
            control.ToolTip = null;
        }

        private static void OnPdfToolTipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool isEnabled && isEnabled)
            {
                if (d is ScrollBar scrollBar)
                {
                    scrollBar.Scroll += async (sender, args) =>
                                        {
                                            if (args.ScrollEventType == ScrollEventType.ThumbTrack)
                                            {
                                                await Generatethumb(scrollBar, (int)args.NewValue);
                                            }
                                        };
                }
            }
        }
    }
}
