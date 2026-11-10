using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DM_RMS.Wpf.Models;

public sealed class RecipeListRequest
{
    [JsonPropertyName("equipment_ids")]
    public List<string> EquipmentIds { get; set; } = new();

    [JsonPropertyName("filters")]
    public Dictionary<string, object> Filters { get; set; } = new();
}

public sealed class RecipeDetailRequest
{
    [JsonPropertyName("eqp_id")]
    public string EqpId { get; set; } = string.Empty;

    [JsonPropertyName("recipe_name")]
    public string RecipeName { get; set; } = string.Empty;
}

public sealed class RecipeActionRequest
{
    [JsonPropertyName("backup_path")]
    public string? BackupPath { get; set; }

    [JsonPropertyName("items")]
    public List<RecipeActionItem> Items { get; set; } = new();
}

public sealed class RecipeActionItem
{
    [JsonPropertyName("eqp_id")]
    public string EqpId { get; set; } = string.Empty;

    [JsonPropertyName("recipe_name")]
    public string RecipeName { get; set; } = string.Empty;
}


public sealed class RecipeColumnFilter : INotifyPropertyChanged
{
    private string _selectedValue = string.Empty;
    private string _textValue = string.Empty;

    public string ColumnName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsComboBox { get; set; }
    public ObservableCollection<string> Options { get; } = new();

    public string SelectedValue
    {
        get => _selectedValue;
        set => SetProperty(ref _selectedValue, value);
    }

    public string TextValue
    {
        get => _textValue;
        set => SetProperty(ref _textValue, value);
    }

    public string EffectiveValue => IsComboBox ? SelectedValue : TextValue;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EffectiveValue)));
    }
}
