﻿using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace GpScanner.ViewModel
{
    public class ViewerTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Img { get; set; }

        public DataTemplate Pdf { get; set; }

        public DataTemplate Zip { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is string dosya)
            {
                string[] imgext = new string[] { ".jpg", ".bmp", ".png", ".tif", ".tiff" };
                string ext = Path.GetExtension(dosya).ToLower();
                if (ext == ".pdf")
                {
                    return Pdf;
                }
                if (ext == ".zip")
                {
                    return Zip;
                }
                if (imgext.Contains(ext))
                {
                    return Img;
                }
            }
            return null;
        }
    }
}