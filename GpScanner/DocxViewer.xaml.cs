using Extensions;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Xceed.Document.NET;
using Xceed.Words.NET;
using Color = System.Windows.Media.Color;
using FormattedText = Xceed.Document.NET.FormattedText;
using Paragraph = Xceed.Document.NET.Paragraph;
using Run = System.Windows.Documents.Run;

namespace GpScanner
{
    /// <summary>
    /// Interaction logic for DocxViewer.xaml
    /// </summary>
    public partial class DocxViewer : UserControl
    {
        public static readonly DependencyProperty DocxDataFilePathProperty = DependencyProperty.Register("DocxDataFilePath", typeof(string), typeof(DocxViewer), new PropertyMetadata(null, DocxDataFilePathChanged));

        public DocxViewer() { InitializeComponent(); }

        public string DocxDataFilePath { get => (string)GetValue(DocxDataFilePathProperty); set => SetValue(DocxDataFilePathProperty, value); }

        private static BlockUIContainer BlockUIContainerGetPicture(Picture picture)
        {
            using Bitmap bitmap = new(picture.Stream);
            System.Windows.Controls.Image image = new() { Source = bitmap?.ToBitmapImage(ImageFormat.Jpeg) };
            return new BlockUIContainer(image);
        }

        private static void DocxDataFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                if (d is DocxViewer viewer && e.NewValue is string uriString)
                {
                    if (Path.GetExtension(uriString.ToLower()) == ".docx")
                    {
                        using DocX document = DocX.Load(uriString);
                        viewer.Fd.Document = DocxFlowDocument(document);
                        return;
                    }
                    if (Path.GetExtension(uriString.ToLower()) is ".txt" or ".xml" or ".xsl" or ".xslt" or ".xaml")
                    {
                        System.Windows.Documents.Paragraph paragraph = new();
                        paragraph.Inlines.Add(File.ReadAllText(uriString));
                        viewer.Fd.Document = new FlowDocument(paragraph);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException(ex.Message);
            }
        }

        private static FlowDocument DocxFlowDocument(DocX document)
        {
            FlowDocument fd = new();
            foreach (Paragraph docxparagraph in document.Paragraphs)
            {
                System.Windows.Documents.Paragraph paragraph = new();
                foreach (FormattedText formattedText in docxparagraph?.MagicText)
                {
                    paragraph.Inlines.Add(GetRun(docxparagraph, paragraph, formattedText));
                    fd.Blocks.Add(paragraph);
                }

                if (docxparagraph?.Pictures?.Count > 0)
                {
                    foreach (Picture picture in docxparagraph.Pictures)
                    {
                        fd.Blocks.Add(BlockUIContainerGetPicture(picture));
                    }
                }
            }

            return fd;
        }

        private static Run GetRun(Paragraph docxparagraph, System.Windows.Documents.Paragraph paragraph, FormattedText formattedText)
        {
            Run inline = new(formattedText.text)
            {
                FontSize = formattedText.formatting?.Size * 4 / 3 ?? 16,
                FontFamily = formattedText.formatting?.FontFamily == null ? new System.Windows.Media.FontFamily("Times New Roman") : new System.Windows.Media.FontFamily(formattedText.formatting?.FontFamily.Name)
            };
            if (formattedText?.formatting != null)
            {
                if (formattedText.formatting.FontColor.HasValue)
                {
                    SolidColorBrush sb = new(Color.FromArgb(formattedText.formatting.FontColor.Value.A, formattedText.formatting.FontColor.Value.R, formattedText.formatting.FontColor.Value.G, formattedText.formatting.FontColor.Value.B));
                    sb.Freeze();
                    inline.Foreground = sb;
                }

                if (formattedText.formatting.ShadingPattern is not null)
                {
                    SolidColorBrush sb = new(Color.FromArgb(formattedText.formatting.ShadingPattern.Fill.A, formattedText.formatting.ShadingPattern.Fill.R, formattedText.formatting.ShadingPattern.Fill.G, formattedText.formatting.ShadingPattern.Fill.B));
                    sb.Freeze();
                    inline.Background = sb;
                }

                if (formattedText.formatting.Bold == true)
                {
                    inline.FontWeight = FontWeights.Bold;
                }

                if (formattedText.formatting.Italic == true)
                {
                    inline.FontStyle = FontStyles.Italic;
                }

                if (formattedText.formatting.StrikeThrough.HasValue)
                {
                    inline.TextDecorations = TextDecorations.Strikethrough;
                }

                if (formattedText.formatting.UnderlineStyle.HasValue)
                {
                    inline.TextDecorations = TextDecorations.Underline;
                }

                if (formattedText.formatting.Script.HasValue)
                {
                    if (formattedText.formatting.Script == Script.subscript)
                    {
                        inline.BaselineAlignment = BaselineAlignment.Subscript;
                    }

                    if (formattedText.formatting.Script == Script.superscript)
                    {
                        inline.BaselineAlignment = BaselineAlignment.Superscript;
                    }
                }
            }

            switch (docxparagraph.Alignment)
            {
                case Alignment.both:
                    paragraph.TextAlignment = TextAlignment.Justify;
                    break;
                case Alignment.center:
                    paragraph.TextAlignment = TextAlignment.Center;
                    break;
                case Alignment.left:
                    paragraph.TextAlignment = TextAlignment.Left;
                    break;
                case Alignment.right:
                    paragraph.TextAlignment = TextAlignment.Right;
                    break;
            }

            return inline;
        }
    }
}
