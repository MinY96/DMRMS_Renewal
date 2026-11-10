using DM_RMS.Wpf.Models;

namespace DM_RMS.Wpf.Converters;

public sealed class MasterEditTemplateSelector : System.Windows.Controls.DataTemplateSelector
{
    public System.Windows.DataTemplate? StringTemplate { get; set; }
    public System.Windows.DataTemplate? IntegerNumberTemplate { get; set; }
    public System.Windows.DataTemplate? DoubleNumberTemplate { get; set; }
    public System.Windows.DataTemplate? IntegerRangeTemplate { get; set; }
    public System.Windows.DataTemplate? DoubleRangeTemplate { get; set; }

    // 기존 XAML과의 호환용 fallback
    public System.Windows.DataTemplate? NumberTemplate { get; set; }
    public System.Windows.DataTemplate? RangeTemplate { get; set; }

    public override System.Windows.DataTemplate? SelectTemplate(object item, System.Windows.DependencyObject container)
    {
        if (item is not MasterParameterItem parameter)
            return base.SelectTemplate(item, container);

        return parameter.ControlType.ToLowerInvariant() switch
        {
            "string" => StringTemplate,
            "integer" => IntegerNumberTemplate ?? NumberTemplate,
            "double" => DoubleNumberTemplate ?? NumberTemplate,
            "range_integer" => IntegerRangeTemplate ?? RangeTemplate,
            "range_double" => DoubleRangeTemplate ?? RangeTemplate,
            _ => NumberTemplate
        };
    }
}
