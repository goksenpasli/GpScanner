namespace PdfiumViewer
{
    public enum HitTest
    {
        Error = NativeMethods.HitTestValues.HTERROR,

        Transparent = NativeMethods.HitTestValues.HTTRANSPARENT,

        Nowhere = NativeMethods.HitTestValues.HTNOWHERE,

        Client = NativeMethods.HitTestValues.HTCLIENT,

        Caption = NativeMethods.HitTestValues.HTCAPTION,

        SystemMenu = NativeMethods.HitTestValues.HTSYSMENU,

        GrowBox = NativeMethods.HitTestValues.HTGROWBOX,

        Menu = NativeMethods.HitTestValues.HTMENU,

        HorizontalScroll = NativeMethods.HitTestValues.HTHSCROLL,

        VerticalScroll = NativeMethods.HitTestValues.HTVSCROLL,

        MinimizeButton = NativeMethods.HitTestValues.HTMINBUTTON,

        MaximizeButton = NativeMethods.HitTestValues.HTMAXBUTTON,

        Left = NativeMethods.HitTestValues.HTLEFT,

        Right = NativeMethods.HitTestValues.HTRIGHT,

        Top = NativeMethods.HitTestValues.HTTOP,

        TopLeft = NativeMethods.HitTestValues.HTTOPLEFT,

        TopRight = NativeMethods.HitTestValues.HTTOPRIGHT,

        Bottom = NativeMethods.HitTestValues.HTBOTTOM,

        BottomLeft = NativeMethods.HitTestValues.HTBOTTOMLEFT,

        BottomRight = NativeMethods.HitTestValues.HTBOTTOMRIGHT,

        Border = NativeMethods.HitTestValues.HTBORDER,

        Object = NativeMethods.HitTestValues.HTOBJECT,

        CloseButton = NativeMethods.HitTestValues.HTCLOSE,

        HelpButton = NativeMethods.HitTestValues.HTHELP
    }
}