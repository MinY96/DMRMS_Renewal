using DM_RMS.Wpf.Converters;
using DM_RMS.Wpf.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Binding = System.Windows.Data.Binding;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Control = System.Windows.Controls.Control;
using SystemColors = System.Windows.SystemColors;

namespace DM_RMS.Wpf.Behaviors;

public static class DynamicDataGridVisualizationBehavior
{
    public static readonly DependencyProperty ColumnMetadataProperty =
        DependencyProperty.RegisterAttached(
            "ColumnMetadata",
            typeof(Dictionary<string, ColumnVisualizationInfo>),
            typeof(DynamicDataGridVisualizationBehavior),
            new PropertyMetadata(null, OnMetadataChanged));

    public static void SetColumnMetadata(DependencyObject element, Dictionary<string, ColumnVisualizationInfo>? value)
        => element.SetValue(ColumnMetadataProperty, value);

    public static Dictionary<string, ColumnVisualizationInfo>? GetColumnMetadata(DependencyObject element)
        => element.GetValue(ColumnMetadataProperty) as Dictionary<string, ColumnVisualizationInfo>;

    private static void OnMetadataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;
        grid.AutoGeneratingColumn -= OnAutoGeneratingColumn;
        grid.AutoGeneratingColumn += OnAutoGeneratingColumn;
        grid.Dispatcher.BeginInvoke(new Action(() => ApplyExistingColumns(grid)));
    }

    private static void OnAutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (sender is DataGrid grid) ConfigureColumn(grid, e.Column, e.PropertyName);
    }

    private static void ApplyExistingColumns(DataGrid grid)
    {
        foreach (var column in grid.Columns)
        {
            var name = column.SortMemberPath;
            if (string.IsNullOrWhiteSpace(name) && column is DataGridBoundColumn bound && bound.Binding is Binding binding)
                name = binding.Path?.Path;
            if (!string.IsNullOrWhiteSpace(name)) ConfigureColumn(grid, column, name);
        }
    }

    private static void ConfigureColumn(DataGrid grid, DataGridColumn column, string columnName)
    {
        var metadata = GetColumnMetadata(grid);
        if (metadata is null || !metadata.TryGetValue(columnName, out var info)) return;

        column.SortMemberPath = columnName;
        if (columnName.StartsWith("_", StringComparison.Ordinal))
        {
            column.Visibility = Visibility.Collapsed;
            return;
        }
        column.Header = CreateHeader(info.Header, info.HeaderColor);

        if (column is not DataGridTextColumn textColumn) return;

        textColumn.Binding = new Binding(".")
        {
            Converter = new DynamicCellValueConverter { ColumnName = columnName, Metadata = info }
        };

        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(7, 3, 7, 3)));
        style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new Binding(".")
        {
            Converter = new DynamicCellStyleConverter { PropertyName = "Foreground", CurrentColumnName = columnName, Rules = info.Rules },
            FallbackValue = SystemColors.ControlTextBrush,
            TargetNullValue = SystemColors.ControlTextBrush
        }));
        style.Setters.Add(new Setter(TextBlock.FontWeightProperty, new Binding(".")
        {
            Converter = new DynamicCellStyleConverter { PropertyName = "FontWeight", CurrentColumnName = columnName, Rules = info.Rules },
            FallbackValue = FontWeights.Normal,
            TargetNullValue = FontWeights.Normal
        }));
        textColumn.ElementStyle = style;

        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(Control.BackgroundProperty, new Binding(".")
        {
            Converter = new DynamicCellStyleConverter { PropertyName = "Background", CurrentColumnName = columnName, Rules = info.Rules },
            FallbackValue = Brushes.Transparent,
            TargetNullValue = Brushes.Transparent
        }));
        column.CellStyle = cellStyle;
    }

    private static FrameworkElement CreateHeader(string text, string color)
    {
        Brush background;
        try { background = new BrushConverter().ConvertFromString(color) as Brush ?? Brushes.Transparent; }
        catch { background = Brushes.Transparent; }

        return new Border
        {
            Background = background,
            Padding = new Thickness(7, 4, 7, 4),
            Child = new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis }
        };
    }
}
