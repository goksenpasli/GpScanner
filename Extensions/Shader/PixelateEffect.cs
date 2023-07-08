using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Extensions;

/// <summary>
/// An effect that turns the input into blocky pixels.
/// </summary>
public class PixelateEffect : ShaderEffect
{
    public static readonly DependencyProperty BrickOffsetProperty = DependencyProperty.Register("BrickOffset", typeof(double), typeof(PixelateEffect), new UIPropertyMetadata(0D, PixelShaderConstantCallback(1)));

    public static readonly DependencyProperty InputProperty =
        RegisterPixelShaderSamplerProperty("Input", typeof(PixelateEffect), 0);

    public static readonly DependencyProperty PixelCountsProperty = DependencyProperty.Register("PixelCounts", typeof(Size), typeof(PixelateEffect), new UIPropertyMetadata(new Size(60D, 40D), PixelShaderConstantCallback(0)));

    public PixelateEffect()
    {
        PixelShader = new PixelShader { UriSource = new Uri("/Extensions;component/Shader/PixelateEffect.ps", UriKind.Relative) };

        UpdateShaderValue(InputProperty);
        UpdateShaderValue(PixelCountsProperty);
        UpdateShaderValue(BrickOffsetProperty);
    }

    /// <summary>
    /// The amount to shift alternate rows (use 1 to get a brick wall look).
    /// </summary>
    public double BrickOffset { get => (double)GetValue(BrickOffsetProperty); set => SetValue(BrickOffsetProperty, value); }

    public Brush Input { get => (Brush)GetValue(InputProperty); set => SetValue(InputProperty, value); }

    /// <summary>
    /// The number of horizontal and vertical pixel blocks.
    /// </summary>
    public Size PixelCounts { get => (Size)GetValue(PixelCountsProperty); set => SetValue(PixelCountsProperty, value); }
}