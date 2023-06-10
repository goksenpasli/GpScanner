using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Extensions;

public static class HSV
{
    public static RGB[] GetSpectrum()
    {
        RGB[] rgbs = new RGB[360];

        for (int h = 0; h < 360; h++)
        {
            rgbs[h] = RGBFromHSV(h, 1f, 1f);
        }

        return rgbs;
    }

    public static RGB[] GradientSpectrum()
    {
        RGB[] rgbs = new RGB[7];

        for (int h = 0; h < 7; h++)
        {
            rgbs[h] = RGBFromHSV(h * 60, 1f, 1f);
        }

        return rgbs;
    }

    public static RGB RGBFromHSV(double h, double s, double v)
    {
        if (h > 360 || h < 0 || s > 1 || s < 0 || v > 1 || v < 0)
        {
            return null;
        }

        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60 % 2) - 1));
        double m = v - c;

        double r = 0, g = 0, b = 0;

        if (h < 60)
        {
            r = c;
            g = x;
        }
        else if (h < 120)
        {
            r = x;
            g = c;
        }
        else if (h < 180)
        {
            g = c;
            b = x;
        }
        else if (h < 240)
        {
            g = x;
            b = c;
        }
        else if (h < 300)
        {
            r = x;
            b = c;
        }
        else if (h <= 360)
        {
            r = c;
            b = x;
        }

        return new RGB((r + m) * 255, (g + m) * 255, (b + m) * 255);
    }
}

[TemplatePart(Name = "SpectrumGrid", Type = typeof(Rectangle))]
[TemplatePart(Name = "RgbGrid", Type = typeof(Rectangle))]
public class ColorPicker : Control
{
    public static readonly DependencyProperty AlphaProperty = DependencyProperty.Register(
        "Alpha",
        typeof(byte),
        typeof(ColorPicker),
        new PropertyMetadata((byte)0xff, AlphaChanged));

    public static readonly DependencyProperty ColorPickerColumnCountProperty =
        DependencyProperty.Register("ColorPickerColumnCount", typeof(int), typeof(ColorPicker), new PropertyMetadata(8));

    public static readonly DependencyProperty HexCodeProperty = DependencyProperty.Register("HexCode", typeof(string), typeof(ColorPicker), new PropertyMetadata("#00000000"));

    public static readonly DependencyProperty HexCodeVisibilityProperty =
        DependencyProperty.Register("HexCodeVisibility", typeof(Visibility), typeof(ColorPicker), new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty MiddleStopColorProperty = DependencyProperty.Register(
        "MiddleStopColor",
        typeof(Color),
        typeof(ColorPicker),
        new PropertyMetadata(Colors.Gray));

    public static readonly DependencyProperty PredefinedColorVisibilityProperty =
        DependencyProperty.Register("PredefinedColorVisibility", typeof(Visibility), typeof(ColorPicker), new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty SliderVisibilityProperty = DependencyProperty.Register(
        "SliderVisibility",
        typeof(Visibility),
        typeof(ColorPicker),
        new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty SpectrumGridBackgroundProperty =
        DependencyProperty.Register("SpectrumGridBackground", typeof(Brush), typeof(ColorPicker), new PropertyMetadata(Brushes.Transparent));

    public RGB Selected = new();

    static ColorPicker()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorPicker), new FrameworkPropertyMetadata(typeof(ColorPicker)));
    }

    public ColorPicker()
    {
        RGB[] g6 = HSV.GradientSpectrum();

        LinearGradientBrush gradientBrush = new() { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        for (int i = 0; i < g6.Length; i++)
        {
            GradientStop stop = new(g6[i].Color(), i * 0.16);
            gradientBrush.GradientStops.Add(stop);
        }

        SpectrumGridBackground = gradientBrush;
        MiddleStopColor = HSV.RGBFromHSV(0, 1f, 1f).Color();
    }

    public byte Alpha { get => (byte)GetValue(AlphaProperty); set => SetValue(AlphaProperty, value); }

    public int ColorPickerColumnCount { get => (int)GetValue(ColorPickerColumnCountProperty); set => SetValue(ColorPickerColumnCountProperty, value); }

    public string HexCode { get => (string)GetValue(HexCodeProperty); set => SetValue(HexCodeProperty, value); }

    public Visibility HexCodeVisibility { get => (Visibility)GetValue(HexCodeVisibilityProperty); set => SetValue(HexCodeVisibilityProperty, value); }

    public Color MiddleStopColor { get => (Color)GetValue(MiddleStopColorProperty); set => SetValue(MiddleStopColorProperty, value); }

    public Visibility PredefinedColorVisibility {
        get => (Visibility)GetValue(PredefinedColorVisibilityProperty);
        set => SetValue(PredefinedColorVisibilityProperty, value);
    }

    public GridLength SelectorLength { get; set; } = new(1, GridUnitType.Star);

    public Visibility SliderVisibility { get => (Visibility)GetValue(SliderVisibilityProperty); set => SetValue(SliderVisibilityProperty, value); }

    public Brush SpectrumGridBackground { get => (Brush)GetValue(SpectrumGridBackgroundProperty); set => SetValue(SpectrumGridBackgroundProperty, value); }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _spectrumgrid = GetTemplateChild("SpectrumGrid") as Rectangle;
        if (_spectrumgrid != null)
        {
            _spectrumgrid.MouseMove -= Spectrumgrid_MouseMove;
            _spectrumgrid.MouseMove += Spectrumgrid_MouseMove;
            _spectrumgrid.MouseDown -= (sender, e) => e.Handled = true;
            _spectrumgrid.MouseDown += (sender, e) => e.Handled = true;
        }

        _rgbgrid = GetTemplateChild("RgbGrid") as Rectangle;
        if (_rgbgrid != null)
        {
            _rgbgrid.MouseMove -= Rgbgrid_MouseMove;
            _rgbgrid.MouseMove += Rgbgrid_MouseMove;
            _rgbgrid.MouseDown -= (sender, e) => e.Handled = true;
            _rgbgrid.MouseDown += (sender, e) => e.Handled = true;
        }
    }

    private Rectangle _rgbgrid;

    private Rectangle _spectrumgrid;

    private double currH = 360;

    private static void AlphaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPicker colorPicker)
        {
            colorPicker._rgbgrid.Opacity = (double)colorPicker.Alpha / 255;
        }
    }

    private void Rgbgrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            Point pos = e.GetPosition(_rgbgrid);
            double x = pos.X;
            _ = pos.Y;
            RGB c = x < _rgbgrid.ActualWidth / 2
                ? HSV.RGBFromHSV(currH, 1f, x / (_rgbgrid.ActualWidth / 2))
                : HSV.RGBFromHSV(currH, ((_rgbgrid.ActualWidth / 2) - (x - (_rgbgrid.ActualWidth / 2))) / _rgbgrid.ActualWidth, 1f);

            HexCode = $"#{c.Hex(Alpha)}";
            Selected = c;
        }
    }

    private void Spectrumgrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            double x = e.GetPosition(_spectrumgrid).X;
            currH = 360 * (x / _spectrumgrid.ActualWidth);
            MiddleStopColor = HSV.RGBFromHSV(currH, 1f, 1f).Color();
        }
    }
}

public class RGB
{
    public RGB()
    {
        R = 0xff;
        G = 0xff;
        B = 0xff;
    }

    public RGB(double r, double g, double b)
    {
        if (r > 255 || g > 255 || b > 255)
        {
            throw new ArgumentException("RGB must be under 255 (1byte)");
        }

        R = (byte)r;
        G = (byte)g;
        B = (byte)b;
    }

    public byte B { get; set; }

    public byte G { get; set; }

    public byte R { get; set; }

    public Color Color()
    {
        return new() { R = R, G = G, B = B, A = 255 };
    }

    public string Hex(byte Alpha)
    {
        return BitConverter.ToString(new[] { Alpha, R, G, B }).Replace("-", string.Empty);
    }
}