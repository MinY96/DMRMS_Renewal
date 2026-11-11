using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DM_RMS.Wpf.Models;

/// <summary>
/// 사용자가 처리 대상으로 선택한 장비/Recipe 식별 정보입니다.
/// </summary>
public sealed class RecipeSelectionItem : INotifyPropertyChanged
{
    private string _operationStatus = "미처리";
    private string _operationMessage = string.Empty;

    [JsonIgnore]
    public string OperationStatus
    {
        get => _operationStatus;
        set => SetProperty(ref _operationStatus, value);
    }

    [JsonIgnore]
    public string OperationMessage
    {
        get => _operationMessage;
        set => SetProperty(ref _operationMessage, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    [JsonPropertyName("eqp_id")]
    public string EqpId { get; set; } = string.Empty;

    [JsonPropertyName("recipe_name")]
    public string RecipeName { get; set; } = string.Empty;
}
