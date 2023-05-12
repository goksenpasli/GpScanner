using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.Win32;

namespace TwainControl
{
    public class Policy : DependencyObject
    {
        public static readonly DependencyProperty PolicyEnabledProperty = DependencyProperty.RegisterAttached("PolicyEnabled", typeof(bool), typeof(Policy), new PropertyMetadata(false, Changed));

        public static readonly DependencyProperty PolicyNameProperty = DependencyProperty.RegisterAttached("PolicyName", typeof(string), typeof(Policy), new PropertyMetadata(string.Empty));

        public static bool GetPolicyEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(PolicyEnabledProperty);
        }

        public static string GetPolicyName(DependencyObject obj)
        {
            return (string)obj.GetValue(PolicyNameProperty);
        }

        public static void SetPolicyEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(PolicyEnabledProperty, value);
        }

        public static void SetPolicyName(DependencyObject obj, string value)
        {
            obj.SetValue(PolicyNameProperty, value);
        }

        private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Button button && (bool)e.NewValue)
            {
                button.IsEnabled = CheckPolicy(GetPolicyName(button));
            }
            if (d is Hyperlink hyperlink && (bool)e.NewValue)
            {
                hyperlink.IsEnabled = CheckPolicy(GetPolicyName(hyperlink));
            }
        }

        private static bool CheckPolicy(string searchvalue)
        {
            try
            {
                using RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\GpScanner");
                if (key is not null)
                {
                    foreach (string value in key.GetValueNames())
                    {
                        if (value == searchvalue)
                        {
                            return (int)key.GetValue(value) != 0;
                        }
                    }
                }
                return true;
            }
            catch (Exception)
            {
            }

            return true;
        }
    }
}