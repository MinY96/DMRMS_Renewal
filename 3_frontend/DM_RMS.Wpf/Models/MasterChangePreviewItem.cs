namespace DM_RMS.Wpf.Models;

/// <summary>Master Parameter 저장 전에 기존값과 변경값을 비교하는 표시 전용 데이터.</summary>
public sealed class MasterChangePreviewItem
{
    public string ParameterName { get; init; } = string.Empty;
    public string OriginalValue { get; init; } = string.Empty;
    public string UpdatedValue { get; init; } = string.Empty;
}
