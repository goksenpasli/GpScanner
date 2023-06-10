using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Extensions;
using GpScanner.ViewModel;
using TwainControl;

namespace GpScanner;

/// <summary>
/// Interaction logic for DocumentViewerWindow.xaml
/// </summary>
public partial class DocumentViewerWindow : Window
{
    public DocumentViewerWindow()
    {
        InitializeComponent();
        DataContext = new DocumentViewerModel();
    }

    private static readonly Rectangle selectionbox = new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)),
        Fill = new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)),
        StrokeThickness = 2,
        StrokeDashArray = new DoubleCollection(new double[] { 1 })
    };

    private double height;

    private bool isMouseDown;

    private Point mousedowncoord;

    private double width;

    private void DocumentViewer_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Image img && img.Parent is ScrollViewer scrollviewer && e.LeftButton == MouseButtonState.Pressed && Keyboard.IsKeyDown(Key.LeftCtrl))
        {
            isMouseDown = true;
            Cursor = Cursors.Cross;
            mousedowncoord = e.GetPosition(scrollviewer);
        }
    }

    private void DocumentViewer_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.OriginalSource is Image img && img.Parent is ScrollViewer scrollviewer && isMouseDown && DataContext is DocumentViewerModel documentViewerModel)
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
                documentViewerModel.ImgData = BitmapMethods.CaptureScreen(captureX, captureY, width, height, scrollviewer, BitmapFrame.Create((BitmapSource)img.Source));
                mousedowncoord.X = mousedowncoord.Y = 0;
                isMouseDown = false;
                Cursor = Cursors.Arrow;
            }
        }
    }

    private void Window_Unloaded(object sender, RoutedEventArgs e)
    {
        if (cnt.GetFirstVisualChild<PdfViewer.PdfViewer>() is PdfViewer.PdfViewer pdfvwr)
        {
            pdfvwr?.Dispose();
            GC.Collect();
        }
    }
}