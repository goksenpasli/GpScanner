using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Extensions;

public class Resizer : Thumb
{
    public static DependencyProperty ThumbDirectionProperty = DependencyProperty.Register("ThumbDirection", typeof(ResizeDirections), typeof(Resizer));

    static Resizer() { DefaultStyleKeyProperty.OverrideMetadata(typeof(Resizer), new FrameworkPropertyMetadata(typeof(Resizer))); }
    public Resizer() { DragDelta += Resizer_DragDelta; }

    public ResizeDirections ThumbDirection { get => (ResizeDirections)GetValue(ThumbDirectionProperty); set => SetValue(ThumbDirectionProperty, value); }

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (DataContext is not ResizablePanel resizablePanel)
        {
            return;
        }
        if ((ThumbDirection == ResizeDirections.Left || ThumbDirection == ResizeDirections.Right) && resizablePanel.MinWidth > 0)
        {
            resizablePanel.Width = resizablePanel.MinWidth;
        }
        else if ((ThumbDirection == ResizeDirections.Top || ThumbDirection == ResizeDirections.Bottom) && resizablePanel.MinHeight > 0)
        {
            resizablePanel.Height = resizablePanel.MinHeight;
        }
    }

    private static double ResizeBottom(DragDeltaEventArgs e, Control designerItem)
    {
        double deltaVertical = Math.Min(-e.VerticalChange, designerItem.ActualHeight - designerItem.MinHeight);
        designerItem.Height = Math.Abs(designerItem.Height - deltaVertical);
        return deltaVertical;
    }

    private static double ResizeLeft(DragDeltaEventArgs e, Control designerItem)
    {
        double deltaHorizontal = Math.Min(e.HorizontalChange, designerItem.ActualWidth - designerItem.MinWidth);
        Canvas.SetLeft(designerItem, Canvas.GetLeft(designerItem) + deltaHorizontal);
        designerItem.Width = Math.Abs(designerItem.Width - deltaHorizontal);
        return deltaHorizontal;
    }

    private static double ResizeRight(DragDeltaEventArgs e, Control designerItem)
    {
        double deltaHorizontal = Math.Min(-e.HorizontalChange, designerItem.ActualWidth - designerItem.MinWidth);
        designerItem.Width = Math.Abs(designerItem.Width - deltaHorizontal);
        return deltaHorizontal;
    }

    private static double ResizeTop(DragDeltaEventArgs e, Control designerItem)
    {
        double deltaVertical = Math.Min(e.VerticalChange, designerItem.ActualHeight - designerItem.MinHeight);
        Canvas.SetTop(designerItem, Canvas.GetTop(designerItem) + deltaVertical);
        designerItem.Height = Math.Abs(designerItem.Height - deltaVertical);
        return deltaVertical;
    }

    private void Resizer_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (DataContext is Control designerItem)
        {
            switch (ThumbDirection)
            {
                case ResizeDirections.TopLeft:
                    _ = ResizeTop(e, designerItem);
                    _ = ResizeLeft(e, designerItem);
                    break;

                case ResizeDirections.Left:
                    _ = ResizeLeft(e, designerItem);
                    break;

                case ResizeDirections.BottomLeft:
                    _ = ResizeBottom(e, designerItem);
                    _ = ResizeLeft(e, designerItem);
                    break;

                case ResizeDirections.Bottom:
                    _ = ResizeBottom(e, designerItem);
                    break;

                case ResizeDirections.BottomRight:
                    _ = ResizeBottom(e, designerItem);
                    _ = ResizeRight(e, designerItem);
                    break;

                case ResizeDirections.Right:
                    _ = ResizeRight(e, designerItem);
                    break;

                case ResizeDirections.TopRight:
                    _ = ResizeTop(e, designerItem);
                    _ = ResizeRight(e, designerItem);
                    break;

                case ResizeDirections.Top:
                    _ = ResizeTop(e, designerItem);
                    break;
            }
        }

        e.Handled = true;
    }
}