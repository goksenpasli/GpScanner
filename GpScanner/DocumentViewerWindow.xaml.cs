using System;
using System.IO;
using System.Linq;
using System.Windows;
using GpScanner.ViewModel;

namespace GpScanner
{
    /// <summary>
    /// Interaction logic for DocumentViewerWindow.xaml
    /// </summary>
    public partial class DocumentViewerWindow : Window
    {
        public DocumentViewerWindow()
        {
            InitializeComponent();
            DataContext = new DocumentViewerModel();
        }
    }
}