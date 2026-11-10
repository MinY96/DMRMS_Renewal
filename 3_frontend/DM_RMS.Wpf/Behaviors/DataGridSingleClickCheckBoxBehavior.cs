
namespace DM_RMS.Wpf.Behaviors;

public static class DataGridSingleClickCheckBoxBehavior
{
    public static readonly System.Windows.DependencyProperty EnableProperty =
        System.Windows.DependencyProperty.RegisterAttached(
            "Enable",
            typeof(bool),
            typeof(DataGridSingleClickCheckBoxBehavior),
            new System.Windows.PropertyMetadata(false, OnEnableChanged));

    public static bool GetEnable(System.Windows.DependencyObject obj) => (bool)obj.GetValue(EnableProperty);
    public static void SetEnable(System.Windows.DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);

    private static void OnEnableChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (d is not System.Windows.Controls.DataGrid dataGrid) return;

        dataGrid.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        if ((bool)e.NewValue)
            dataGrid.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var checkBox = FindAncestor<System.Windows.Controls.CheckBox>(e.OriginalSource as System.Windows.DependencyObject);
        if (checkBox == null) return;

        if (!checkBox.IsKeyboardFocusWithin)
        {
            checkBox.Focus();
            checkBox.IsChecked = !(checkBox.IsChecked ?? false);
            e.Handled = true;
        }
    }

    private static T? FindAncestor<T>(System.Windows.DependencyObject? current) where T : System.Windows.DependencyObject
    {
        while (current != null)
        {
            if (current is T target) return target;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
