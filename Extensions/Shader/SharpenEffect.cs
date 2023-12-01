using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Extensions;

/// <summary>
/// An effect that sharpens the input.
/// </summary>
public class SharpenEffect : ShaderEffect
{
    public static readonly DependencyProperty AmountProperty = DependencyProperty.Register(
        "Amount",
        typeof(double),
        typeof(SharpenEffect),
        new UIPropertyMetadata(1D, PixelShaderConstantCallback(0)));
    public static readonly DependencyProperty InputProperty =
        RegisterPixelShaderSamplerProperty("Input", typeof(SharpenEffect), 0);
    public static readonly DependencyProperty InputSizeProperty = DependencyProperty.Register(
        "InputSize",
        typeof(Size),
        typeof(SharpenEffect),
        new UIPropertyMetadata(new Size(800D, 600D), PixelShaderConstantCallback(1)));

    public SharpenEffect()
    {
        PixelShader = new PixelShader { UriSource = new Uri("/Extensions;component/Shader/SharpenEffect.ps", UriKind.Relative) };

        UpdateShaderValue(InputProperty);
        UpdateShaderValue(AmountProperty);
        UpdateShaderValue(InputSizeProperty);
    }

    /// <summary>
    /// The amount of sharpening.
    /// </summary>
    public double Amount { get => (double)GetValue(AmountProperty); set => SetValue(AmountProperty, value); }

    public Brush Input { get => (Brush)GetValue(InputProperty); set => SetValue(InputProperty, value); }

    /// <summary>
    /// The size of the input (in pixels).
    /// </summary>
    public Size InputSize { get => (Size)GetValue(InputSizeProperty); set => SetValue(InputSizeProperty, value); }
}