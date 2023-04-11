using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace TwainControl
{
    /// <summary>
    /// Interaction logic for XmlViewerControl.xaml
    /// </summary>
    public partial class XmlViewerControl : UserControl
    {
        public static readonly DependencyProperty XmlContentProperty = DependencyProperty.Register(
        "XmlContent", typeof(string), typeof(XmlViewerControl), new PropertyMetadata(default(string), OnXmlContentChanged));

        public XmlViewerControl()
        {
            InitializeComponent();
        }

        public string XmlContent {
            get => (string)GetValue(XmlContentProperty);
            set => SetValue(XmlContentProperty, value);
        }

        private static void OnXmlContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is XmlViewerControl xmlViewerControl && e.NewValue is string xmlfilepath && File.Exists(xmlfilepath))
            {
                FlowDocument document = xmlViewerControl.XmlRichTextBox?.Document;
                document?.Blocks?.Clear();
                Paragraph xmlParagraph = new();
                xmlParagraph.Inlines.Add(new Run(File.ReadAllText(xmlfilepath)));
                document?.Blocks?.Add(xmlParagraph);
            }
        }
    }
}