using System.Windows.Controls;

namespace TwainControl
{
    /// <summary>
    /// Interaction logic for XmlViewerControl.xaml
    /// </summary>
    public partial class XmlViewerControl : UserControl
    {
        public XmlViewerControl()
        {
            InitializeComponent();
            DataContext = new XmlViewerControlModel();
        }
    }
}