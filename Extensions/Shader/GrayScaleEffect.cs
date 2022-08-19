using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Extensions
{
    /// <summary>An effect that turns the input into gray scale shades.</summary>
    public class GrayScaleEffect : ShaderEffect
    {
        public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(GrayScaleEffect), 0);

        public GrayScaleEffect()
        {
            PixelShader = (new()
            {
                UriSource = new Uri("/Extensions;component/Shader/GrayScaleEffect.ps", UriKind.Relative)
            });

            UpdateShaderValue(InputProperty);
        }

        public Brush Input
        {
            get => (Brush)GetValue(InputProperty);

            set => SetValue(InputProperty, value);
        }
    }
}