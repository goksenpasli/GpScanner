﻿using Extensions;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TwainControl;

public class SimpleImageViewer : ImageViewer
{
    private ImageViewer imageViewer;
    private Window maximizePdfWindow;

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Image image)
        {
            imageViewer ??= new ImageViewer { PanoramaButtonVisibility = Visibility.Collapsed, PrintButtonVisibility = Visibility.Visible };
            imageViewer.ImageFilePath = image.DataContext as string;
            imageViewer.DataContext = Tag;

            if (maximizePdfWindow == null)
            {
                maximizePdfWindow = new Window { WindowState = WindowState.Maximized, ShowInTaskbar = true, Title = Application.Current?.MainWindow?.Title, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                maximizePdfWindow.Closed += MaximizePdfWindow_Closed;
            }

            maximizePdfWindow.Content = imageViewer;
            maximizePdfWindow.DataContext = imageViewer.ImageFilePath;
            _ = maximizePdfWindow.ShowDialog();
            base.OnMouseDoubleClick(e);
        }
    }

    private void MaximizePdfWindow_Closed(object sender, EventArgs e)
    {
        maximizePdfWindow.Closed -= MaximizePdfWindow_Closed;
        maximizePdfWindow = null;
        imageViewer?.Dispose();
        imageViewer = null;
    }
}