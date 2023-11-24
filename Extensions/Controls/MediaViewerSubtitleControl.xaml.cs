using System.Windows.Controls;
using System.Windows.Data;

namespace Extensions.Controls;

/// <summary>
/// Interaction logic for MediaViewerSubtitleControl.xaml
/// </summary>
public partial class MediaViewerSubtitleControl : UserControl
{
    public CollectionViewSource cvs;

    public MediaViewerSubtitleControl()
    {
        InitializeComponent();
        cvs = TryFindResource("Subtitle") as CollectionViewSource;
    }
}