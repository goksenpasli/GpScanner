using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace GpScanner.ViewModel;

public class FoldEffect : ShaderEffect
{
    public FoldEffect()
    {
        PixelShader = new PixelShader
        { UriSource = new Uri("/GpScanner;component/Resources/FoldEffect.ps", UriKind.Relative) };

        UpdateShaderValue(InputProperty);
        UpdateShaderValue(FoldAmountProperty);
    }

    public double FoldAmount {
        get => (double)GetValue(FoldAmountProperty);
        set => SetValue(FoldAmountProperty, value);
    }

    public Brush Input {
        get => (Brush)GetValue(InputProperty);
        set => SetValue(InputProperty, value);
    }

    public static readonly DependencyProperty FoldAmountProperty = DependencyProperty.Register(
                    "FoldAmount",
        typeof(double),
        typeof(FoldEffect),
        new UIPropertyMetadata(0.0D, PixelShaderConstantCallback(0)));

    public static readonly DependencyProperty InputProperty =
        RegisterPixelShaderSamplerProperty("Input", typeof(FoldEffect), 0);
}