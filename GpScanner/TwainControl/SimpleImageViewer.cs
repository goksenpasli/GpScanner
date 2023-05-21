using System.Windows;
using System.Windows.Input;
using Extensions;

namespace TwainControl;

public class SimpleImageViewer : ImageViewer
{
    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        using (ImageViewer imageViewer = new()
        {
            PanoramaButtonVisibility = Visibility.Collapsed,
            PrintButtonVisibility = Visibility.Visible,
            ImageFilePath = (string)Tag
        })
        {
            Window maximizePdfWindow = new()
            {
                Content = imageViewer,
                WindowState = WindowState.Maximized,
                ShowInTaskbar = true,
                Title = Application.Current?.MainWindow?.Title,
                DataContext = Tag,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            _ = maximizePdfWindow.ShowDialog();
            maximizePdfWindow.Closed += (s, e) => maximizePdfWindow = null;
        }

        base.OnMouseDoubleClick(e);
    }
}