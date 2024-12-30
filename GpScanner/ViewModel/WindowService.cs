using System.Linq;
using System.Windows;

namespace GpScanner;

public class WindowService : IWindowService
{
    public Window GetActiveWindow() => Application.Current?.Windows?.OfType<Window>()?.SingleOrDefault(x => x.IsActive);

    public Window GetFirstWindow() => Application.Current?.Windows?.OfType<Window>()?.FirstOrDefault();

    public T GetFirstWindow<T>() where T : Window => Application.Current?.Windows?.OfType<T>()?.FirstOrDefault();

    public Window GetLastWindow() => Application.Current?.Windows?.OfType<Window>()?.LastOrDefault();
}
