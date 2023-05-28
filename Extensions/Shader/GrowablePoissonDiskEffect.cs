using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Extensions;

/// <summary>
/// An effect that blurs the input using Poisson disk sampling.
/// </summary>
public class GrowablePoissonDiskEffect : ShaderEffect
{
    public GrowablePoissonDiskEffect()
    {
        PixelShader = new PixelShader { UriSource = new Uri("/Extensions;component/Shader/GrowablePoissonDiskEffect.ps", UriKind.Relative) };

        UpdateShaderValue(InputProperty);
        UpdateShaderValue(DiskRadiusProperty);
        UpdateShaderValue(InputSizeProperty);
    }

    /// <summary>
    /// The radius of the Poisson disk (in pixels).
    /// </summary>
    public double DiskRadius { get => (double)GetValue(DiskRadiusProperty); set => SetValue(DiskRadiusProperty, value); }

    public Brush Input { get => (Brush)GetValue(InputProperty); set => SetValue(InputProperty, value); }

    /// <summary>
    /// The size of the input (in pixels).
    /// </summary>
    public Size InputSize { get => (Size)GetValue(InputSizeProperty); set => SetValue(InputSizeProperty, value); }

    public static readonly DependencyProperty DiskRadiusProperty = DependencyProperty.Register(
        "DiskRadius",
        typeof(double),
        typeof(GrowablePoissonDiskEffect),
        new UIPropertyMetadata(5D, PixelShaderConstantCallback(0)));

    public static readonly DependencyProperty InputProperty =
        RegisterPixelShaderSamplerProperty("Input", typeof(GrowablePoissonDiskEffect), 0);

    public static readonly DependencyProperty InputSizeProperty = DependencyProperty.Register(
        "InputSize",
        typeof(Size),
        typeof(GrowablePoissonDiskEffect),
        new UIPropertyMetadata(new Size(600D, 400D), PixelShaderConstantCallback(1)));
}