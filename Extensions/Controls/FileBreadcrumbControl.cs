using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Extensions
{
    public class FileBreadcrumbControl : Control
    {
        public static readonly DependencyProperty FilePathProperty =
            DependencyProperty.Register("FilePath", typeof(string), typeof(FileBreadcrumbControl), new PropertyMetadata(string.Empty, OnFilePathChanged));
        public static readonly DependencyProperty IsIndeterminateProperty =
            DependencyProperty.Register("IsIndeterminate", typeof(bool), typeof(FileBreadcrumbControl), new PropertyMetadata(false));
        public static readonly DependencyProperty PathSegmentsProperty =
            DependencyProperty.Register("PathSegments", typeof(ObservableCollection<Data>), typeof(FileBreadcrumbControl), new PropertyMetadata(new ObservableCollection<Data>()));
        public static readonly DependencyProperty ProgressValueProperty = DependencyProperty.Register("ProgressValue", typeof(double), typeof(FileBreadcrumbControl), new PropertyMetadata(0d));
        public static readonly DependencyProperty ShowFileNameProperty =
            DependencyProperty.Register("ShowFileName", typeof(Visibility), typeof(FileBreadcrumbControl), new PropertyMetadata(Visibility.Collapsed, VisibilityChanged));

        static FileBreadcrumbControl() { DefaultStyleKeyProperty.OverrideMetadata(typeof(FileBreadcrumbControl), new FrameworkPropertyMetadata(typeof(FileBreadcrumbControl))); }

        public FileBreadcrumbControl()
        {
            Navigate = new RelayCommand<object>(
                parameter =>
                {
                    try
                    {
                        _ = Process.Start(parameter as string);
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException(ex.Message);
                    }
                },
                parameter => !string.IsNullOrWhiteSpace(parameter as string));
        }

        public string FileName { get; private set; }

        public string FilePath { get => (string)GetValue(FilePathProperty); set => SetValue(FilePathProperty, value); }

        public bool IsIndeterminate { get => (bool)GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }

        public RelayCommand<object> Navigate { get; }

        public ObservableCollection<Data> PathSegments { get => (ObservableCollection<Data>)GetValue(PathSegmentsProperty); set => SetValue(PathSegmentsProperty, value); }

        public double ProgressValue { get => (double)GetValue(ProgressValueProperty); set => SetValue(ProgressValueProperty, value); }

        public Visibility ShowFileName { get => (Visibility)GetValue(ShowFileNameProperty); set => SetValue(ShowFileNameProperty, value); }

        private static void OnFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FileBreadcrumbControl breadcrumbControl)
            {
                breadcrumbControl.UpdatePathSegments();
                breadcrumbControl.FileName = Path.GetFileName(breadcrumbControl.FilePath);
            }
        }

        private static void VisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FileBreadcrumbControl breadcrumbControl)
            {
                breadcrumbControl.FileName = e.NewValue is Visibility.Visible ? Path.GetFileName(breadcrumbControl.FilePath) : null;
            }
        }

        private List<string> SplitFolderPath(string path)
        {
            string[] folders = path.Split('\\');
            List<string> combinations = [];

            for (int i = 1; i < folders.Length; i++)
            {
                string folderPath = string.Join("\\", folders, 0, i);
                combinations.Add(folderPath);
            }

            return combinations;
        }

        private void UpdatePathSegments()
        {
            if (!string.IsNullOrWhiteSpace(FilePath))
            {
                PathSegments = [];
                foreach (string segment in SplitFolderPath(FilePath))
                {
                    if (string.IsNullOrWhiteSpace(Path.GetDirectoryName(segment)))
                    {
                        PathSegments.Add(new Data() { Path = segment[0].ToString(), FullPath = $"{segment}\\" });
                    }
                    else
                    {
                        PathSegments.Add(new Data() { Path = Path.GetFileName(segment), FullPath = segment });
                    }
                }
            }
        }

        public record Data
        {
            public string FullPath { get; set; }

            public string Path { get; set; }
        }
    }
}
