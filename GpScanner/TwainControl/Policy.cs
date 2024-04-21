using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Documents;

namespace TwainControl;

public class Policy : DependencyObject
{
    public static readonly DependencyProperty PolicyNameProperty = DependencyProperty.RegisterAttached("PolicyName", typeof(string), typeof(Policy), new PropertyMetadata(string.Empty, Changed));

    public static bool CheckKeyPolicy(string searchvalue, RegistryKey registryKey)
    {
        using RegistryKey key = registryKey;
        if (key is not null)
        {
            foreach (string value in key.GetValueNames())
            {
                if (value == searchvalue && key.GetValue(value) is int dwordvalue)
                {
                    return dwordvalue != 0;
                }
            }
        }
        return true;
    }

    public static bool CheckPolicy(string policyname)
    {
        try
        {
            using RegistryKey localMachineKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\GpScanner");
            using RegistryKey currentUserKey = Registry.CurrentUser.OpenSubKey(@"Software\Policies\GpScanner");
            return CheckKeyPolicy(policyname, localMachineKey) && CheckKeyPolicy(policyname, currentUserKey);
        }
        catch (Exception)
        {
        }
        return true;
    }

    public static string GetPolicyName(DependencyObject obj) => (string)obj.GetValue(PolicyNameProperty);

    public static void SetPolicyName(DependencyObject obj, string value) => obj.SetValue(PolicyNameProperty, value);

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (DesignerProperties.GetIsInDesignMode(d))
        {
            return;
        }

        if (d is UIElement uIElement)
        {
            uIElement.IsEnabled = CheckPolicy((string)e.NewValue);
        }

        if (d is Hyperlink hyperlink)
        {
            hyperlink.IsEnabled = CheckPolicy((string)e.NewValue);
        }
    }
}