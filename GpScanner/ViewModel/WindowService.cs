using System.Linq;
using System.Windows;

namespace GpScanner;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
/// 
public interface IWindowService
{
    Window GetActiveWindow();

    Window GetFirstWindow();
}

public class WindowService : IWindowService
{
    public Window GetActiveWindow() => Application.Current?.Windows?.OfType<Window>()?.SingleOrDefault(x => x.IsActive);

    public Window GetFirstWindow() => Application.Current?.Windows?.OfType<Window>()?.FirstOrDefault();
}
