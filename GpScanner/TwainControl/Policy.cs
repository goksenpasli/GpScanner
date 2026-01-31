using Microsoft.Win32;
using System.ComponentModel;
using System.Windows;
using System.Windows.Documents;

namespace TwainControl;

public class Policy : DependencyObject
{
    public static readonly DependencyProperty PolicyNameProperty = DependencyProperty.RegisterAttached("PolicyName", typeof(string), typeof(Policy), new PropertyMetadata(string.Empty, Changed));
    public static readonly DependencyProperty PolicyVisibilityNameProperty = DependencyProperty.RegisterAttached("PolicyVisibilityName", typeof(string), typeof(Policy), new PropertyMetadata(string.Empty, VisibilityChanged));

    public static bool AnyPolicyExsist()
    {
        try
        {
            using RegistryKey localMachineKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\GpScanner");
            if (HasDisabledValue(localMachineKey))
            {
                return true;
            }

            using RegistryKey currentUserKey = Registry.CurrentUser.OpenSubKey(@"Software\Policies\GpScanner");
            if (HasDisabledValue(currentUserKey))
            {
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

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
        catch
        {
        }
        return true;
    }

    public static string GetPolicyName(DependencyObject obj) => (string)obj.GetValue(PolicyNameProperty);

    public static string GetPolicyVisibilityName(DependencyObject obj) => (string)obj.GetValue(PolicyVisibilityNameProperty);

    public static void SetPolicyName(DependencyObject obj, string value) => obj.SetValue(PolicyNameProperty, value);

    public static void SetPolicyVisibilityName(DependencyObject obj, string value) => obj.SetValue(PolicyVisibilityNameProperty, value);

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (DesignerProperties.GetIsInDesignMode(d))
        {
            return;
        }

        if (d is UIElement uIElement && uIElement.IsEnabled)
        {
            uIElement.IsEnabled = CheckPolicy((string)e.NewValue);
        }

        if (d is Hyperlink hyperlink && hyperlink.IsEnabled)
        {
            hyperlink.IsEnabled = CheckPolicy((string)e.NewValue);
        }
    }

    private static bool HasDisabledValue(RegistryKey key)
    {
        if (key is null)
        {
            return false;
        }

        foreach (string valueName in key.GetValueNames())
        {
            object value = key.GetValue(valueName);
            if (value is int intValue && intValue == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void VisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (DesignerProperties.GetIsInDesignMode(d))
        {
            return;
        }

        if (d is UIElement uIElement && uIElement.Visibility == Visibility.Visible)
        {
            uIElement.Visibility = CheckPolicy((string)e.NewValue) ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}