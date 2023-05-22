using System.Windows;
using System.Windows.Input;

namespace TwainControl
{
    public class SimpleXmlViewer : XmlViewerControl
    {
        protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
        {
            xmlViewerControl ??= new XmlViewerControl();

            XmlViewerControlModel.SetXmlContent(xmlViewerControl, (string)Tag);

            if (maximizePdfWindow == null)
            {
                maximizePdfWindow = new Window
                {
                    WindowState = WindowState.Maximized,
                    ShowInTaskbar = true,
                    Title = Application.Current?.MainWindow?.Title,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                maximizePdfWindow.Closed += MaximizePdfWindow_Closed;
            }

            maximizePdfWindow.Content = xmlViewerControl;
            maximizePdfWindow.DataContext = Tag;
            _ = maximizePdfWindow.ShowDialog();

            base.OnMouseDoubleClick(e);
        }

        private Window maximizePdfWindow;

        private XmlViewerControl xmlViewerControl;

        private void MaximizePdfWindow_Closed(object sender, System.EventArgs e)
        {
            maximizePdfWindow.Closed -= MaximizePdfWindow_Closed;
            xmlViewerControl = null;
            maximizePdfWindow = null;
        }
    }
}