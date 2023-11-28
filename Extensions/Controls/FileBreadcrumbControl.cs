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
        public static readonly DependencyProperty PathSegmentsProperty =
            DependencyProperty.Register("PathSegments", typeof(ObservableCollection<Data>), typeof(FileBreadcrumbControl), new PropertyMetadata(new ObservableCollection<Data>()));

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

        public string FilePath { get => (string)GetValue(FilePathProperty); set => SetValue(FilePathProperty, value); }

        public RelayCommand<object> Navigate { get; }

        public ObservableCollection<Data> PathSegments { get => (ObservableCollection<Data>)GetValue(PathSegmentsProperty); set => SetValue(PathSegmentsProperty, value); }

        private static void OnFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            FileBreadcrumbControl control = (FileBreadcrumbControl)d;
            control?.UpdatePathSegments();
        }

        private List<string> SplitFolderPath(string path)
        {
            string[] folders = path.Split('\\');
            List<string> combinations = [];

            for (int i = 1; i < folders.Length; i++)
            {
                string[] temp = new string[i];
                Array.Copy(folders, temp, i);
                combinations.Add(string.Join("\\", temp));
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
