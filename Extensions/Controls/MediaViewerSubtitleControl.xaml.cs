﻿using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Microsoft.Win32;

namespace Extensions.Controls
{
    /// <summary>
    /// Interaction logic for MediaViewerSubtitleControl.xaml
    /// </summary>
    public partial class MediaViewerSubtitleControl : UserControl, INotifyPropertyChanged
    {
        public CollectionViewSource cvs;

        public MediaViewerSubtitleControl()
        {
            InitializeComponent();
            cvs = TryFindResource("Subtitle") as CollectionViewSource;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void Subtitle_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new() { Multiselect = false, Filter = "Srt Dosyası (*.srt)|*.srt" };
            if (openFileDialog.ShowDialog() == true && DataContext is MediaViewer mediaviewer)
            {
                mediaviewer.SubtitleFilePath = openFileDialog.FileName;
                mediaviewer.ParsedSubtitle = mediaviewer.ParseSrtFile(mediaviewer.SubtitleFilePath);
            }
        }

        private void SubtitleMargin_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MediaViewer mediaviewer)
            {
                Thickness defaultsubtitlethickness = new(
                    mediaviewer.SubTitleMargin.Left,
                    mediaviewer.SubTitleMargin.Top,
                    mediaviewer.SubTitleMargin.Right,
                    mediaviewer.SubTitleMargin.Bottom);
                if (sender is RepeatButton repeatbutton)
                {
                    switch (repeatbutton.Content)
                    {
                        case "6":
                            defaultsubtitlethickness.Bottom -= 10;
                            break;

                        case "5":
                            defaultsubtitlethickness.Bottom += 10;
                            break;

                        case "4":
                            defaultsubtitlethickness.Left += 10;
                            break;

                        case "3":
                            defaultsubtitlethickness.Left -= 10;
                            break;

                        case "=":
                            defaultsubtitlethickness.Left = 0;
                            defaultsubtitlethickness.Bottom = 0;
                            break;
                    }

                    mediaviewer.SubTitleMargin = defaultsubtitlethickness;
                }
            }
        }
    }
}