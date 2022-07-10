using System.Windows;
using System.Windows.Controls;

namespace Extensions
{
    public class HorizontalListBox : ListBox
    {
        static HorizontalListBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(HorizontalListBox), new FrameworkPropertyMetadata(typeof(HorizontalListBox)));
        }
    }
}