using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Control = System.Windows.Controls.Control;

namespace DM_RMS.Wpf.Behaviors;

/// <summary>
/// Recipe 목록의 상태 우선순위에 따라 행을 표시합니다.
/// Matching=False는 오류색, Matching이 비어 있고 no_master=True이면 초록색을 적용합니다.
/// </summary>
public static class RecipeRowHighlightBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(RecipeRowHighlightBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly Brush NonMatchingBackground =
        new SolidColorBrush(Color.FromRgb(255, 235, 238));
    private static readonly Brush NonMatchingForeground =
        new SolidColorBrush(Color.FromRgb(156, 0, 6));
    private static readonly Brush NoMasterBackground =
        new SolidColorBrush(Color.FromRgb(226, 239, 218));
    private static readonly Brush NoMasterForeground =
        new SolidColorBrush(Color.FromRgb(0, 97, 0));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;

        grid.LoadingRow -= OnLoadingRow;
        grid.UnloadingRow -= OnUnloadingRow;

        if ((bool)e.NewValue)
        {
            grid.LoadingRow += OnLoadingRow;
            grid.UnloadingRow += OnUnloadingRow;
            grid.Items.Refresh();
        }
    }

    private static void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        ResetRow(e.Row);
        if (e.Row.Item is not DataRowView rowView) return;

        var matching = GetValue(rowView, "matching");
        if (ValueEquals(matching, "False"))
        {
            e.Row.Background = NonMatchingBackground;
            e.Row.Foreground = NonMatchingForeground;
            return;
        }

        var noMaster = GetValue(rowView, "no_master");
        if (IsEmpty(matching) && IsTrue(noMaster))
        {
            e.Row.Background = NoMasterBackground;
            e.Row.Foreground = NoMasterForeground;
        }
    }

    private static void OnUnloadingRow(object? sender, DataGridRowEventArgs e) => ResetRow(e.Row);

    private static object? GetValue(DataRowView rowView, string columnName)
        => rowView.Row.Table.Columns.Contains(columnName) ? rowView[columnName] : null;

    private static bool IsEmpty(object? value)
        => value is null or DBNull || string.IsNullOrWhiteSpace(Convert.ToString(value, CultureInfo.InvariantCulture));

    private static bool IsTrue(object? value)
    {
        if (value is bool boolValue) return boolValue;
        var text = value is null or DBNull
            ? string.Empty
            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        return text.Equals("true", StringComparison.OrdinalIgnoreCase)
               || text.Equals("1", StringComparison.OrdinalIgnoreCase)
               || text.Equals("y", StringComparison.OrdinalIgnoreCase)
               || text.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ValueEquals(object? value, string expected)
    {
        var actual = value is null or DBNull
            ? string.Empty
            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        return actual.Trim().Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static void ResetRow(DataGridRow row)
    {
        row.ClearValue(Control.BackgroundProperty);
        row.ClearValue(Control.ForegroundProperty);
    }
}
