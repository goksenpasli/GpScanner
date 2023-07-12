﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Extensions
{
    public class ArchiveViewer : Control, INotifyPropertyChanged, IDisposable
    {
        public static readonly DependencyProperty ArchivePathProperty = DependencyProperty.Register("ArchivePath", typeof(string), typeof(ArchiveViewer), new PropertyMetadata(null, Changed));
        private ObservableCollection<ArchiveData> arşivİçerik;
        private bool disposedValue;
        private string[] selectedFiles;
        private double toplamOran;

        static ArchiveViewer() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ArchiveViewer), new FrameworkPropertyMetadata(typeof(ArchiveViewer))); }

        public ArchiveViewer()
        {
            ArşivTekDosyaÇıkar = new RelayCommand<object>(
                parameter =>
                {
                    try
                    {
                        using ZipArchive archive = ZipFile.Open(ArchivePath, ZipArchiveMode.Read);
                        ZipArchiveEntry dosya = archive.GetEntry(parameter as string);
                        string extractpath = $"{Path.GetTempPath()}{Guid.NewGuid()}{Path.GetExtension(dosya.Name)}";
                        dosya?.ExtractToFile(extractpath, true);
                        _ = Process.Start(extractpath);
                    } catch(Exception ex)
                    {
                        throw new ArgumentException(ArchivePath, ex);
                    }
                },
                parameter => !string.IsNullOrWhiteSpace(ArchivePath));

            ArşivDosyaEkle = new RelayCommand<object>(
                parameter =>
                {
                    try
                    {
                        AddFilesToZip(ArchivePath, SelectedFiles);
                    } catch(Exception ex)
                    {
                        throw new ArgumentException(ArchivePath, ex);
                    }
                },
                parameter => !string.IsNullOrWhiteSpace(ArchivePath));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string ArchivePath { get => (string)GetValue(ArchivePathProperty); set => SetValue(ArchivePathProperty, value); }

        public RelayCommand<object> ArşivDosyaEkle { get; }

        public ObservableCollection<ArchiveData> Arşivİçerik
        {
            get => arşivİçerik;

            set
            {
                if(arşivİçerik != value)
                {
                    arşivİçerik = value;
                    OnPropertyChanged(nameof(Arşivİçerik));
                }
            }
        }

        public RelayCommand<object> ArşivTekDosyaÇıkar { get; }

        public string[] SelectedFiles
        {
            get => selectedFiles;
            set
            {
                if(selectedFiles != value)
                {
                    selectedFiles = value;
                    OnPropertyChanged(nameof(SelectedFiles));
                }
            }
        }

        public double ToplamOran
        {
            get => toplamOran;

            set
            {
                toplamOran = value;
                OnPropertyChanged(nameof(ToplamOran));
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public  void ReadArchiveContent(string ArchiveFilePath, ArchiveViewer archiveViewer)
        {
            archiveViewer.Arşivİçerik = new ObservableCollection<ArchiveData>();
            using(ZipArchive archive = ZipFile.Open(ArchiveFilePath, ZipArchiveMode.Read))
            {
                foreach(ZipArchiveEntry item in archive.Entries.Where(z => z.Length > 0))
                {
                    ArchiveData archiveData = new()
                    {
                        SıkıştırılmışBoyut = item.CompressedLength,
                        DosyaAdı = item.Name,
                        TamYol = item.FullName,
                        Boyut = item.Length,
                        Oran = (double)item.CompressedLength / item.Length,
                        DüzenlenmeZamanı = item.LastWriteTime.Date
                    };
                    archiveViewer.Arşivİçerik.Add(archiveData);
                }
            }

            archiveViewer.ToplamOran = (double)archiveViewer.Arşivİçerik.Sum(z => z.SıkıştırılmışBoyut) / archiveViewer.Arşivİçerik.Sum(z => z.Boyut) * 100;
        }

        protected virtual void Dispose(bool disposing)
        {
            if(!disposedValue)
            {
                if(disposing)
                {
                }
                disposedValue = true;
            }
        }

        protected virtual void OnPropertyChanged(string propertyName) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

        private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if(d is ArchiveViewer archiveViewer && e.NewValue is not null)
            {
                string ArchiveFilePath = (string)e.NewValue;
                archiveViewer.ReadArchiveContent(ArchiveFilePath, archiveViewer);
            }
        }

        private void AddFilesToZip(string zipPath, string[] files)
        {
            if(files?.Length == 0)
            {
                return;
            }

            using ZipArchive zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Update);
            foreach(string file in files)
            {
                FileInfo fileInfo = new(file);
                _ = zipArchive.CreateEntryFromFile(fileInfo.FullName, fileInfo.Name);
            }
        }
    }
}
