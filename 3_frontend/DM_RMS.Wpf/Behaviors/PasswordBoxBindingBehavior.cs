
using System.Windows;

namespace DM_RMS.Wpf.Behaviors;

public static class PasswordBoxBindingBehavior
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxBindingBehavior),
            new FrameworkPropertyMetadata(
                null, 
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBoundPasswordChanged));

    private static readonly System.Windows.DependencyProperty IsUpdatingProperty =
        System.Windows.DependencyProperty.RegisterAttached(
            "IsUpdating",
            typeof(bool),
            typeof(PasswordBoxBindingBehavior),
            new PropertyMetadata(false));

    public static string? GetBoundPassword(System.Windows.DependencyObject obj)
        => (string?)obj.GetValue(BoundPasswordProperty);

    public static void SetBoundPassword(System.Windows.DependencyObject obj, string? value)
        => obj.SetValue(BoundPasswordProperty, value);

    private static bool GetIsUpdating(System.Windows.DependencyObject obj)
        => (bool)obj.GetValue(IsUpdatingProperty);

    private static void SetIsUpdating(System.Windows.DependencyObject obj, bool value)
        => obj.SetValue(IsUpdatingProperty, value);

    private static void OnBoundPasswordChanged(
        System.Windows.DependencyObject dependencyObject,
        System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not System.Windows.Controls.PasswordBox passwordBox)
        {
            return;
        }

        passwordBox.PasswordChanged -= OnPasswordChanged;

        if (!GetIsUpdating(passwordBox))
        {
            string newPassword = e.NewValue as string ?? string.Empty;

            if (passwordBox.Password != newPassword)
            {
                passwordBox.Password = newPassword;
            }            
        }

        passwordBox.PasswordChanged += OnPasswordChanged;
    }

    private static void OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.PasswordBox passwordBox)
        {
            return;
        }

        try
        {
            SetIsUpdating(passwordBox, true);
            SetBoundPassword(passwordBox, passwordBox.Password);
        }
        finally
        {
            SetIsUpdating(passwordBox, false);
        }
    }
}
