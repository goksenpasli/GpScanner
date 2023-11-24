using System.Windows;
using System.Windows.Controls;

namespace Extensions;

public class ResizablePanel : ContentControl
{
    static ResizablePanel() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ResizablePanel), new FrameworkPropertyMetadata(typeof(ResizablePanel))); }
}
