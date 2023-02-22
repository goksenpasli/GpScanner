using System.Windows.Controls;
using GpScanner.ViewModel;

namespace GpScanner {
    /// <summary>
    /// Interaction logic for TesseractView.xaml
    /// </summary>
    public partial class TesseractView : UserControl {
        public TesseractView() {
            InitializeComponent();
            DataContext = new TesseractViewModel();
        }
    }
}