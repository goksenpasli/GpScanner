using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GpScanner.ViewModel;
using TwainControl;

namespace GpScanner {
    /// <summary>
    /// Interaction logic for DocumentViewerWindow.xaml
    /// </summary>
    public partial class DocumentViewerWindow : Window {
        public DocumentViewerWindow() {
            InitializeComponent();
            DataContext = new DocumentViewerModel();
        }

        private double height;

        private bool isMouseDown;

        private Point mousedowncoord;

        private double width;

        private void DocumentViewer_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.OriginalSource is Image img && img.Parent is ScrollViewer scrollviewer && e.LeftButton == MouseButtonState.Pressed && Keyboard.IsKeyDown(Key.LeftCtrl)) {
                isMouseDown = true;
                Cursor = Cursors.Cross;
                mousedowncoord = e.GetPosition(scrollviewer);
            }
        }

        private void DocumentViewer_MouseMove(object sender, MouseEventArgs e) {
            if (e.OriginalSource is Image img && img.Parent is ScrollViewer scrollviewer && isMouseDown && DataContext is DocumentViewerModel documentViewerModel) {
                Point mousemovecoord = e.GetPosition(scrollviewer);
                SolidColorBrush fill = new() {
                    Color = Color.FromArgb(80, 0, 255, 0)
                };
                fill.Freeze();
                SolidColorBrush stroke = new() {
                    Color = Color.FromArgb(80, 255, 0, 0)
                };
                stroke.Freeze();
                Rectangle selectionbox = new() {
                    Stroke = stroke,
                    Fill = fill,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection(new double[] { 4, 2 }),
                    Width = Math.Abs(mousemovecoord.X - mousedowncoord.X),
                    Height = Math.Abs(mousemovecoord.Y - mousedowncoord.Y)
                };
                cnv.Children.Clear();
                _ = cnv.Children.Add(selectionbox);
                if (mousedowncoord.X < mousemovecoord.X) {
                    Canvas.SetLeft(selectionbox, mousedowncoord.X);
                    selectionbox.Width = mousemovecoord.X - mousedowncoord.X;
                }
                else {
                    Canvas.SetLeft(selectionbox, mousemovecoord.X);
                    selectionbox.Width = mousedowncoord.X - mousemovecoord.X;
                }

                if (mousedowncoord.Y < mousemovecoord.Y) {
                    Canvas.SetTop(selectionbox, mousedowncoord.Y);
                    selectionbox.Height = mousemovecoord.Y - mousedowncoord.Y;
                }
                else {
                    Canvas.SetTop(selectionbox, mousemovecoord.Y);
                    selectionbox.Height = mousedowncoord.Y - mousemovecoord.Y;
                }
                if (e.LeftButton == MouseButtonState.Released) {
                    cnv.Children.Clear();
                    width = Math.Abs(mousemovecoord.X - mousedowncoord.X);
                    height = Math.Abs(mousemovecoord.Y - mousedowncoord.Y);

                    if (mousedowncoord.X < mousemovecoord.X && mousedowncoord.Y < mousemovecoord.Y) {
                        documentViewerModel.ImgData = BitmapMethods.CaptureScreen(mousedowncoord.X, mousedowncoord.Y, width, height, scrollviewer, BitmapFrame.Create((BitmapSource)img.Source));
                    }
                    if (mousedowncoord.X > mousemovecoord.X && mousedowncoord.Y > mousemovecoord.Y) {
                        documentViewerModel.ImgData = BitmapMethods.CaptureScreen(mousemovecoord.X, mousemovecoord.Y, width, height, scrollviewer, BitmapFrame.Create((BitmapSource)img.Source));
                    }
                    if (mousedowncoord.X < mousemovecoord.X && mousedowncoord.Y > mousemovecoord.Y) {
                        documentViewerModel.ImgData = BitmapMethods.CaptureScreen(mousedowncoord.X, mousemovecoord.Y, width, height, scrollviewer, BitmapFrame.Create((BitmapSource)img.Source));
                    }
                    if (mousedowncoord.X > mousemovecoord.X && mousedowncoord.Y < mousemovecoord.Y) {
                        documentViewerModel.ImgData = BitmapMethods.CaptureScreen(mousemovecoord.X, mousedowncoord.Y, width, height, scrollviewer, BitmapFrame.Create((BitmapSource)img.Source));
                    }

                    mousedowncoord.X = mousedowncoord.Y = 0;
                    isMouseDown = false;
                    Cursor = Cursors.Arrow;
                }
            }
        }
    }
}