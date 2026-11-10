using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Control = System.Windows.Controls.Control;

namespace DM_RMS.Wpf.Behaviors;

/// <summary>
/// DataGrid의 특정 컬럼 값이 지정 값과 일치할 때 행 또는 셀을 하이라이트합니다.
/// bool, string, 숫자, DBNull/null 값을 문자열 기준으로 안전하게 비교할 수 있습니다.
/// </summary>
public static class DataGridConditionalHighlightBehavior
{
    public static readonly DependencyProperty ColumnNameProperty = DependencyProperty.RegisterAttached(
        "ColumnName", typeof(string), typeof(DataGridConditionalHighlightBehavior), new PropertyMetadata(null, OnRuleChanged));

    public static readonly DependencyProperty MatchValueProperty = DependencyProperty.RegisterAttached(
        "MatchValue", typeof(string), typeof(DataGridConditionalHighlightBehavior), new PropertyMetadata(null, OnRuleChanged));

    public static readonly DependencyProperty HighlightTargetProperty = DependencyProperty.RegisterAttached(
        "HighlightTarget", typeof(string), typeof(DataGridConditionalHighlightBehavior), new PropertyMetadata("Row", OnRuleChanged));

    public static readonly DependencyProperty HighlightBackgroundProperty = DependencyProperty.RegisterAttached(
        "HighlightBackground", typeof(Brush), typeof(DataGridConditionalHighlightBehavior),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(255, 235, 238)), OnRuleChanged));

    public static readonly DependencyProperty HighlightForegroundProperty = DependencyProperty.RegisterAttached(
        "HighlightForeground", typeof(Brush), typeof(DataGridConditionalHighlightBehavior), new PropertyMetadata(null, OnRuleChanged));

    public static readonly DependencyProperty IgnoreCaseProperty = DependencyProperty.RegisterAttached(
        "IgnoreCase", typeof(bool), typeof(DataGridConditionalHighlightBehavior), new PropertyMetadata(true, OnRuleChanged));

    // 기존 XAML과의 하위 호환용. 신규 코드는 ColumnName + MatchValue 사용을 권장합니다.
    public static readonly DependencyProperty BooleanColumnNameProperty = DependencyProperty.RegisterAttached(
        "BooleanColumnName", typeof(string), typeof(DataGridConditionalHighlightBehavior), new PropertyMetadata(null, OnLegacyRuleChanged));

    public static readonly DependencyProperty HighlightWhenFalseProperty = DependencyProperty.RegisterAttached(
        "HighlightWhenFalse", typeof(bool), typeof(DataGridConditionalHighlightBehavior), new PropertyMetadata(true, OnLegacyRuleChanged));

    private static readonly DependencyProperty IsAttachedProperty = DependencyProperty.RegisterAttached(
        "IsAttached", typeof(bool), typeof(DataGridConditionalHighlightBehavior), new PropertyMetadata(false));

    public static string? GetColumnName(DependencyObject obj) => (string?)obj.GetValue(ColumnNameProperty);
    public static void SetColumnName(DependencyObject obj, string? value) => obj.SetValue(ColumnNameProperty, value);
    public static string? GetMatchValue(DependencyObject obj) => (string?)obj.GetValue(MatchValueProperty);
    public static void SetMatchValue(DependencyObject obj, string? value) => obj.SetValue(MatchValueProperty, value);
    public static string GetHighlightTarget(DependencyObject obj) => (string)obj.GetValue(HighlightTargetProperty);
    public static void SetHighlightTarget(DependencyObject obj, string value) => obj.SetValue(HighlightTargetProperty, value);
    public static Brush? GetHighlightBackground(DependencyObject obj) => (Brush?)obj.GetValue(HighlightBackgroundProperty);
    public static void SetHighlightBackground(DependencyObject obj, Brush? value) => obj.SetValue(HighlightBackgroundProperty, value);
    public static Brush? GetHighlightForeground(DependencyObject obj) => (Brush?)obj.GetValue(HighlightForegroundProperty);
    public static void SetHighlightForeground(DependencyObject obj, Brush? value) => obj.SetValue(HighlightForegroundProperty, value);
    public static bool GetIgnoreCase(DependencyObject obj) => (bool)obj.GetValue(IgnoreCaseProperty);
    public static void SetIgnoreCase(DependencyObject obj, bool value) => obj.SetValue(IgnoreCaseProperty, value);
    public static string? GetBooleanColumnName(DependencyObject obj) => (string?)obj.GetValue(BooleanColumnNameProperty);
    public static void SetBooleanColumnName(DependencyObject obj, string? value) => obj.SetValue(BooleanColumnNameProperty, value);
    public static bool GetHighlightWhenFalse(DependencyObject obj) => (bool)obj.GetValue(HighlightWhenFalseProperty);
    public static void SetHighlightWhenFalse(DependencyObject obj, bool value) => obj.SetValue(HighlightWhenFalseProperty, value);

    private static void OnLegacyRuleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;
        var legacyColumn = GetBooleanColumnName(grid);
        if (!string.IsNullOrWhiteSpace(legacyColumn))
        {
            SetColumnName(grid, legacyColumn);
            SetMatchValue(grid, GetHighlightWhenFalse(grid) ? "False" : "True");
        }
        EnsureAttached(grid);
        grid.Items.Refresh();
    }

    private static void OnRuleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;
        EnsureAttached(grid);
        grid.Items.Refresh();
    }

    private static void EnsureAttached(DataGrid grid)
    {
        if ((bool)grid.GetValue(IsAttachedProperty)) return;

        grid.SetValue(IsAttachedProperty, true);
        grid.LoadingRow += OnLoadingRow;
        grid.UnloadingRow += OnUnloadingRow;
        grid.AutoGeneratingColumn += OnAutoGeneratingColumn;
    }

    private static void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (sender is not DataGrid grid) return;

        ResetRow(e.Row);
        if (!string.Equals(GetHighlightTarget(grid), "Row", StringComparison.OrdinalIgnoreCase)) return;
        if (!IsMatch(grid, e.Row.Item)) return;

        e.Row.Background = GetHighlightBackground(grid) ?? Brushes.Transparent;
        if (GetHighlightForeground(grid) is Brush foreground)
            e.Row.Foreground = foreground;
    }

    private static void OnUnloadingRow(object? sender, DataGridRowEventArgs e) => ResetRow(e.Row);

    private static void OnAutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        if (!string.Equals(GetHighlightTarget(grid), "Cell", StringComparison.OrdinalIgnoreCase)) return;
        if (!string.Equals(e.PropertyName, GetColumnName(grid), StringComparison.OrdinalIgnoreCase)) return;

        if (e.Column is not DataGridBoundColumn boundColumn) return;

        var style = new Style(typeof(TextBlock), boundColumn.ElementStyle);
        var trigger = new DataTrigger
        {
            Binding = new System.Windows.Data.Binding($"[{e.PropertyName}]")
            {
                Converter = new HighlightValueMatchConverter(GetMatchValue(grid), GetIgnoreCase(grid))
            },
            Value = true
        };

        if (GetHighlightBackground(grid) is Brush background)
            trigger.Setters.Add(new Setter(TextBlock.BackgroundProperty, background));
        if (GetHighlightForeground(grid) is Brush foreground)
            trigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, foreground));

        style.Triggers.Add(trigger);
        boundColumn.ElementStyle = style;
    }

    private static bool IsMatch(DataGrid grid, object item)
    {
        var columnName = GetColumnName(grid);
        if (string.IsNullOrWhiteSpace(columnName)) return false;

        object? rawValue = item switch
        {
            DataRowView drv when drv.Row.Table.Columns.Contains(columnName) => drv[columnName],
            _ => item.GetType().GetProperty(columnName)?.GetValue(item)
        };

        return ValueEquals(rawValue, GetMatchValue(grid), GetIgnoreCase(grid));
    }

    internal static bool ValueEquals(object? rawValue, string? expected, bool ignoreCase)
    {
        var actual = rawValue is null or DBNull ? string.Empty : Convert.ToString(rawValue, CultureInfo.InvariantCulture) ?? string.Empty;
        var target = expected ?? string.Empty;
        return string.Equals(actual.Trim(), target.Trim(), ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static void ResetRow(DataGridRow row)
    {
        row.ClearValue(Control.BackgroundProperty);
        row.ClearValue(Control.ForegroundProperty);
    }

    private sealed class HighlightValueMatchConverter : System.Windows.Data.IValueConverter
    {
        private readonly string? _expected;
        private readonly bool _ignoreCase;

        public HighlightValueMatchConverter(string? expected, bool ignoreCase)
        {
            _expected = expected;
            _ignoreCase = ignoreCase;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => ValueEquals(value, _expected, _ignoreCase);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }
}
