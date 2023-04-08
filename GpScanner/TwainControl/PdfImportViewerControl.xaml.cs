using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TwainControl
{
    /// <summary>
    /// Interaction logic for PdfImportViewerControl.xaml
    /// </summary>
    public partial class PdfImportViewerControl : UserControl
    {
        public PdfImportViewerControl()
        {
            InitializeComponent();
        }

        private static readonly Rectangle selectionbox = new()
        {
            Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)),
            Fill = new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection(new double[] { 4, 2 }),
        };

        private double height;

        private bool isMouseDown;

        private Point mousedowncoord;

        private double width;

        private void PdfImportViewerControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Image img && img.Parent is ScrollViewer scrollviewer && e.LeftButton == MouseButtonState.Pressed && Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                isMouseDown = true;
                Cursor = Cursors.Cross;
                mousedowncoord = e.GetPosition(scrollviewer);
            }
        }

        private void PdfImportViewerControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.OriginalSource is Image img && img.Parent is ScrollViewer scrollviewer && isMouseDown && DataContext is TwainCtrl twainctrl)
            {
                Point mousemovecoord = e.GetPosition(scrollviewer);
                if (!cnv.Children.Contains(selectionbox))
                {
                    _ = cnv.Children.Add(selectionbox);
                }
                double x1 = Math.Min(mousedowncoord.X, mousemovecoord.X);
                double x2 = Math.Max(mousedowncoord.X, mousemovecoord.X);
                double y1 = Math.Min(mousedowncoord.Y, mousemovecoord.Y);
                double y2 = Math.Max(mousedowncoord.Y, mousemovecoord.Y);

                Canvas.SetLeft(selectionbox, x1);
                Canvas.SetTop(selectionbox, y1);
                selectionbox.Width = x2 - x1;
                selectionbox.Height = y2 - y1;

                if (e.LeftButton == MouseButtonState.Released)
                {
                    cnv.Children.Remove(selectionbox);
                    width = Math.Abs(mousemovecoord.X - mousedowncoord.X);
                    height = Math.Abs(mousemovecoord.Y - mousedowncoord.Y);
                    double captureX, captureY;
                    captureX = mousedowncoord.X < mousemovecoord.X ? mousedowncoord.X : mousemovecoord.X;
                    captureY = mousedowncoord.Y < mousemovecoord.Y ? mousedowncoord.Y : mousemovecoord.Y;
                    twainctrl.ImgData = BitmapMethods.CaptureScreen(captureX, captureY, width, height, scrollviewer, BitmapFrame.Create((BitmapSource)img.Source));
                    mousedowncoord.X = mousedowncoord.Y = 0;
                    isMouseDown = false;
                    Cursor = Cursors.Arrow;
                }
            }
        }
    }
}