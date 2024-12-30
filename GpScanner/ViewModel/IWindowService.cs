using System.Windows;

namespace GpScanner;

public interface IWindowService
{
    Window GetActiveWindow();

    Window GetFirstWindow();

    T GetFirstWindow<T>() where T : Window;

    Window GetLastWindow();
}
