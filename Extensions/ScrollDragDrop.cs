﻿using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Extensions
{
    public static class DragDropExtension
    {
        public static readonly DependencyProperty ScrollOnDragDropProperty = DependencyProperty.RegisterAttached("ScrollOnDragDrop", typeof(bool), typeof(DragDropExtension), new PropertyMetadata(false, HandleScrollOnDragDropChanged));

        public static T GetFirstVisualChild<T>(this DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child is not null and T)
                    {
                        return (T)child;
                    }

                    T childItem = GetFirstVisualChild<T>(child);
                    if (childItem != null)
                    {
                        return childItem;
                    }
                }
            }

            return null;
        }

        public static bool GetScrollOnDragDrop(DependencyObject element)
        {
            return element == null ? throw new ArgumentNullException(nameof(element)) : (bool)element.GetValue(ScrollOnDragDropProperty);
        }

        public static void SetScrollOnDragDrop(DependencyObject element, bool value)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(ScrollOnDragDropProperty, value);
        }

        private static void HandleScrollOnDragDropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            FrameworkElement container = d as FrameworkElement;

            if (d != null)
            {
                Unsubscribe(container);

                if (true.Equals(e.NewValue))
                {
                    Subscribe(container);
                }
            }
            else
            {
                Debug.Fail("Invalid type!");
            }
        }

        private static void OnContainerPreviewDragOver(object sender, DragEventArgs e)
        {
            if (sender is FrameworkElement container)
            {
                ScrollViewer scrollViewer = container.GetFirstVisualChild<ScrollViewer>();

                if (scrollViewer != null)
                {
                    const double tolerance = 60;
                    double verticalPos = e.GetPosition(container).Y;
                    const double offset = 20;

                    if (verticalPos < tolerance) // Top of visible list?
                    {
                        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - offset); //Scroll up.
                    }
                    else if (verticalPos > container.ActualHeight - tolerance) //Bottom of visible list?
                    {
                        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + offset); //Scroll down.
                    }
                }
            }
        }

        private static void Subscribe(FrameworkElement container)
        {
            container.PreviewDragOver += OnContainerPreviewDragOver;
        }

        private static void Unsubscribe(FrameworkElement container)
        {
            container.PreviewDragOver -= OnContainerPreviewDragOver;
        }
    }
}