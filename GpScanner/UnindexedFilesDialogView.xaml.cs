using System.Windows.Controls;
using System.Windows.Data;

namespace GpScanner
{
    /// <summary>
    /// Interaction logic for UnindexedFilesDialogView.xaml
    /// </summary>
    public partial class UnindexedFilesDialogView : UserControl
    {
        public static CollectionViewSource cvs;

        public UnindexedFilesDialogView()
        {
            InitializeComponent();
            cvs = TryFindResource("UnindexedFiles") as CollectionViewSource;
        }
    }
}
