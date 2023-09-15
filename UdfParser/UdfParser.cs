using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UdfParser;

public static class UdfParser
{
    public static IDocumentPaginatorSource RenderDocument(Template content)
    {
        FlowDocument flowdocument = new();
        FlowDocumentScrollViewer flowdocumentscrollviewer = new();
        Textcreate(content, flowdocument);
        Tblcreate(content, flowdocument);
        Imgcreate(content, flowdocument);
        Bgimgcreate(content, flowdocument);
        PrintDialog pd = new();
        PageFormat pageformat = content.Properties.PageFormat;
        flowdocumentscrollviewer.Document = flowdocument;
        flowdocumentscrollviewer.Document.PagePadding = new Thickness(pageformat.LeftMargin * 4 / 3, pageformat.TopMargin * 4 / 3, pageformat.RightMargin * 4 / 3, pageformat.BottomMargin * 4 / 3);
        flowdocumentscrollviewer.Document.ColumnWidth = pd.PrintableAreaWidth;
        return flowdocumentscrollviewer.Document;
    }

    private static void Bgimgcreate(Template content, FlowDocument flowdocument)
    {
        BgImage bgimage = content.Properties.BgImage;
        if (bgimage != null)
        {
            byte[] binaryData = Convert.FromBase64String(content.Properties.BgImage.BgImageData);
            BitmapImage bi = new();
            bi.BeginInit();
            bi.StreamSource = new MemoryStream(binaryData);
            bi.EndInit();
            bi.Freeze();
            System.Windows.Controls.Image v = new() { Source = bi, Margin = new Thickness(bgimage.BgImageLeftMargin * 4 / 3, bgimage.BgImageUpMargin * 4 / 3, bgimage.BgImageRigtMargin * 4 / 3, bgimage.BgImageBottomMargin * 4 / 3) };
            flowdocument.Blocks.Add(new BlockUIContainer(v));
        }
    }

    private static Run[,] Getcellcontent(Table table, Template content, int genişlik, int yükseklik)
    {
        System.Collections.Generic.List<Content> cellparagrafcontent = table.Row
            .SelectMany(z => z.Cell)
            .SelectMany(z => z.Paragraph)
            .Select(
                z => new Content
                {
                    Background = z.Content.FirstOrDefault().Background,
                    Alignment = z.Alignment,
                    Bold = z.Content.FirstOrDefault().Bold,
                    Bulleted = z.Bulleted,
                    Description = z.Content.FirstOrDefault().Description,
                    Family = z.Content.FirstOrDefault().Family,
                    Foreground = z.Content.FirstOrDefault().Foreground,
                    Italic = z.Content.FirstOrDefault().Italic,
                    LeftIndent = z.LeftIndent,
                    StartOffset = z.Content.FirstOrDefault().StartOffset,
                    Strikethrough = z.Content.FirstOrDefault().Strikethrough,
                    Length = z.Content.FirstOrDefault().Length,
                    RightIndent = z.Content.FirstOrDefault().RightIndent,
                    Size = z.Content.FirstOrDefault().Size,
                    Superscript = z.Content.FirstOrDefault().Superscript,
                    Underline = z.Content.FirstOrDefault().Underline,
                    Subscript = z.Content.FirstOrDefault().Subscript
                })
            .ToList();

        Run[,] array = new Run[genişlik, yükseklik];
        int j = 0;
        for (int x = 0; x < yükseklik; x++)
        {
            for (int i = 0; i < genişlik; i++)
            {
                Content cellcontent = cellparagrafcontent.ElementAtOrDefault(j);
                if (cellcontent is not null)
                {
                    string text = content.Content.Substring(cellcontent.StartOffset, cellcontent.Length);
                    array[i, x] = GetRun(text, cellparagrafcontent.ElementAtOrDefault(j));
                }

                j++;
            }
        }

        return array;
    }

    private static Run GetRun(string text, Content xmlparagraphcontent)
    {
        Run inline = new(text);
        if (xmlparagraphcontent != null)
        {
            if (xmlparagraphcontent.Bulleted)
            {
                inline.Text = $"\t•{inline.Text}";
            }

            if (xmlparagraphcontent.Foreground != 0)
            {
                inline.Foreground =
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString($"#{xmlparagraphcontent.Foreground:X}"));
            }

            if (xmlparagraphcontent.Background != 0)
            {
                inline.Background =
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString($"#{xmlparagraphcontent.Background:X}"));
            }

            inline.FontSize = xmlparagraphcontent.Size == 0 ? 16 : xmlparagraphcontent.Size * 4 / 3;

            inline.FontFamily = string.IsNullOrWhiteSpace(xmlparagraphcontent.Family) ? new FontFamily("Times New Roman") : new FontFamily(xmlparagraphcontent.Family);
            if (xmlparagraphcontent.Bold)
            {
                inline.FontWeight = FontWeights.Bold;
            }

            if (xmlparagraphcontent.Italic)
            {
                inline.FontStyle = FontStyles.Italic;
            }

            if (xmlparagraphcontent.Strikethrough)
            {
                inline.TextDecorations = TextDecorations.Strikethrough;
            }

            if (xmlparagraphcontent.Underline)
            {
                inline.TextDecorations = TextDecorations.Underline;
            }

            if (xmlparagraphcontent.Subscript)
            {
                inline.BaselineAlignment = BaselineAlignment.Subscript;
            }

            if (xmlparagraphcontent.Superscript)
            {
                inline.BaselineAlignment = BaselineAlignment.Superscript;
            }
        }

        return inline;
    }

    private static void Imgcreate(Template content, FlowDocument flowdocument)
    {
        foreach (Image image in content.Elements.Paragraph.SelectMany(z => z.Image))
        {
            byte[] binaryData = Convert.FromBase64String(image.ImageData);
            BitmapImage bi = new();
            bi.BeginInit();
            bi.StreamSource = new MemoryStream(binaryData);
            bi.EndInit();
            bi.Freeze();
            System.Windows.Controls.Image v = new() { Source = bi, Width = image.Width * 4 / 3, Height = image.Height * 4 / 3 };
            flowdocument.Blocks.Add(new BlockUIContainer(v));
            binaryData = null;
        }
    }

    private static void Tblcreate(Template content, FlowDocument flowdocument)
    {
        foreach (Table udftable in content.Elements.Table)
        {
            System.Windows.Documents.Table table = new();
            for (int x = 0; x < udftable.ColumnCount; x++)
            {
                double width = Convert.ToDouble(udftable.ColumnSpans.Split(',')[x]);
                TableColumn tblcolumn = new() { Width = new GridLength(width / 794, GridUnitType.Star) };
                table.Columns.Add(tblcolumn);
                table.RowGroups.Add(new TableRowGroup());

                for (int i = 0; i < udftable.Row.Count; i++)
                {
                    table.RowGroups.Add(new TableRowGroup());
                    TableRow tr = new();
                    table.RowGroups[0].Rows.Add(tr);
                    TableRow currentRow = table.RowGroups[0].Rows[i];
                    Run textrun = Getcellcontent(udftable, content, udftable.ColumnCount, udftable.Row.Count)[x, i];
                    if (textrun is not null)
                    {
                        TableCell tc = new(new System.Windows.Documents.Paragraph(textrun)) { BorderThickness = new Thickness(1), BorderBrush = Brushes.Black };
                        currentRow.Cells.Add(tc);
                    }
                }

                flowdocument.Blocks.Add(table);
            }
        }
    }

    private static void Textcreate(Template content, FlowDocument flowdocument)
    {
        System.Collections.Generic.List<Content> documentcontent = content.Elements.Paragraph
            .ConvertAll(
                z => new Content
                {
                    Background = z.Content.FirstOrDefault().Background,
                    Alignment = z.Alignment,
                    Bold = z.Content.FirstOrDefault().Bold,
                    Bulleted = z.Bulleted,
                    Description = z.Content.FirstOrDefault().Description,
                    Family = z.Content.FirstOrDefault().Family,
                    Foreground = z.Content.FirstOrDefault().Foreground,
                    Italic = z.Content.FirstOrDefault().Italic,
                    LeftIndent = z.LeftIndent,
                    StartOffset = z.Content.FirstOrDefault().StartOffset,
                    Strikethrough = z.Content.FirstOrDefault().Strikethrough,
                    Length = z.Content.FirstOrDefault().Length,
                    RightIndent = z.Content.FirstOrDefault().RightIndent,
                    Size = z.Content.FirstOrDefault().Size,
                    Superscript = z.Content.FirstOrDefault().Superscript,
                    Underline = z.Content.FirstOrDefault().Underline,
                    Subscript = z.Content.FirstOrDefault().Subscript
                })
;
        foreach (Content element in documentcontent)
        {
            System.Windows.Documents.Paragraph paragraph = new();
            string text = content.Content.Substring(element.StartOffset, element.Length);
            Run inlinetext = GetRun(text, element);
            paragraph.LineHeight = 10;
            paragraph.Margin = new Thickness(0);
            paragraph.Padding = new Thickness(0);
            paragraph.TextIndent = element.LeftIndent * 4 / 3;
            paragraph.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            switch (element.Alignment)
            {
                case 3:
                    paragraph.TextAlignment = TextAlignment.Justify;
                    break;

                case 1:
                    paragraph.TextAlignment = TextAlignment.Center;
                    break;

                case 0:
                    paragraph.TextAlignment = TextAlignment.Left;
                    break;

                case 2:
                    paragraph.TextAlignment = TextAlignment.Right;
                    break;
            }

            paragraph.Inlines.Add(inlinetext);
            flowdocument.Blocks.Add(paragraph);
        }
    }
}