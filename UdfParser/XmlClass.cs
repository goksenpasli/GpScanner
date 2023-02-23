using System.Collections.Generic;
using System.Xml.Serialization;

namespace UdfParser
{
    [XmlRoot(ElementName = "bgImage")]
    public class BgImage
    {
        [XmlAttribute(AttributeName = "bgImageBottomMargin")]
        public int BgImageBottomMargin { get; set; }

        [XmlAttribute(AttributeName = "bgImageData")]
        public string BgImageData { get; set; }

        [XmlAttribute(AttributeName = "bgImageLeftMargin")]
        public int BgImageLeftMargin { get; set; }

        [XmlAttribute(AttributeName = "bgImageRigtMargin")]
        public int BgImageRigtMargin { get; set; }

        [XmlAttribute(AttributeName = "bgImageSource")]
        public string BgImageSource { get; set; }

        [XmlAttribute(AttributeName = "bgImageUpMargin")]
        public int BgImageUpMargin { get; set; }
    }

    [XmlRoot(ElementName = "cell")]
    public class Cell
    {
        [XmlElement(ElementName = "paragraph")]
        public List<Paragraph> Paragraph { get; set; }

        [XmlElement(ElementName = "table")]
        public List<Table> Table { get; set; }
    }

    [XmlRoot(ElementName = "content")]
    public class Content
    {
        [XmlAttribute(AttributeName = "Alignment")]
        public int Alignment { get; set; }

        [XmlAttribute(AttributeName = "background")]
        public int Background { get; set; }

        [XmlAttribute(AttributeName = "bold")]
        public bool Bold { get; set; }

        [XmlAttribute(AttributeName = "bulleted")]
        public bool Bulleted { get; set; }

        [XmlAttribute(AttributeName = "description")]
        public string Description { get; set; }

        [XmlAttribute(AttributeName = "family")]
        public string Family { get; set; }

        [XmlAttribute(AttributeName = "foreground")]
        public int Foreground { get; set; }

        [XmlAttribute(AttributeName = "italic")]
        public bool Italic { get; set; }

        [XmlAttribute(AttributeName = "LeftIndent")]
        public double LeftIndent { get; set; }

        [XmlAttribute(AttributeName = "length")]
        public int Length { get; set; }

        [XmlAttribute(AttributeName = "RightIndent")]
        public double RightIndent { get; set; }

        [XmlAttribute(AttributeName = "size")]
        public int Size { get; set; }

        [XmlAttribute(AttributeName = "startOffset")]
        public int StartOffset { get; set; }

        [XmlAttribute(AttributeName = "strikethrough")]
        public bool Strikethrough { get; set; }

        [XmlAttribute(AttributeName = "subscript")]
        public bool Subscript { get; set; }

        [XmlAttribute(AttributeName = "superscript")]
        public bool Superscript { get; set; }

        [XmlAttribute(AttributeName = "underline")]
        public bool Underline { get; set; }
    }

    [XmlRoot(ElementName = "elements")]
    public class Elements
    {
        [XmlElement(ElementName = "footer")]
        public Footer Footer { get; set; }

        [XmlElement(ElementName = "header")]
        public Header Header { get; set; }

        [XmlElement(ElementName = "paragraph")]
        public List<Paragraph> Paragraph { get; set; }

        [XmlAttribute(AttributeName = "resolver")]
        public string Resolver { get; set; }

        [XmlElement(ElementName = "table")]
        public List<Table> Table { get; set; }
    }

    [XmlRoot(ElementName = "footer")]
    public class Footer
    {
        [XmlAttribute(AttributeName = "pageNumber-color")]
        public int PageNumberColor { get; set; }

        [XmlAttribute(AttributeName = "pageNumber-fontFace")]
        public string PageNumberFontFace { get; set; }

        [XmlAttribute(AttributeName = "pageNumber-fontSize")]
        public int PageNumberFontSize { get; set; }

        [XmlAttribute(AttributeName = "pageNumber-foreStr")]
        public string PageNumberForeStr { get; set; }

        [XmlAttribute(AttributeName = "pageNumber-pageStartNumStr")]
        public string PageNumberPageStartNumStr { get; set; }

        [XmlAttribute(AttributeName = "pageNumber-spec")]
        public string PageNumberSpec { get; set; }

        [XmlElement(ElementName = "paragraph")]
        public Paragraph Paragraph { get; set; }
    }

    [XmlRoot(ElementName = "header")]
    public class Header
    {
        [XmlElement(ElementName = "paragraph")]
        public Paragraph Paragraph { get; set; }
    }

    [XmlRoot(ElementName = "image")]
    public class Image
    {
        [XmlAttribute(AttributeName = "height")]
        public double Height { get; set; }

        [XmlAttribute(AttributeName = "imageData")]
        public string ImageData { get; set; }

        [XmlAttribute(AttributeName = "length")]
        public int Length { get; set; }

        [XmlAttribute(AttributeName = "startOffset")]
        public int StartOffset { get; set; }

        [XmlAttribute(AttributeName = "width")]
        public double Width { get; set; }
    }

    [XmlRoot(ElementName = "pageFormat")]
    public class PageFormat
    {
        [XmlAttribute(AttributeName = "bottomMargin")]
        public double BottomMargin { get; set; }

        [XmlAttribute(AttributeName = "footerFOffset")]
        public double FooterFOffset { get; set; }

        [XmlAttribute(AttributeName = "headerFOffset")]
        public double HeaderFOffset { get; set; }

        [XmlAttribute(AttributeName = "leftMargin")]
        public double LeftMargin { get; set; }

        [XmlAttribute(AttributeName = "mediaSizeName")]
        public int MediaSizeName { get; set; }

        [XmlAttribute(AttributeName = "paperOrientation")]
        public int PaperOrientation { get; set; }

        [XmlAttribute(AttributeName = "rightMargin")]
        public double RightMargin { get; set; }

        [XmlAttribute(AttributeName = "topMargin")]
        public double TopMargin { get; set; }
    }

    [XmlRoot(ElementName = "paragraph")]
    public class Paragraph
    {
        [XmlAttribute(AttributeName = "Alignment")]
        public int Alignment { get; set; }

        [XmlAttribute(AttributeName = "Bulleted")]
        public bool Bulleted { get; set; }

        [XmlAttribute(AttributeName = "BulletType")]
        public string BulletType { get; set; }

        [XmlElement(ElementName = "content")]
        public List<Content> Content { get; set; }

        [XmlAttribute(AttributeName = "description")]
        public string Description { get; set; }

        [XmlAttribute(AttributeName = "family")]
        public string Family { get; set; }

        [XmlElement(ElementName = "image")]
        public List<Image> Image { get; set; }

        [XmlAttribute(AttributeName = "LeftIndent")]
        public double LeftIndent { get; set; }

        [XmlAttribute(AttributeName = "LineSpacing")]
        public double LineSpacing { get; set; }

        [XmlAttribute(AttributeName = "ListId")]
        public int ListId { get; set; }

        [XmlAttribute(AttributeName = "ListLevel")]
        public int ListLevel { get; set; }

        [XmlAttribute(AttributeName = "Numbered")]
        public bool Numbered { get; set; }

        [XmlAttribute(AttributeName = "NumberType")]
        public string NumberType { get; set; }

        [XmlAttribute(AttributeName = "RightIndent")]
        public double RightIndent { get; set; }

        [XmlAttribute(AttributeName = "size")]
        public int Size { get; set; }
    }

    [XmlRoot(ElementName = "properties")]
    public class Properties
    {
        [XmlElement(ElementName = "bgImage")]
        public BgImage BgImage { get; set; }

        [XmlElement(ElementName = "pageFormat")]
        public PageFormat PageFormat { get; set; }
    }

    [XmlRoot(ElementName = "row")]
    public class Row
    {
        [XmlElement(ElementName = "cell")]
        public List<Cell> Cell { get; set; }

        [XmlAttribute(AttributeName = "height")]
        public double Height { get; set; }

        [XmlAttribute(AttributeName = "rowName")]
        public string RowName { get; set; }

        [XmlAttribute(AttributeName = "rowType")]
        public string RowType { get; set; }
    }

    [XmlRoot(ElementName = "style")]
    public class Style
    {
        [XmlAttribute(AttributeName = "bold")]
        public bool Bold { get; set; }

        [XmlAttribute(AttributeName = "description")]
        public string Description { get; set; }

        [XmlAttribute(AttributeName = "family")]
        public string Family { get; set; }

        [XmlAttribute(AttributeName = "FONT_ATTRIBUTE_KEY")]
        public string FONTATTRIBUTEKEY { get; set; }

        [XmlAttribute(AttributeName = "foreground")]
        public int Foreground { get; set; }

        [XmlAttribute(AttributeName = "italic")]
        public bool Italic { get; set; }

        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "size")]
        public int Size { get; set; }
    }

    [XmlRoot(ElementName = "styles")]
    public class Styles
    {
        [XmlElement(ElementName = "style")]
        public List<Style> Style { get; set; }
    }

    [XmlRoot(ElementName = "table")]
    public class Table
    {
        [XmlAttribute(AttributeName = "border")]
        public string Border { get; set; }

        [XmlAttribute(AttributeName = "columnCount")]
        public int ColumnCount { get; set; }

        [XmlAttribute(AttributeName = "columnSpans")]
        public string ColumnSpans { get; set; }

        [XmlElement(ElementName = "row")]
        public List<Row> Row { get; set; }

        [XmlAttribute(AttributeName = "tableName")]
        public string TableName { get; set; }
    }

    [XmlRoot(ElementName = "template")]
    public class Template
    {
        [XmlElement(ElementName = "content")]
        public string Content { get; set; }

        [XmlElement(ElementName = "elements")]
        public Elements Elements { get; set; }

        [XmlAttribute(AttributeName = "format_id")]
        public string FormatId { get; set; }

        [XmlElement(ElementName = "properties")]
        public Properties Properties { get; set; }

        [XmlElement(ElementName = "styles")]
        public Styles Styles { get; set; }

        [XmlText]
        public string Text { get; set; }
    }
}