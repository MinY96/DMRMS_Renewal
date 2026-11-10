using System.Text.Json.Serialization;

namespace DM_RMS.Wpf.Models;

public sealed class DashboardSummaryResponse
{
    [JsonPropertyName("cards")] public List<DashboardCard> Cards { get; set; } = new();
    [JsonPropertyName("matching_rate")] public DataFrameResponse MatchingRate { get; set; } = new();
    [JsonPropertyName("update_history")] public DataFrameResponse UpdateHistory { get; set; } = new();
    [JsonPropertyName("equipment_status")] public DataFrameResponse EquipmentStatus { get; set; } = new();
    [JsonPropertyName("exclude_status")] public DataFrameResponse ExcludeStatus { get; set; } = new();
    [JsonPropertyName("non_matching_parameter")] public DataFrameResponse NonMatchingParameter { get; set; } = new();
}
public sealed class DashboardCard
{
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("value")] public string Value { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
}
public sealed class DashboardChartsResponse
{
    [JsonPropertyName("matching_rate_html")] public string MatchingRateHtml { get; set; } = string.Empty;
    [JsonPropertyName("update_history_html")] public string UpdateHistoryHtml { get; set; } = string.Empty;
    [JsonPropertyName("exclude_status_html")] public string ExcludeStatusHtml { get; set; } = string.Empty;
    [JsonPropertyName("non_matching_parameter_html")] public string NonMatchingParameterHtml { get; set; } = string.Empty;
}
