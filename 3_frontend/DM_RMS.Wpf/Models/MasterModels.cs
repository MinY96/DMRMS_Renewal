using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DM_RMS.Wpf.Models;

public sealed class MasterListRequest
{
    [JsonPropertyName("filters")]
    public Dictionary<string, object> Filters { get; set; } = new();
}

public sealed class MasterUpdateRequest
{
    [JsonPropertyName("parameters")]
    public List<MasterParameterEdit> Parameters { get; set; } = new();
}

public sealed class MasterCreateRequest
{
    [JsonPropertyName("criteria")]
    public Dictionary<string, string> Criteria { get; set; } = new();

    [JsonPropertyName("parameters")]
    public List<MasterParameterEdit> Parameters { get; set; } = new();
}

public sealed class MasterParameterEdit
{
    [JsonPropertyName("parameter_name")]
    public string ParameterName { get; set; } = string.Empty;

    [JsonPropertyName("display_value")]
    public string DisplayValue { get; set; } = string.Empty;
}

public sealed class MasterDetailResponse
{
    [JsonPropertyName("master_key")]
    public string MasterKey { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public List<MasterParameterDto> Parameters { get; set; } = new();
}

public sealed class MasterParameterDto
{
    [JsonPropertyName("parameter_name")]
    public string ParameterName { get; set; } = string.Empty;

    [JsonPropertyName("control_type")]
    public string ControlType { get; set; } = "string";

    [JsonPropertyName("allowed_values")]
    public List<string> AllowedValues { get; set; } = new();

    [JsonPropertyName("display_value")]
    public string DisplayValue { get; set; } = string.Empty;
}

public sealed class MasterJsonFile
{
    [JsonPropertyName("master_key")]
    public string MasterKey { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public List<MasterJsonParameter> Parameters { get; set; } = new();
}

/// <summary>
/// Master Recipe JSON 파일에 저장되는 Parameter 정보입니다.
///
/// API 수정 요청용 MasterParameterEdit과 달리, 파일을 다시 불러왔을 때
/// 동일한 편집 컨트롤을 재구성할 수 있도록 ControlType과 AllowedValues도
/// 함께 저장합니다.
/// </summary>
public sealed class MasterJsonParameter
{
    [JsonPropertyName("parameter_name")]
    public string ParameterName { get; set; } = string.Empty;

    [JsonPropertyName("control_type")]
    public string ControlType { get; set; } = "STRING";

    [JsonPropertyName("allowed_values")]
    public List<string> AllowedValues { get; set; } = new();

    [JsonPropertyName("display_value")]
    public string DisplayValue { get; set; } = string.Empty;

    /// <summary>
    /// 화면에서 사용 중인 Parameter Item을 JSON 저장 모델로 변환합니다.
    /// </summary>
    /// <param name="item">현재 Master Parameter 화면 항목입니다.</param>
    /// <returns>JSON 파일에 저장할 Parameter 모델입니다.</returns>
    public static MasterJsonParameter FromItem(MasterParameterItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new MasterJsonParameter
        {
            ParameterName = item.ParameterName,
            ControlType = item.ControlType,
            DisplayValue = item.GetCurrentValue(),
            AllowedValues = item.AllowedValues.ToList()
        };
    }

    /// <summary>
    /// JSON 파일 모델을 화면 생성에 사용하는 DTO로 변환합니다.
    /// </summary>
    /// <returns>Master Parameter 화면 생성용 DTO입니다.</returns>
    public MasterParameterDto ToDto()
    {
        return new MasterParameterDto
        {
            ParameterName = ParameterName,
            ControlType = NormalizeControlType(ControlType),
            DisplayValue = DisplayValue,
            AllowedValues = AllowedValues?.ToList() ?? new List<string>()
        };
    }

    /// <summary>
    /// 과거 또는 외부 JSON에서 사용하는 타입 표현을 현재 표준 타입으로 정규화합니다.
    /// </summary>
    private static string NormalizeControlType(string? controlType)
    {
        var normalized = (controlType ?? string.Empty)
            .Trim()
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal)
            .ToUpperInvariant();

        return normalized switch
        {
            "STRING" => "STRING",
            "INTEGER" or "INT" => "INTEGER",
            "DOUBLE" or "FLOAT" or "DECIMAL" => "DOUBLE",

            "INTEGERRANGE"
                or "INTEGER_RANGE"
                or "RANGEINTEGER"
                or "RANGE_INTEGER" => "RANGE_INTEGER",

            "DOUBLERANGE"
                or "DOUBLE_RANGE"
                or "RANGEDOUBLE"
                or "RANGE_DOUBLE" => "RANGE_DOUBLE",

            _ => "STRING"
        };
    }
}


public sealed class MasterFilterItem : INotifyPropertyChanged
{
    private string _selectedValue = string.Empty;
    private string _textValue = string.Empty;

    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsLikeFilter { get; set; }
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

    public string EffectiveValue => IsLikeFilter ? TextValue : SelectedValue;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EffectiveValue)));
    }
}

public sealed class MasterParameterItem : INotifyPropertyChanged
{
    private string _displayValue = string.Empty;
    private string _editValue = string.Empty;
    private string _editMinValue = string.Empty;
    private string _editMaxValue = string.Empty;
    private string _originalValue = string.Empty;
    private string _validationError = string.Empty;
    private bool _isEditMode;

    public string ParameterName { get; set; } = string.Empty;
    public string ControlType { get; set; } = "STRING";
    public ObservableCollection<string> AllowedValues { get; } = new();

    public string DisplayValue
    {
        get => _displayValue;
        set => SetProperty(ref _displayValue, value);
    }

    public string EditValue
    {
        get => _editValue;
        set
        {
            if (SetProperty(ref _editValue, value))
            {
                Validate();
                OnPropertyChanged(nameof(EditIntegerValue));
                OnPropertyChanged(nameof(EditDoubleValue));
                OnChangedStateUpdated();
            }
        }
    }

    public string EditMinValue
    {
        get => _editMinValue;
        set
        {
            if (SetProperty(ref _editMinValue, value))
            {
                RebuildRangeEditValue();
                Validate();
                OnPropertyChanged(nameof(EditIntegerMinValue));
                OnPropertyChanged(nameof(EditDoubleMinValue));
                OnChangedStateUpdated();
            }
        }
    }

    public string EditMaxValue
    {
        get => _editMaxValue;
        set
        {
            if (SetProperty(ref _editMaxValue, value))
            {
                RebuildRangeEditValue();
                Validate();
                OnPropertyChanged(nameof(EditIntegerMaxValue));
                OnPropertyChanged(nameof(EditDoubleMaxValue));
                OnChangedStateUpdated();
            }
        }
    }

    public int? EditIntegerValue
    {
        get => int.TryParse(EditValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
        set => EditValue = value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public double? EditDoubleValue
    {
        get => double.TryParse(EditValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
        set => EditValue = value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public int? EditIntegerMinValue
    {
        get => int.TryParse(EditMinValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
        set => EditMinValue = value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public int? EditIntegerMaxValue
    {
        get => int.TryParse(EditMaxValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
        set => EditMaxValue = value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public double? EditDoubleMinValue
    {
        get => double.TryParse(EditMinValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
        set => EditMinValue = value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public double? EditDoubleMaxValue
    {
        get => double.TryParse(EditMaxValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
        set => EditMaxValue = value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public string OriginalValue
    {
        get => _originalValue;
        set
        {
            if (SetProperty(ref _originalValue, value))
                OnChangedStateUpdated();
        }
    }

    public string ValidationError
    {
        get => _validationError;
        set => SetProperty(ref _validationError, value);
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set => SetProperty(ref _isEditMode, value);
    }

    public bool IsRange => ControlType.Equals("RANGE_INTEGER", StringComparison.OrdinalIgnoreCase)
                           || ControlType.Equals("RANGE_DOUBLE", StringComparison.OrdinalIgnoreCase);

    public bool IsIntegerType => ControlType.Contains("INTEGER", StringComparison.OrdinalIgnoreCase);
    public bool IsDoubleType => ControlType.Contains("DOUBLE", StringComparison.OrdinalIgnoreCase);
    public bool IsChanged => !string.Equals(GetCurrentValue(), OriginalValue, StringComparison.Ordinal);
    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationError);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// DTO 정보를 기반으로 신규 Master 편집 항목을 생성합니다.
    ///
    /// Range 타입은 display_value를 최소값과 최대값으로 분리하며,
    /// String 타입은 현재 값이 AllowedValues에 없더라도 ComboBox에서
    /// 표시될 수 있도록 선택 항목에 현재 값을 보완합니다.
    /// </summary>
    /// <param name="dto">Parameter 이름, 타입, 허용값 및 설정값입니다.</param>
    /// <param name="isEditMode">생성 직후 편집 가능 여부입니다.</param>
    /// <returns>화면에 바인딩할 Master Parameter 항목입니다.</returns>
    public static MasterParameterItem FromDto(
        MasterParameterDto dto,
        bool isEditMode = false)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var item = new MasterParameterItem
        {
            ParameterName = dto.ParameterName,
            ControlType = NormalizeControlType(dto.ControlType),
            DisplayValue = dto.DisplayValue,
            EditValue = dto.DisplayValue,
            OriginalValue = dto.DisplayValue,
            IsEditMode = isEditMode
        };

        foreach (var value in dto.AllowedValues ?? new List<string>())
        {
            if (!item.AllowedValues.Contains(value))
            {
                item.AllowedValues.Add(value);
            }
        }

        // 저장 당시 허용값 목록이 없거나 변경된 경우에도 현재 문자열 값을
        // ComboBox에서 정상적으로 표시할 수 있도록 현재 값을 보완합니다.
        if (item.ControlType.Equals(
                "STRING",
                StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(dto.DisplayValue)
            && !item.AllowedValues.Contains(dto.DisplayValue))
        {
            item.AllowedValues.Add(dto.DisplayValue);
        }

        // RANGE_INTEGER / RANGE_DOUBLE이면 내부적으로 Min/Max 값을 분리합니다.
        item.ApplyRangeParts(dto.DisplayValue);
        item.Validate();

        return item;
    }

    private static string NormalizeControlType(string? controlType)
    {
        var normalized = (controlType ?? string.Empty)
            .Trim()
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal)
            .ToUpperInvariant();

        return normalized switch
        {
            "STRING" => "STRING",
            "INTEGER" or "INT" => "INTEGER",
            "DOUBLE" or "FLOAT" or "DECIMAL" => "DOUBLE",

            "INTEGERRANGE"
                or "INTEGER_RANGE"
                or "RANGEINTEGER"
                or "RANGE_INTEGER" => "RANGE_INTEGER",

            "DOUBLERANGE"
                or "DOUBLE_RANGE"
                or "RANGEDOUBLE"
                or "RANGE_DOUBLE" => "RANGE_DOUBLE",

            _ => "STRING"
        };
    }

    public MasterParameterEdit ToEdit()
    {
        return new MasterParameterEdit
        {
            ParameterName = ParameterName,
            DisplayValue = GetCurrentValue()
        };
    }

    public bool Validate()
    {
        ValidationError = string.Empty;

        if (IsRange)
        {
            if (!IsValidNumber(EditMinValue) || !IsValidNumber(EditMaxValue))
            {
                ValidationError = $"{ParameterName}: 범위 값은 숫자여야 합니다.";
                return false;
            }

            var min = ParseDouble(EditMinValue);
            var max = ParseDouble(EditMaxValue);
            if (min > max)
            {
                ValidationError = $"{ParameterName}: 최소값은 최대값보다 클 수 없습니다.";
                return false;
            }
        }
        else if (IsIntegerType || IsDoubleType)
        {
            if (!IsValidNumber(EditValue))
            {
                ValidationError = $"{ParameterName}: 숫자 형식이 올바르지 않습니다.";
                return false;
            }
        }

        OnPropertyChanged(nameof(HasValidationError));
        return true;
    }

    public string GetCurrentValue()
    {
        return IsRange ? $"{EditMinValue}~{EditMaxValue}" : EditValue;
    }

    public void AcceptCurrentValueAsSaved()
    {
        var value = GetCurrentValue();
        OriginalValue = value;
        DisplayValue = value;
        EditValue = value;
        ApplyRangeParts(value);
        Validate();
    }

    private void ApplyRangeParts(string value)
    {
        if (!IsRange) return;
        var parts = value.Split('~', 2, StringSplitOptions.TrimEntries);
        _editMinValue = parts.Length > 0 ? parts[0] : string.Empty;
        _editMaxValue = parts.Length > 1 ? parts[1] : string.Empty;
        OnPropertyChanged(nameof(EditMinValue));
        OnPropertyChanged(nameof(EditMaxValue));
        OnPropertyChanged(nameof(EditIntegerMinValue));
        OnPropertyChanged(nameof(EditIntegerMaxValue));
        OnPropertyChanged(nameof(EditDoubleMinValue));
        OnPropertyChanged(nameof(EditDoubleMaxValue));
    }

    private void RebuildRangeEditValue()
    {
        if (!IsRange) return;
        _editValue = $"{EditMinValue}~{EditMaxValue}";
        OnPropertyChanged(nameof(EditValue));
        OnPropertyChanged(nameof(EditIntegerValue));
        OnPropertyChanged(nameof(EditDoubleValue));
    }

    private bool IsValidNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (IsIntegerType)
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }

    private static double ParseDouble(string value)
        => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

    private void OnChangedStateUpdated()
    {
        OnPropertyChanged(nameof(IsChanged));
        OnPropertyChanged(nameof(HasValidationError));
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
