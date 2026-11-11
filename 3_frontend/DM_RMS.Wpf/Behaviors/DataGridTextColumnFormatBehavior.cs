namespace DM_RMS.Wpf.Behaviors;

public static class DataGridTextColumnFormatBehavior
{
    public static readonly System.Windows.DependencyProperty CenterHeadersProperty = System.Windows.DependencyProperty.RegisterAttached(
        "CenterHeaders", typeof(bool), typeof(DataGridTextColumnFormatBehavior), new System.Windows.PropertyMetadata(false, OnChanged));

    public static bool GetCenterHeaders(System.Windows.DependencyObject obj) => (bool)obj.GetValue(CenterHeadersProperty);
    public static void SetCenterHeaders(System.Windows.DependencyObject obj, bool value) => obj.SetValue(CenterHeadersProperty, value);

    private static void OnChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (d is not System.Windows.Controls.DataGrid grid) return;
        grid.AutoGeneratingColumn -= OnAutoGeneratingColumn;
        if ((bool)e.NewValue) grid.AutoGeneratingColumn += OnAutoGeneratingColumn;
    }

    private static void OnAutoGeneratingColumn(object? sender, System.Windows.Controls.DataGridAutoGeneratingColumnEventArgs e)
    {
        e.Column.Header = e.PropertyName switch
        {
            "is_selected" => "선택",
            "model" => "모델",
            "equip" => "장비",
            "device" => "Device",
            "tech_prod" => "Tech Prod",
            "tech" => "Tech",
            "prod_group" => "Prod Group",
            "mode" => "Mode",
            "recipe" => "Recipe",
            "recipe_count" => "Recipe 수",
            "matching_recipe_count" => "Matching Recipe 수",
            "non_matching_recipe_count" => "Non-Matching Recipe 수",
            "exclude_recipe_count" => "제외 Recipe 수",
            "no_master_recipe_count" => "No Master Recipe 수",
            "matching_count" => "Matching 수",
            "non_matching_count" => "Non Matching 수",
            "new_recipe_count" => "신규 Recipe 수",
            "update_recipe_count" => "수정 Recipe 수",
            "remove_recipe_count" => "삭제 Recipe 수",
            "matching_rate" => "Matching Rate",
            "no_master" => "Master X 수",
            "exclude_matching" => "집계 제외",
            "aggregate_time" => "집계 시간",
            "aggregation_time" => "집계 시간",
            "last_modified_time" => "최종 수정 시간",
            "parameter_name" => "Parameter",
            "recipe_value" => "설정값",
            "master_value" => "Master 설정값",
            "matching" => "Matching",
            "para_nm" => "Parameter",
            "para_value" => "설정값",
            "master_key" => "Master Key",
            "exclude_code" => "집계 제외 코드",
            "exclude_count" => "제외 건수",
            "parameter_count" => "불일치 건수",
            "description" => "Description",
            _ => e.PropertyName
        };

        if (e.Column is not System.Windows.Controls.DataGridTextColumn textColumn) return;

        string? format = e.PropertyName switch
        {
            "matching_rate" => "{0:P2}",
            "recipe_count" or "matching_recipe_count" or "non_matching_recipe_count" or
            "exclude_recipe_count" or "no_master_recipe_count" or "matching_count" or
            "non_matching_count" or "new_recipe_count" or "update_recipe_count" or
            "remove_recipe_count" or "exclude_count" or "parameter_count" => "{0:N0}",
            _ => null
        };

        if (format is not null && textColumn.Binding is System.Windows.Data.Binding binding)
        {
            binding.StringFormat = format;
        }

        if (e.PropertyName is "recipe_count" or "matching_recipe_count" or
            "non_matching_recipe_count" or "exclude_recipe_count" or
            "no_master_recipe_count" or "matching_rate" or "new_recipe_count" or "update_recipe_count" or
            "remove_recipe_count" or "exclude_count" or "parameter_count")
        {
            var style = new System.Windows.Style(typeof(System.Windows.Controls.TextBlock));
            style.Setters.Add(new System.Windows.Setter(System.Windows.Controls.TextBlock.TextAlignmentProperty, System.Windows.TextAlignment.Right));
            style.Setters.Add(new System.Windows.Setter(System.Windows.FrameworkElement.MarginProperty, new System.Windows.Thickness(4, 0, 8, 0)));
            textColumn.ElementStyle = style;
        }
    }
}
