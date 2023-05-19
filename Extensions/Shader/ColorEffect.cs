using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

//float Brightness : register(C0);
//float Contrast : register(C1);
//float Red: register(C2);
//float Green: register(C3);
//float Blue: register(C4);

//sampler2D Texture1Sampler : register(S0);
//float4 main(float2 uv : TEXCOORD) : COLOR
//{
//    float4 pixelColor = tex2D(Texture1Sampler, uv);
//    pixelColor.rgb /= pixelColor.a;
//    pixelColor.rgb = ((pixelColor.rgb - 0.5f) * max(Contrast + 1, 0)) + 0.5f;
//    pixelColor.rgb += Brightness;
//    pixelColor.rgb *= pixelColor.a;

//    pixelColor.r += Red;
//    pixelColor.g += Green;
//    pixelColor.b += Blue;
//    return pixelColor;
//}

namespace Extensions;

public class ColorEffect : ShaderEffect
{
    public static readonly DependencyProperty BlueProperty = DependencyProperty.Register("Blue", typeof(double),
        typeof(ColorEffect), new UIPropertyMetadata(0D, PixelShaderConstantCallback(4)));

    public static readonly DependencyProperty BrightnessProperty = DependencyProperty.Register("Brightness",
        typeof(double), typeof(ColorEffect), new UIPropertyMetadata(0D, PixelShaderConstantCallback(0)));

    public static readonly DependencyProperty ContrastProperty = DependencyProperty.Register("Contrast", typeof(double),
        typeof(ColorEffect), new UIPropertyMetadata(0D, PixelShaderConstantCallback(1)));

    public static readonly DependencyProperty GreenProperty = DependencyProperty.Register("Green", typeof(double),
        typeof(ColorEffect), new UIPropertyMetadata(0D, PixelShaderConstantCallback(3)));

    public static readonly DependencyProperty InputProperty =
        RegisterPixelShaderSamplerProperty("Input", typeof(ColorEffect), 0);

    public static readonly DependencyProperty RedProperty = DependencyProperty.Register("Red", typeof(double),
        typeof(ColorEffect), new UIPropertyMetadata(0D, PixelShaderConstantCallback(2)));

    public ColorEffect()
    {
        PixelShader = new PixelShader
        {
            UriSource = new Uri("/Extensions;component/Shader/ColorEffect.ps", UriKind.Relative)
        };

        UpdateShaderValue(InputProperty);
        UpdateShaderValue(BrightnessProperty);
        UpdateShaderValue(ContrastProperty);
        UpdateShaderValue(RedProperty);
        UpdateShaderValue(GreenProperty);
        UpdateShaderValue(BlueProperty);
    }

    public double Blue {
        get => (double)GetValue(BlueProperty);
        set => SetValue(BlueProperty, value);
    }

    public double Brightness {
        get => (double)GetValue(BrightnessProperty);
        set => SetValue(BrightnessProperty, value);
    }

    public double Contrast {
        get => (double)GetValue(ContrastProperty);
        set => SetValue(ContrastProperty, value);
    }

    public double Green {
        get => (double)GetValue(GreenProperty);
        set => SetValue(GreenProperty, value);
    }

    public Brush Input {
        get => (Brush)GetValue(InputProperty);
        set => SetValue(InputProperty, value);
    }

    public double Red {
        get => (double)GetValue(RedProperty);
        set => SetValue(RedProperty, value);
    }
}