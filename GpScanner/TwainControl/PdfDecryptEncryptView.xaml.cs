using System.Windows.Controls;

namespace TwainControl
{
    /// <summary>
    /// Interaction logic for PdfDecryptEncryptView.xaml
    /// </summary>
    public partial class PdfDecryptEncryptView : UserControl
    {
        public PdfDecryptEncryptView()
        {
            InitializeComponent();
            DataContext = new PdfSecurityViewModel();
        }
    }
}
