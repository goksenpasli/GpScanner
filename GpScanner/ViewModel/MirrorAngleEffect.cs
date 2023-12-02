using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace GpScanner.ViewModel;

public class MirrorAngleEffect : ShaderEffect
{
    public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(MirrorAngleEffect), 0);
    public static readonly DependencyProperty RelativeHeightProperty = DependencyProperty.Register("RelativeHeight", typeof(double), typeof(MirrorAngleEffect), new UIPropertyMetadata(0D, PixelShaderConstantCallback(0)));

    public MirrorAngleEffect()
    {
        PixelShader = new() { UriSource = new Uri("/GpScanner;component/Resources/MirrorAngleEffect.ps", UriKind.Relative) };

        UpdateShaderValue(InputProperty);
        UpdateShaderValue(RelativeHeightProperty);
    }

    public Brush Input { get => (Brush)GetValue(InputProperty); set => SetValue(InputProperty, value); }

    public double RelativeHeight { get => (double)GetValue(RelativeHeightProperty); set => SetValue(RelativeHeightProperty, value); }
}