using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Extensions;

public class SelectableTextBlock : TextBlock
{
    private TextPointer EndSelectPosition;
    private TextPointer StartSelectPosition;
    private TextRange textRange;

    public SelectableTextBlock() { Focusable = true; }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.C)
        {
            Clipboard.SetDataObject(textRange?.Text);
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Point mouseDownPoint = e.GetPosition(this);
        StartSelectPosition = GetPositionFromPoint(mouseDownPoint, true);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        Point mouseUpPoint = e.GetPosition(this);
        EndSelectPosition = GetPositionFromPoint(mouseUpPoint, true);
        if (EndSelectPosition is not null && StartSelectPosition is not null)
        {
            textRange = new(StartSelectPosition, EndSelectPosition);
            textRange?.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(SystemColors.HighlightTextColor));
            _ = Focus();
        }
    }
}