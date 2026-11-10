using System.Collections.ObjectModel;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DM_RMS.Wpf.Models;

public sealed class DataFrameResponse
{
    [JsonPropertyName("columns")]
    public List<DataFrameColumn> Columns { get; set; } = new();

    [JsonPropertyName("rows")]
    public List<Dictionary<string, JsonElement>> Rows { get; set; } = new();

    [JsonPropertyName("row_count")]
    public int RowCount { get; set; }

    public DataTable ToDataTable()
    {
        var table = new DataTable();
        foreach (var col in Columns)
            table.Columns.Add(col.Name, ResolveType(col.Type));

        foreach (var row in Rows)
        {
            var dr = table.NewRow();
            foreach (var col in Columns)
            {
                if (row.TryGetValue(col.Name, out var value))
                    dr[col.Name] = ConvertJsonElement(value, ResolveType(col.Type)) ?? DBNull.Value;
            }
            table.Rows.Add(dr);
        }
        return table;
    }

    private static Type ResolveType(string type) => type switch
    {
        "bool" => typeof(bool),
        "int" => typeof(int),
        "float" => typeof(double),
        _ => typeof(string)
    };

    private static object? ConvertJsonElement(JsonElement element, Type targetType)
    {
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (targetType == typeof(bool)) return element.GetBoolean();
        if (targetType == typeof(int)) return element.TryGetInt32(out var i) ? i : null;
        if (targetType == typeof(double)) return element.TryGetDouble(out var d) ? d : null;
        return element.ToString();
    }
}

public sealed class DataFrameColumn
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "str";
}
