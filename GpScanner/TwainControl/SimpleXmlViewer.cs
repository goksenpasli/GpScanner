using System.Windows;
using System.Windows.Input;

namespace TwainControl;

public class SimpleXmlViewer : XmlViewerControl
{
    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        XmlViewerControl xmlViewerControl = new();
        XmlViewerControlModel.SetXmlContent(xmlViewerControl, (string)Tag);
        Window maximizePdfWindow = new()
        {
            Content = xmlViewerControl,
            WindowState = WindowState.Maximized,
            ShowInTaskbar = true,
            Title = "GPSCANNER",
            DataContext = Tag,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        _ = maximizePdfWindow.ShowDialog();
        maximizePdfWindow.Closed += (s, e) =>
        {
            xmlViewerControl = null;
            maximizePdfWindow = null;
        };
        base.OnMouseDoubleClick(e);
    }
}