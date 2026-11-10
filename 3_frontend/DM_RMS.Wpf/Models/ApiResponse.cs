using System.Text.Json.Serialization;

namespace DM_RMS.Wpf.Models;

public sealed class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data_type")]
    public string DataType { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("errors")]
    public List<ApiError> Errors { get; set; } = new();

    [JsonPropertyName("meta")]
    public Dictionary<string, object>? Meta { get; set; }
}

public sealed class ApiError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
