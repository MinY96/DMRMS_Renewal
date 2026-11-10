using System.Text.Json.Serialization;

namespace DM_RMS.Wpf.Models;

/// <summary>
/// Recipe 백업/삭제 ZIP 내부의 result.json 역직렬화 모델입니다.
/// </summary>
public sealed class RecipeArchiveManifest
{
    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("requested_count")]
    public int RequestedCount { get; init; }

    [JsonPropertyName("success_count")]
    public int SuccessCount { get; init; }

    [JsonPropertyName("failure_count")]
    public int FailureCount { get; init; }

    [JsonPropertyName("backup_success_count")]
    public int BackupSuccessCount { get; init; }

    [JsonPropertyName("deleted_count")]
    public int DeletedCount { get; init; }

    [JsonPropertyName("failed_count")]
    public int FailedCount { get; init; }

    [JsonPropertyName("deleted_items")]
    public List<RecipeSelectionItem> DeletedItems { get; init; } = new();

    [JsonIgnore]
    public int EffectiveFailureCount => FailureCount + FailedCount;
}

/// <summary>
/// 다운로드한 Recipe ZIP의 저장 및 처리 결과입니다.
/// </summary>
public sealed class RecipeArchiveExtractionResult
{
    public required string DestinationDirectory { get; init; }
    public required RecipeArchiveManifest Manifest { get; init; }
}
