using System.Text.Json;
using System.Text.Json.Serialization;

namespace DM_RMS.Wpf.Models;

public sealed class CusomDetailTableResponse
{
    [JsonPropertyName("table")]
    public DataFrameResponse Table { get; set; } = new();

    [JsonPropertyName("column_metadata")]
    public Dictionary<string, ColumnVisualizationInfo> ColumnMetadata { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ColumnVisualizationInfo
{
    [JsonPropertyName("tree_role")]
    public string? TreeRole { get; set; }
    [JsonPropertyName("header")]
    public string Header { get; set; } = string.Empty;

    [JsonPropertyName("filter")]
    public ColumnFilterInfo Filter { get; set; } = new();

    [JsonPropertyName("header_color")]
    public string HeaderColor { get; set; } = "#EEF2F7";

    [JsonPropertyName("format")]
    public ColumnFormatInfo Format { get; set; } = new();

    [JsonPropertyName("rules")]
    public List<ColumnStyleRule> Rules { get; set; } = new();
}

public sealed class ColumnFilterInfo
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "like";

    [JsonPropertyName("options")]
    public List<string> Options { get; set; } = new();
}

public sealed class ColumnFormatInfo
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("decimal_places")]
    public int DecimalPlaces { get; set; }

    [JsonPropertyName("thousands_separator")]
    public bool ThousandsSeparator { get; set; }

    [JsonPropertyName("multiply")]
    public double Multiply { get; set; } = 1d;

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }
}

public sealed class ColumnStyleRule
{
    [JsonPropertyName("condition_column")]
    public string? ConditionColumn { get; set; }
    [JsonPropertyName("operator")]
    public string Operator { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("style")]
    public CellStyleInfo Style { get; set; } = new();
}

public sealed class CellStyleInfo
{
    [JsonPropertyName("foreground")]
    public string? Foreground { get; set; }

    [JsonPropertyName("background")]
    public string? Background { get; set; }

    [JsonPropertyName("font_weight")]
    public string? FontWeight { get; set; }
}
