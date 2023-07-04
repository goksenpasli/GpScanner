using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Extensions;

public class PasswordBoxHelper
{
    public static string GetPassword(DependencyObject obj)
    {
        return (string)obj.GetValue(PasswordProperty);
    }

    public static void SetPassword(DependencyObject obj, string value)
    {
        obj.SetValue(PasswordProperty, value);
    }

    public static readonly DependencyProperty PasswordProperty =
                DependencyProperty.RegisterAttached("Password", typeof(string), typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(string.Empty, HandleBoundPasswordChanged)
            {
                BindsTwoWayByDefault = true,
                DefaultUpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            });

    private static void HandleBoundPasswordChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
    {
        if (dp is not PasswordBox passwordBox)
        {
            return;
        }

        if ((bool)passwordBox.GetValue(SettingPasswordProperty))
        {
            return;
        }

        if (!(bool)passwordBox.GetValue(PasswordInitializedProperty))
        {
            passwordBox.SetValue(PasswordInitializedProperty, true);
            passwordBox.PasswordChanged += HandlePasswordChanged;
        }

        passwordBox.Password = e.NewValue as string;
    }

    private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
    {
        PasswordBox passwordBox = (PasswordBox)sender;
        passwordBox.SetValue(SettingPasswordProperty, true);
        SetPassword(passwordBox, passwordBox.Password);
        passwordBox.SetValue(SettingPasswordProperty, false);
    }

    private static readonly DependencyProperty PasswordInitializedProperty =
                DependencyProperty.RegisterAttached("PasswordInitialized", typeof(bool), typeof(PasswordBoxHelper),
            new PropertyMetadata(false));

    private static readonly DependencyProperty SettingPasswordProperty =
        DependencyProperty.RegisterAttached("SettingPassword", typeof(bool), typeof(PasswordBoxHelper),
            new PropertyMetadata(false));
}