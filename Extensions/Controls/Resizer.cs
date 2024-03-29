﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Extensions;

public class Resizer : Thumb
{
    public static DependencyProperty ThumbDirectionProperty =
                    DependencyProperty.Register("ThumbDirection", typeof(ResizeDirections), typeof(Resizer));

    static Resizer() { DefaultStyleKeyProperty.OverrideMetadata(typeof(Resizer), new FrameworkPropertyMetadata(typeof(Resizer))); }
    public Resizer() { DragDelta += Resizer_DragDelta; }

    public ResizeDirections ThumbDirection { get => (ResizeDirections)GetValue(ThumbDirectionProperty); set => SetValue(ThumbDirectionProperty, value); }

    private static double ResizeBottom(DragDeltaEventArgs e, Control designerItem)
    {
        double deltaVertical = Math.Min(-e.VerticalChange, designerItem.ActualHeight - designerItem.MinHeight);
        designerItem.Height -= deltaVertical;
        return deltaVertical;
    }

    private static double ResizeLeft(DragDeltaEventArgs e, Control designerItem)
    {
        double deltaHorizontal = Math.Min(e.HorizontalChange, designerItem.ActualWidth - designerItem.MinWidth);
        Canvas.SetLeft(designerItem, Canvas.GetLeft(designerItem) + deltaHorizontal);
        designerItem.Width -= deltaHorizontal;
        return deltaHorizontal;
    }

    private static double ResizeRight(DragDeltaEventArgs e, Control designerItem)
    {
        double deltaHorizontal = Math.Min(-e.HorizontalChange, designerItem.ActualWidth - designerItem.MinWidth);
        designerItem.Width -= deltaHorizontal;
        return deltaHorizontal;
    }

    private static double ResizeTop(DragDeltaEventArgs e, Control designerItem)
    {
        double deltaVertical = Math.Min(e.VerticalChange, designerItem.ActualHeight - designerItem.MinHeight);
        Canvas.SetTop(designerItem, Canvas.GetTop(designerItem) + deltaVertical);
        designerItem.Height -= deltaVertical;
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