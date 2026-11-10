
namespace DM_RMS.Wpf.Behaviors;

public static class DataGridRowDoubleClickBehavior
{
    public static readonly System.Windows.DependencyProperty CommandProperty = System.Windows.DependencyProperty.RegisterAttached(
        "Command", typeof(System.Windows.Input.ICommand), typeof(DataGridRowDoubleClickBehavior), new System.Windows.PropertyMetadata(null, OnCommandChanged));

    public static System.Windows.Input.ICommand? GetCommand(System.Windows.DependencyObject obj) => (System.Windows.Input.ICommand?)obj.GetValue(CommandProperty);
    public static void SetCommand(System.Windows.DependencyObject obj, System.Windows.Input.ICommand? value) => obj.SetValue(CommandProperty, value);

    private static void OnCommandChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (d is not System.Windows.Controls.DataGrid grid) return;
        grid.MouseDoubleClick -= OnMouseDoubleClick;
        if (e.NewValue != null) grid.MouseDoubleClick += OnMouseDoubleClick;
    }

    private static void OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid || grid.SelectedItem == null) return;
        var command = GetCommand(grid);
        if (command?.CanExecute(grid.SelectedItem) == true)
            command.Execute(grid.SelectedItem);
    }
}
