using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Extensions;

/// <summary>
///     An effect that embosses the input.
/// </summary>
public class EmbossedEffect : ShaderEffect
{
    public EmbossedEffect()
    {
        PixelShader = new PixelShader
        { UriSource = new Uri("/Extensions;component/Shader/EmbossedEffect.ps", UriKind.Relative) };

        UpdateShaderValue(InputProperty);
        UpdateShaderValue(EmbossedAmountProperty);
        UpdateShaderValue(WidthProperty);
    }

    /// <summary>
    ///     The amplitude of the embossing.
    /// </summary>
    public double EmbossedAmount {
        get => (double)GetValue(EmbossedAmountProperty);
        set => SetValue(EmbossedAmountProperty, value);
    }

    public Brush Input {
        get => (Brush)GetValue(InputProperty);
        set => SetValue(InputProperty, value);
    }

    /// <summary>
    ///     The separation between samples (as a fraction of input size).
    /// </summary>
    public double Width {
        get => (double)GetValue(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    public static readonly DependencyProperty EmbossedAmountProperty = DependencyProperty.Register(
                        "EmbossedAmount",
        typeof(double),
        typeof(EmbossedEffect),
        new UIPropertyMetadata(0.5D, PixelShaderConstantCallback(0)));

    public static readonly DependencyProperty InputProperty =
        RegisterPixelShaderSamplerProperty("Input", typeof(EmbossedEffect), 0);

    public static readonly DependencyProperty WidthProperty = DependencyProperty.Register(
        "Width",
        typeof(double),
        typeof(EmbossedEffect),
        new UIPropertyMetadata(0.003D, PixelShaderConstantCallback(1)));
}