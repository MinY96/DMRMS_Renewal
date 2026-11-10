using System.Data;
using System.Globalization;
using System.Windows.Data;
using DM_RMS.Wpf.Models;

namespace DM_RMS.Wpf.Converters;

public sealed class DynamicCellValueConverter : IValueConverter
{
    public string ColumnName { get; init; } = string.Empty;
    public ColumnVisualizationInfo Metadata { get; init; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        object? rawValue = value;
        var prefix = string.Empty;
        if (value is DataRowView row)
        {
            var rowType = row.DataView.Table.Columns.Contains("_row_type") ? row["_row_type"]?.ToString() : null;
            if (Metadata.TreeRole?.Equals("parent", StringComparison.OrdinalIgnoreCase) == true && rowType == "DETAIL") return string.Empty;
            if (Metadata.TreeRole?.Equals("detail", StringComparison.OrdinalIgnoreCase) == true && rowType == "HEADER") return string.Empty;
            if (rowType == "DETAIL" && ColumnName.Equals("para_nm", StringComparison.OrdinalIgnoreCase)) prefix = "    ↳ ";
            if (rowType == "HEADER" && ColumnName.Equals("master_key", StringComparison.OrdinalIgnoreCase)) prefix = "▼ ";
            rawValue = row.DataView.Table.Columns.Contains(ColumnName) ? row[ColumnName] : null;
        }

        if (rawValue is null || rawValue == DBNull.Value) return string.Empty;
        var format = Metadata.Format;
        var type = format.Type?.Trim().ToLowerInvariant();
        if (type == "uppercase") return prefix + (rawValue.ToString()?.ToUpperInvariant() ?? string.Empty);
        if (type == "lowercase") return prefix + (rawValue.ToString()?.ToLowerInvariant() ?? string.Empty);
        if (type is "number" or "percentage" && double.TryParse(rawValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
        {
            number *= format.Multiply;
            var numberFormat = format.ThousandsSeparator ? $"N{format.DecimalPlaces}" : $"F{format.DecimalPlaces}";
            var result = number.ToString(numberFormat, CultureInfo.CurrentCulture);
            return prefix + (type == "percentage" ? result + "%" : result);
        }
        if (type == "datetime" && DateTime.TryParse(rawValue.ToString(), out var dateTime))
            return prefix + dateTime.ToString(format.Pattern ?? "yyyy-MM-dd HH:mm:ss");
        return prefix + (rawValue.ToString() ?? string.Empty);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
