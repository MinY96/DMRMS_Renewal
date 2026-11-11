using DM_RMS.Wpf.Models;
using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using SystemColors = System.Windows.SystemColors;

namespace DM_RMS.Wpf.Converters;

public sealed class DynamicCellStyleConverter : IValueConverter
{
    public string PropertyName { get; init; } = string.Empty;
    public string CurrentColumnName { get; init; } = string.Empty;
    public IReadOnlyList<ColumnStyleRule> Rules { get; init; } = Array.Empty<ColumnStyleRule>();

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        Brush foreground = SystemColors.ControlTextBrush;
        Brush background = Brushes.Transparent;
        var fontWeight = FontWeights.Normal;

        foreach (var rule in Rules.OrderBy(x => x.Priority))
        {
            var conditionValue = ResolveValue(value, rule.ConditionColumn ?? CurrentColumnName);
            if (!Matches(conditionValue, rule)) continue;
            if (!string.IsNullOrWhiteSpace(rule.Style.Foreground)) foreground = ToBrush(rule.Style.Foreground) ?? foreground;
            if (!string.IsNullOrWhiteSpace(rule.Style.Background)) background = ToBrush(rule.Style.Background) ?? background;
            if (!string.IsNullOrWhiteSpace(rule.Style.FontWeight))
                fontWeight = rule.Style.FontWeight.Equals("bold", StringComparison.OrdinalIgnoreCase)
                    ? FontWeights.Bold : FontWeights.Normal;
        }

        return PropertyName switch
        {
            "Foreground" => foreground,
            "Background" => background,
            "FontWeight" => fontWeight,
            _ => null
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static object? ResolveValue(object row, string columnName)
    {
        if (row is DataRowView rowView && rowView.DataView.Table.Columns.Contains(columnName))
            return rowView[columnName];
        if (row is DataRow dataRow && dataRow.Table.Columns.Contains(columnName))
            return dataRow[columnName];
        return row;
    }

    private static bool Matches(object? rawValue, ColumnStyleRule rule)
    {
        var op = rule.Operator.Trim().ToLowerInvariant();
        if (op == "always") return true;
        if (op == "isnull") return rawValue is null || rawValue == DBNull.Value || string.IsNullOrEmpty(rawValue.ToString());
        if (op == "notnull") return rawValue is not null && rawValue != DBNull.Value && !string.IsNullOrEmpty(rawValue.ToString());

        var right = rule.Value.ValueKind == JsonValueKind.String ? rule.Value.GetString() ?? string.Empty : rule.Value.ToString();
        var left = rawValue?.ToString() ?? string.Empty;
        if (double.TryParse(left, NumberStyles.Any, CultureInfo.InvariantCulture, out var l) &&
            double.TryParse(right, NumberStyles.Any, CultureInfo.InvariantCulture, out var r))
            return op switch { "eq" => l == r, "ne" => l != r, "gt" => l > r, "gte" => l >= r, "lt" => l < r, "lte" => l <= r, _ => false };

        return op switch
        {
            "eq" => left.Equals(right, StringComparison.OrdinalIgnoreCase),
            "ne" => !left.Equals(right, StringComparison.OrdinalIgnoreCase),
            "contains" => left.Contains(right, StringComparison.OrdinalIgnoreCase),
            "startswith" => left.StartsWith(right, StringComparison.OrdinalIgnoreCase),
            "endswith" => left.EndsWith(right, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static Brush? ToBrush(string color)
    {
        try { return new BrushConverter().ConvertFromString(color) as Brush; }
        catch { return null; }
    }
}
