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
                if (cnv.Children.Contains(selectionbox))
                {
                    cnv.Children.Remove(selectionbox);
                }
                _ = cnv.Children.Add(selectionbox);
                if (mousedowncoord.X < mousemovecoord.X)
                {
                    Canvas.SetLeft(selectionbox, mousedowncoord.X);
                    selectionbox.Width = mousemovecoord.X - mousedowncoord.X;
                }
                else
                {
                    Canvas.SetLeft(selectionbox, mousemovecoord.X);
                    selectionbox.Width = mousedowncoord.X - mousemovecoord.X;
                }

                if (mousedowncoord.Y < mousemovecoord.Y)
                {
                    Canvas.SetTop(selectionbox, mousedowncoord.Y);
                    selectionbox.Height = mousemovecoord.Y - mousedowncoord.Y;
                }
                else
                {
                    Canvas.SetTop(selectionbox, mousemovecoord.Y);
                    selectionbox.Height = mousedowncoord.Y - mousemovecoord.Y;
                }
                if (e.LeftButton == MouseButtonState.Released)
                {
                    cnv.Children.Remove(selectionbox);
                    width = Math.Abs(mousemovecoord.X - mousedowncoord.X);
                    height = Math.Abs(mousemovecoord.Y - mousedowncoord.Y);

                    if (mousedowncoord.X < mousemovecoord.X && mousedowncoord.Y < mousemovecoord.Y)
                    {
                        twainctrl.ImgData = BitmapMethods.CaptureScreen(mousedowncoord.X, mousedowncoord.Y, width, height, scrollviewer, BitmapFrame.Create((BitmapSource)img.Source));
                    }
                    if (mousedowncoord.X > mousemovecoord.X && mousedowncoord.Y > mousemovecoord.Y)
                    {
                        twainctrl.ImgData = BitmapMethods.CaptureScreen(mousemovecoord.X, mousemovecoord.Y, width, height, scrollviewer, BitmapFrame.Create((BitmapSource)img.Source));
                    }
                    if (mousedowncoord.X < mousemovecoord.X && mousedowncoord.Y > mousemovecoord.Y)
                    {
                        twainctrl.ImgData = BitmapMethods.CaptureScreen(mousedowncoord.X, mousemovecoord.Y, width, height, scrollviewer, BitmapFrame.Create((BitmapSource)img.Source));
                    }
                    if (mousedowncoord.X > mousemovecoord.X && mousedowncoord.Y < mousemovecoord.Y)
                    {
                        twainctrl.ImgData = BitmapMethods.CaptureScreen(mousemovecoord.X, mousedowncoord.Y, width, height, scrollviewer, BitmapFrame.Create((BitmapSource)img.Source));
                    }

                    mousedowncoord.X = mousedowncoord.Y = 0;
                    isMouseDown = false;
                    Cursor = Cursors.Arrow;
                }
            }
        }
    }
}