using System.Windows;
using System.Windows.Input;
using Extensions;

namespace TwainControl
{
    public class SimpleImageViewer : ImageViewer
    {
        protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
        {
            ImageViewer imageViewer = new()
            {
                PanoramaButtonVisibility = Visibility.Collapsed,
                ImageFilePath = (string)Tag
            };
            Window maximizePdfWindow = new()
            {
                Content = imageViewer,
                WindowState = WindowState.Maximized,
                ShowInTaskbar = true,
                Title = "GPSCANNER",
                DataContext = Tag,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            _ = maximizePdfWindow.ShowDialog();
            maximizePdfWindow.Closed += (s, e) =>
            {
                imageViewer?.Dispose();
                maximizePdfWindow = null;
            };
            base.OnMouseDoubleClick(e);
        }
    }
}