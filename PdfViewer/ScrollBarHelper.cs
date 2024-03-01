using Extensions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using Viewer = PdfViewer.PdfViewer;

namespace PdfViewer
{
    public class ScrollBarHelper
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

            BitmapImage bitmapImage = await Viewer.ConvertToImgAsync(GetPdfFilePath(control), pagenumber + 1);
            bitmapImage.Freeze();
            tooltip.Content = new Image { Source = bitmapImage, Width = 210, Height = 210 };
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
