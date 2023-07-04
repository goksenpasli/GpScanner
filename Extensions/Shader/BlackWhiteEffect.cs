using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Extensions;

/// <summary>
///     An effect that turns the input into black/white colors.
/// </summary>
public class BlackWhiteEffect : ShaderEffect
{
    public BlackWhiteEffect()
    {
        PixelShader = new PixelShader
        { UriSource = new Uri("/Extensions;component/Shader/BlackWhiteEffect.ps", UriKind.Relative) };

        UpdateShaderValue(InputProperty);
        UpdateShaderValue(ThresholdProperty);
    }

    public Brush Input {
        get => (Brush)GetValue(InputProperty);
        set => SetValue(InputProperty, value);
    }

    /// <summary>
    ///     The Threshold value to convert pixel from black to white.
    /// </summary>
    public double Threshold {
        get => (double)GetValue(ThresholdProperty);
        set => SetValue(ThresholdProperty, value);
    }

    public static readonly DependencyProperty InputProperty =
                    RegisterPixelShaderSamplerProperty("Input", typeof(BlackWhiteEffect), 0);

    public static readonly DependencyProperty ThresholdProperty = DependencyProperty.Register(
        "Threshold",
        typeof(double),
        typeof(BlackWhiteEffect),
        new UIPropertyMetadata(0.6D, PixelShaderConstantCallback(1)));
}