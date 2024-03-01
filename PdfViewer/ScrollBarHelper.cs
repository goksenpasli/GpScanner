using Extensions;
using PdfiumViewer;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;

namespace PdfViewer
{
    public class ScrollBarHelper
    {
        public static readonly DependencyProperty PdfFilePathProperty = DependencyProperty.RegisterAttached("PdfFilePath", typeof(string), typeof(ScrollBarHelper), new PropertyMetadata(null));
        public static readonly DependencyProperty PdfToolTipProperty = DependencyProperty.RegisterAttached("PdfToolTip", typeof(bool), typeof(ScrollBarHelper), new PropertyMetadata(false, OnPdfToolTipChanged));
        public static readonly DependencyProperty ThumbSizeProperty = DependencyProperty.RegisterAttached("ThumbSize", typeof(int), typeof(ScrollBarHelper), new PropertyMetadata(210));
        private static readonly ToolTip tooltip = new();

        public static string GetPdfFilePath(DependencyObject obj) => (string)obj.GetValue(PdfFilePathProperty);

        public static bool GetPdfToolTip(DependencyObject obj) => (bool)obj.GetValue(PdfToolTipProperty);

        public static int GetThumbSize(DependencyObject obj) => (int)obj.GetValue(ThumbSizeProperty);

        public static void SetPdfFilePath(DependencyObject obj, string value) => obj.SetValue(PdfFilePathProperty, value);

        public static void SetPdfToolTip(DependencyObject obj, bool value) => obj.SetValue(PdfToolTipProperty, value);

        public static void SetThumbSize(DependencyObject obj, int value) => obj.SetValue(ThumbSizeProperty, value);

        private static async Task Generatethumb(Control control, int pagenumber)
        {
            if (tooltip.IsOpen)
            {
                return;
            }
            int thumbsize = GetThumbSize(control);
            using PdfDocument pdfDoc = PdfDocument.Load(GetPdfFilePath(control));
            using Bitmap bitmap = pdfDoc.Render(pagenumber, 12, 12, false) as Bitmap;
            BitmapImage bitmapImage = bitmap.ToBitmapImage(ImageFormat.Jpeg, thumbsize);
            bitmapImage.Freeze();
            tooltip.Content = new System.Windows.Controls.Image { Source = bitmapImage, Width = thumbsize, Height = thumbsize };
            control.ToolTip = tooltip;
            tooltip.IsOpen = true;
            await Task.Delay(125);
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
                                                await Generatethumb(scrollBar, (int)args.NewValue - 1);
                                            }
                                        };
                }

                if (d is ListBox listBox)
                {
                    ScrollViewer scrollViewer = listBox.GetFirstVisualChild<ScrollViewer>();
                    if (scrollViewer != null)
                    {
                        scrollViewer.ScrollChanged += async (sender, e) =>
                                                      {
                                                          if (scrollViewer.ScrollableHeight > 0)
                                                          {
                                                              double index = scrollViewer.VerticalOffset / scrollViewer.ExtentHeight * listBox.Items.Count;
                                                              await Generatethumb(listBox, (int)index);
                                                          }
                                                      };
                    }
                }
            }
        }
    }
}
