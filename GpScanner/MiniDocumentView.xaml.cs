using GpScanner.ViewModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using TwainControl;
using static Extensions.ExtensionMethods;
using static TwainControl.DrawControl;

namespace GpScanner
{
    /// <summary>
    /// Interaction logic for MiniDocumentView.xaml
    /// </summary>
    public partial class MiniDocumentView : UserControl
    {
        private GpScannerViewModel viewModel;

        public MiniDocumentView() { InitializeComponent(); }

        private async void ListBox_DropAsync(object sender, DragEventArgs e) => await viewModel.TwainCtrl.ListBoxDropFileAsync(e);

        private void MiniDocumentRun_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Run run)
            {
                try
                {
                    viewModel.TwainCtrl.DragMoveStarted = true;
                    StackPanel stackPanel = (run.Parent as TextBlock)?.Parent as StackPanel;
                    using Bitmap img = stackPanel.ToRenderTargetBitmap().BitmapSourceToBitmap();
                    using Icon icon = Icon.FromHandle(img.GetHicon());
                    TwainCtrl.DragCursor = CursorInteropHelper.Create(new SafeIconHandle(icon.Handle));
                    _ = DragDrop.DoDragDrop(run, run.DataContext, DragDropEffects.Move);
                    viewModel.TwainCtrl.DragMoveStarted = false;
                    e.Handled = true;
                }
                finally
                {
                    viewModel.TwainCtrl.DragMoveStarted = false;
                }
            }
        }

        private void StackPanel_Drop(object sender, DragEventArgs e) => viewModel.TwainCtrl.DropFile(sender, e);

        private void StackPanel_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            if (e.Effects == DragDropEffects.Move)
            {
                if (TwainCtrl.DragCursor is not null)
                {
                    e.UseDefaultCursors = false;
                    _ = Mouse.SetCursor(TwainCtrl.DragCursor);
                }
            }
            else
            {
                e.UseDefaultCursors = true;
            }
            e.Handled = true;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) => viewModel = DataContext as GpScannerViewModel;
    }
}
