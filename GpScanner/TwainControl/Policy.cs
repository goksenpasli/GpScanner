﻿using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Documents;

namespace TwainControl;

public class Policy : DependencyObject
{
    public static readonly DependencyProperty PolicyEnabledProperty =
                                    DependencyProperty.RegisterAttached("PolicyEnabled", typeof(bool), typeof(Policy), new PropertyMetadata(false, Changed));
    public static readonly DependencyProperty PolicyNameProperty =
        DependencyProperty.RegisterAttached("PolicyName", typeof(string), typeof(Policy), new PropertyMetadata(string.Empty));

    public static bool CheckKeyPolicy(string searchvalue, RegistryKey registryKey)
    {
        try
        {
            using RegistryKey key = registryKey;
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

    public static bool CheckPolicy(DependencyObject dependencyObject)
    {
        return CheckKeyPolicy(GetPolicyName(dependencyObject), Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\GpScanner")) &&
            CheckKeyPolicy(GetPolicyName(dependencyObject), Registry.CurrentUser.OpenSubKey(@"Software\Policies\GpScanner"));
    }
    public static bool CheckPolicy(string policyname)
    { return CheckKeyPolicy(policyname, Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\GpScanner")) && CheckKeyPolicy(policyname, Registry.CurrentUser.OpenSubKey(@"Software\Policies\GpScanner")); }
    public static bool GetPolicyEnabled(DependencyObject obj) => (bool)obj.GetValue(PolicyEnabledProperty);

    public static string GetPolicyName(DependencyObject obj) => (string)obj.GetValue(PolicyNameProperty);

    public static void SetPolicyEnabled(DependencyObject obj, bool value) => obj.SetValue(PolicyEnabledProperty, value);

    public static void SetPolicyName(DependencyObject obj, string value) => obj.SetValue(PolicyNameProperty, value);

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (DesignerProperties.GetIsInDesignMode(d))
        {
            return;
        }

        if (d is UIElement uIElement && (bool)e.NewValue)
        {
            uIElement.IsEnabled = CheckPolicy(uIElement);
        }

        if (d is Hyperlink hyperlink && (bool)e.NewValue)
        {
            hyperlink.IsEnabled = CheckPolicy(hyperlink);
        }
    }
}