using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

//sampler2D inputSampler : register(s0);

//float FoldAmount : register(c0);
//float4 transform(float2 uv : TEXCOORD) : COLOR
//{
//float right = 1 - FoldAmount;
//    float2 tuv = float2((uv.x - FoldAmount) / (right - FoldAmount), uv.y);

//    float tx = tuv.x;
//    if (tx > 0.5)
//    {
//        tx = 1 - tx;
//    }
//    float top = FoldAmount * tx;
//    float bottom = 1 - top;
//    if (uv.y >= top && uv.y <= bottom)
//    {
//        float ty = lerp(0, 1, (tuv.y - top) / (bottom - top));

//        return tex2D(inputSampler, float2(tuv.x, ty));
//}
//return 0;
//}
//float4 main(float2 uv : TEXCOORD) : COLOR
//{
//    float right = 1 - FoldAmount;
//    if (uv.x > FoldAmount && uv.x < right)
//    {
//        return transform(uv);
//    }

//    return 0;
//}

namespace GpScanner.ViewModel
{
    public class FoldEffect : ShaderEffect
    {
        public static readonly DependencyProperty FoldAmountProperty = DependencyProperty.Register("FoldAmount", typeof(double), typeof(FoldEffect), new UIPropertyMetadata(0.0D, PixelShaderConstantCallback(0)));

        public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(FoldEffect), 0);

        public FoldEffect()
        {
            PixelShader = new PixelShader
            {
                UriSource = new Uri("/GpScanner;component/Resources/FoldEffect.ps", UriKind.Relative)
            };

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
    }
}