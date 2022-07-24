using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace GpScanner.ViewModel
{
    public class ViewerTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Pdf { get; set; }

        public DataTemplate Zip { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is string dosya)
            {
                string ext = new FileInfo(dosya).Extension.ToLower();
                if (ext == ".pdf")
                {
                    return Pdf;
                }
                if (ext == ".zip")
                {
                    return Zip;
                }
            }
            return null;
        }
    }
}