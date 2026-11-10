using System.Text.Json.Serialization;

namespace DM_RMS.Wpf.Models;

/// <summary>
/// 사용자가 처리 대상으로 선택한 장비/Recipe 식별 정보입니다.
/// </summary>
public sealed class RecipeSelectionItem
{
    [JsonPropertyName("eqp_id")]
    public string EqpId { get; set; } = string.Empty;

    [JsonPropertyName("recipe_name")]
    public string RecipeName { get; set; } = string.Empty;
}
