using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Xml;

namespace TwainControl;

public class XmlViewerControlModel
{
    public static readonly DependencyProperty XmlContentProperty = DependencyProperty.RegisterAttached("XmlContent", typeof(string), typeof(XmlViewerControlModel), new PropertyMetadata(null, Changed));

    public static string GetXmlContent(DependencyObject obj) => (string)obj.GetValue(XmlContentProperty);

    public static void SetXmlContent(DependencyObject obj, string value) => obj.SetValue(XmlContentProperty, value);

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is XmlViewerControl xmlViewerControl && e.NewValue is string path && File.Exists(path))
        {
            try
            {
                XmlDocument XMLdoc = new();
                XMLdoc?.Load(path);
                Binding binding = new() { Source = new XmlDataProvider { Document = XMLdoc }, XPath = "child::node()" };
                _ = xmlViewerControl.xmlTree.SetBinding(ItemsControl.ItemsSourceProperty, binding);
            }
            catch (XmlException ex)
            {
                _ = Application.Current.Dispatcher.InvokeAsync(() => MessageBox.Show(ex?.Message, "GPSCANNER", MessageBoxButton.OK, MessageBoxImage.Warning));
            }
        }
    }
}