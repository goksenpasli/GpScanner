using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace GpScanner.ViewModel;

public class RippleEffect : ShaderEffect
{
    public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(RippleEffect), 0);
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        "Progress",
        typeof(double),
        typeof(RippleEffect),
        new UIPropertyMetadata(0D, PixelShaderConstantCallback(0)));
    public static readonly DependencyProperty Texture2Property = RegisterPixelShaderSamplerProperty("Texture2", typeof(RippleEffect), 1);

    public RippleEffect()
    {
        PixelShader = new PixelShader { UriSource = new Uri("/GpScanner;component/Resources/RippleEffect.ps", UriKind.Relative) };

        UpdateShaderValue(InputProperty);
        UpdateShaderValue(Texture2Property);
        UpdateShaderValue(ProgressProperty);
    }

    public Brush Input { get => (Brush)GetValue(InputProperty); set => SetValue(InputProperty, value); }

    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    public Brush Texture2 { get => (Brush)GetValue(Texture2Property); set => SetValue(Texture2Property, value); }
}