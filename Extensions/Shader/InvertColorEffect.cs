using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Extensions;

/// <summary>
/// An effect that inverts all colors.
/// </summary>
public class InvertColorEffect : ShaderEffect
{
    public InvertColorEffect()
    {
        PixelShader = new PixelShader { UriSource = new Uri("/Extensions;component/Shader/InvertColorEffect.ps", UriKind.Relative) };

        UpdateShaderValue(InputProperty);
    }

    public Brush Input { get => (Brush)GetValue(InputProperty); set => SetValue(InputProperty, value); }

    public static readonly DependencyProperty InputProperty =
        RegisterPixelShaderSamplerProperty("Input", typeof(InvertColorEffect), 0);
}