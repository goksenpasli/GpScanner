﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using GpScanner.ViewModel;
using TwainControl;
using ZXing;

namespace GpScanner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static CollectionViewSource cvs;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new GpScannerViewModel();
            cvs = TryFindResource("Veriler") as CollectionViewSource;
            TwainCtrl.PropertyChanged += TwainCtrl_PropertyChanged;
        }

        private static void AddBarcodeToList(GpScannerViewModel ViewModel)
        {
            if (ViewModel.BarcodeContent is not null)
            {
                ViewModel.BarcodeList.Add(ViewModel.BarcodeContent);
            }
        }

        private void Calendar_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.Captured is CalendarItem)
            {
                _ = Mouse.Capture(null);
            }
        }

        private void GridSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is GpScannerViewModel ViewModel)
            {
                ViewModel.MainWindowDocumentGuiControlLength = new(1, GridUnitType.Star);
                ViewModel.MainWindowGuiControlLength = new(2, GridUnitType.Star);
            }
        }

        private void MW_ContentRendered(object sender, EventArgs e)
        {
            WindowExtensions.SystemMenu(this);
            if (StillImageHelper.ShouldScan)
            {
                TwainCtrl.ScanImage.Execute(null);
            }
        }

        private void TwainCtrl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (DataContext is GpScannerViewModel ViewModel)
            {
                if (e.PropertyName is "Resimler")
                {
                    ViewModel.Dosyalar = ViewModel.GetScannerFileData();
                    ViewModel.ChartData = ViewModel.GetChartsData();
                }

                if (e.PropertyName is "DetectPageSeperator" && ViewModel.DetectBarCode)
                {
                    Result result = ViewModel.GetImageBarcodeResult(TwainCtrl.Scanner.Resimler.LastOrDefault().Resim.BitmapSourceToBitmap());
                    ViewModel.BarcodeContent = result?.Text;
                    ViewModel.BarcodePosition = result?.ResultPoints;
                    AddBarcodeToList(ViewModel);

                    if (ViewModel.DetectPageSeperator && ViewModel.BarcodeContent is not null)
                    {
                        TwainCtrl.Scanner.FileName = ViewModel.GetPatchCodeResult(ViewModel.BarcodeContent);
                    }
                }

                if (e.PropertyName is "ImgData" && TwainCtrl.ImgData is not null)
                {
                    ViewModel.BarcodeContent = ViewModel.GetImageBarcodeResult(TwainCtrl.ImgData)?.Text;
                    AddBarcodeToList(ViewModel);
                    _ = ViewModel.Ocr(TwainCtrl.ImgData);
                    TwainCtrl.ImgData = null;
                }
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (TwainCtrl.filesavetask?.IsCompleted == false)
            {
                e.Cancel = true;
            }
        }
    }
}