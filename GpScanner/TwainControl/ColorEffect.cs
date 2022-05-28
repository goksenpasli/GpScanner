﻿using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace TwainControl
{
    public class ColorEffect : ShaderEffect
    {
        public static readonly DependencyProperty BlueProperty = DependencyProperty.Register("Blue", typeof(double), typeof(ColorEffect), new UIPropertyMetadata(0D, PixelShaderConstantCallback(4)));

        public static readonly DependencyProperty BrightnessProperty = DependencyProperty.Register("Brightness", typeof(double), typeof(ColorEffect), new UIPropertyMetadata(0D, PixelShaderConstantCallback(0)));

        public static readonly DependencyProperty ContrastProperty = DependencyProperty.Register("Contrast", typeof(double), typeof(ColorEffect), new UIPropertyMetadata(0D, PixelShaderConstantCallback(1)));

        public static readonly DependencyProperty GreenProperty = DependencyProperty.Register("Green", typeof(double), typeof(ColorEffect), new UIPropertyMetadata(0D, PixelShaderConstantCallback(3)));

        public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(ColorEffect), 0);

        public static readonly DependencyProperty RedProperty = DependencyProperty.Register("Red", typeof(double), typeof(ColorEffect), new UIPropertyMetadata(0D, PixelShaderConstantCallback(2)));

        public ColorEffect()
        {
            PixelShader = (new()
            {
                UriSource = new Uri("/TwainControl;component/ColorEffect.ps", UriKind.RelativeOrAbsolute)
            });

            UpdateShaderValue(InputProperty);
            UpdateShaderValue(BrightnessProperty);
            UpdateShaderValue(ContrastProperty);
            UpdateShaderValue(RedProperty);
            UpdateShaderValue(GreenProperty);
            UpdateShaderValue(BlueProperty);
        }

        public double Blue
        {
            get => (double)GetValue(BlueProperty);
            set => SetValue(BlueProperty, value);
        }

        public double Brightness
        {
            get => (double)GetValue(BrightnessProperty);
            set => SetValue(BrightnessProperty, value);
        }

        public double Contrast
        {
            get => (double)GetValue(ContrastProperty);
            set => SetValue(ContrastProperty, value);
        }

        public double Green
        {
            get => (double)GetValue(GreenProperty);
            set => SetValue(GreenProperty, value);
        }

        public Brush Input
        {
            get => (Brush)GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }

        public double Red
        {
            get => (double)GetValue(RedProperty);
            set => SetValue(RedProperty, value);
        }
    }
}