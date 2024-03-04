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
    public static class ScrollBarHelper
    {
        public static readonly DependencyProperty DurationProperty = DependencyProperty.RegisterAttached("Duration", typeof(int), typeof(ScrollBarHelper), new PropertyMetadata(400));
        public static readonly DependencyProperty PdfFilePathProperty = DependencyProperty.RegisterAttached("PdfFilePath", typeof(string), typeof(ScrollBarHelper), new PropertyMetadata(null, OnPdfFilePathChanged));
        public static readonly DependencyProperty ThumbSizeProperty = DependencyProperty.RegisterAttached("ThumbSize", typeof(int), typeof(ScrollBarHelper), new PropertyMetadata(210));
        private static readonly ToolTip tooltip = new();

        public static async Task GenerateThumb(Control control, int pagenumber, string pdfpath = null)
        {
            if (tooltip.IsOpen)
            {
                return;
            }
            string file = pdfpath ?? GetPdfFilePath(control);
            if (!PdfViewer.IsValidPdfFile(file))
            {
                return;
            }
            int thumbsize = GetThumbSize(control);
            tooltip.HorizontalOffset = -thumbsize - 40;
            BitmapImage bitmapImage = await Task.Run(
                () =>
                {
                    using PdfDocument pdfDoc = PdfDocument.Load(file);
                    using Bitmap bitmap = pdfDoc.Render(pagenumber, 12, 12, false) as Bitmap;
                    return bitmap.ToBitmapImage(ImageFormat.Jpeg, thumbsize);
                });
            bitmapImage.Freeze();
            tooltip.Content = new System.Windows.Controls.Image { Source = bitmapImage, Width = thumbsize, Height = thumbsize };
            control.ToolTip = tooltip;
            tooltip.IsOpen = true;
            await Task.Delay(GetDuration(control));
            tooltip.IsOpen = false;
            tooltip.Content = null;
            control.ToolTip = null;
        }

        public static int GetDuration(DependencyObject obj) => (int)obj.GetValue(DurationProperty);

        public static string GetPdfFilePath(DependencyObject obj) => (string)obj.GetValue(PdfFilePathProperty);

        public static int GetThumbSize(DependencyObject obj) => (int)obj.GetValue(ThumbSizeProperty);

        public static void SetDuration(DependencyObject obj, int value) => obj.SetValue(DurationProperty, value);

        public static void SetPdfFilePath(DependencyObject obj, string value) => obj.SetValue(PdfFilePathProperty, value);

        public static void SetThumbSize(DependencyObject obj, int value) => obj.SetValue(ThumbSizeProperty, value);

        private static void OnPdfFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement frameworkElement && e.NewValue is string)
            {
                if (frameworkElement is ScrollBar scrollBar)
                {
                    scrollBar.Scroll += async (sender, args) =>
                                        {
                                            if (args.ScrollEventType == ScrollEventType.ThumbTrack)
                                            {
                                                await GenerateThumb(scrollBar, (int)args.NewValue - 1);
                                            }
                                        };
                }

                if (frameworkElement is ListBox listBox)
                {
                    ScrollViewer scrollViewer = listBox.GetFirstVisualChild<ScrollViewer>();
                    if (scrollViewer != null)
                    {
                        scrollViewer.ScrollChanged += async (sender, args) =>
                                                      {
                                                          if (scrollViewer.ScrollableHeight > 0)
                                                          {
                                                              double index = scrollViewer.VerticalOffset / scrollViewer.ExtentHeight * listBox.Items.Count;
                                                              await GenerateThumb(listBox, (int)index);
                                                          }
                                                      };
                    }
                }
            }
        }
    }
}
