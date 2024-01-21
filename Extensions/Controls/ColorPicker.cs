using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Extensions;

[TemplatePart(Name = "SpectrumGrid", Type = typeof(Rectangle))]
[TemplatePart(Name = "RgbGrid", Type = typeof(Rectangle))]
public class ColorPicker : Control
{
    public static readonly DependencyProperty AlphaProperty = DependencyProperty.Register("Alpha", typeof(byte), typeof(ColorPicker), new PropertyMetadata((byte)0xff, AlphaChanged));
    public static readonly DependencyProperty ColorPickerColumnCountProperty =
        DependencyProperty.Register("ColorPickerColumnCount", typeof(int), typeof(ColorPicker), new PropertyMetadata(8));
    public static readonly DependencyProperty HexCodeProperty = DependencyProperty.Register("HexCode", typeof(string), typeof(ColorPicker), new PropertyMetadata("#00000000"));
    public static readonly DependencyProperty HexCodeVisibilityProperty =
        DependencyProperty.Register("HexCodeVisibility", typeof(Visibility), typeof(ColorPicker), new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty MiddleStopColorProperty = DependencyProperty.Register("MiddleStopColor", typeof(Color), typeof(ColorPicker), new PropertyMetadata(Colors.Gray));
    public static readonly DependencyProperty PredefinedColorVisibilityProperty =
        DependencyProperty.Register("PredefinedColorVisibility", typeof(Visibility), typeof(ColorPicker), new PropertyMetadata(Visibility.Collapsed));
    public static readonly DependencyProperty SliderVisibilityProperty = DependencyProperty.Register("SliderVisibility", typeof(Visibility), typeof(ColorPicker), new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty SpectrumGridBackgroundProperty =
        DependencyProperty.Register("SpectrumGridBackground", typeof(Brush), typeof(ColorPicker), new PropertyMetadata(Brushes.Transparent));
    public RGB Selected = new();
    private Rectangle _rgbgrid;
    private Rectangle _spectrumgrid;
    private double currH = 360;

    static ColorPicker() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorPicker), new FrameworkPropertyMetadata(typeof(ColorPicker))); }

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

    public PropertyInfo[] ListBoxColors { get; } = typeof(Colors).GetProperties();

    public Color MiddleStopColor { get => (Color)GetValue(MiddleStopColorProperty); set => SetValue(MiddleStopColorProperty, value); }

    public Visibility PredefinedColorVisibility { get => (Visibility)GetValue(PredefinedColorVisibilityProperty); set => SetValue(PredefinedColorVisibilityProperty, value); }

    public GridLength SelectorLength { get; set; } = new(1, GridUnitType.Star);

    public Visibility SliderVisibility { get => (Visibility)GetValue(SliderVisibilityProperty); set => SetValue(SliderVisibilityProperty, value); }

    public Brush SpectrumGridBackground { get => (Brush)GetValue(SpectrumGridBackgroundProperty); set => SetValue(SpectrumGridBackgroundProperty, value); }

    public static string ConvertColorNameToHex(string colorName)
    {
        Color color = (Color)ColorConverter.ConvertFromString(colorName);
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

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
            RGB c = x < _rgbgrid.ActualWidth / 2 ? HSV.RGBFromHSV(currH, 1f, x / (_rgbgrid.ActualWidth / 2)) : HSV.RGBFromHSV(currH, ((_rgbgrid.ActualWidth / 2) - (x - (_rgbgrid.ActualWidth / 2))) / _rgbgrid.ActualWidth, 1f);

            HexCode = $"#{c?.Hex(Alpha)}";
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
