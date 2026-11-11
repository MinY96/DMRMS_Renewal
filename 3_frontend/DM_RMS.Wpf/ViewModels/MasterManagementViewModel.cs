using DM_RMS.Wpf.Commands;
using DM_RMS.Wpf.Helpers;
using DM_RMS.Wpf.Models;
using DM_RMS.Wpf.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace DM_RMS.Wpf.ViewModels;

public sealed class MasterManagementViewModel : BaseViewModel
{
    public static readonly string[] MasterListHiddenColumns = { "internal_id" };
    private readonly DmRmsApiService _api;
    private readonly IDialogService _dialog;
    private string _selectedFab = "fab1";
    private string _selectedMasterKey = string.Empty;
    private string _selectedModel = string.Empty;
    private long _filterRequestVersion;
    private DataView? _masterListView;
    private bool _isEditMode;
    private bool _isLoadingFilters;

    public MasterManagementViewModel(DmRmsApiService api, IDialogService dialog, bool canManageMaster)
    {
        _api = api;
        _dialog = dialog;
        CanManageMaster = canManageMaster;

        LoadFilterOptionsCommand = new AsyncRelayCommand(_ => LoadModelsAsync());
        SearchMasterListCommand = new AsyncRelayCommand(_ => SearchMasterListAsync(), _ => CanSearchMasterList);
        LoadMasterDetailCommand = new AsyncRelayCommand(row => LoadMasterDetailAsync(row));
        DeleteMasterCommand = new AsyncRelayCommand(row => DeleteMasterAsync(row), _ => CanManageMaster);
        BeginEditCommand = new RelayCommand(_ => SetEditMode(true), _ => CanManageMaster);
        CancelEditCommand = new RelayCommand(_ => SetEditMode(false));
        SaveMasterDetailCommand = new AsyncRelayCommand(_ => SaveMasterDetailAsync(), _ => CanManageMaster);
        SaveMasterJsonFileCommand = new RelayCommand(_ => SaveMasterJsonFile());
        OpenNewMasterCommand = new AsyncRelayCommand(_ => OpenNewMasterAsync(), _ => CanCreateNewMaster);
        CopyMasterListCommand = new RelayCommand(_ => DataTableExportHelper.CopyToClipboard(MasterListView, MasterListHiddenColumns));
        ExportMasterListCsvCommand = new RelayCommand(_ => ExportCsv(MasterListView, "master_recipe_list.csv", MasterListHiddenColumns));
        ExportMasterListExcelCommand = new RelayCommand(_ => ExportExcel(MasterListView, "master_recipe_list.xlsx", "MasterList", MasterListHiddenColumns));
        CopyMasterDetailCommand = new RelayCommand(_ => CopyMasterDetailToClipboard());
        ExportMasterDetailExcelCommand = new RelayCommand(_ => ExportExcel(CreateMasterDetailTable(), "master_recipe_detail.xlsx", "MasterDetail"));

        MasterParameters.CollectionChanged += (_, e) =>
        {
            if (e.OldItems != null)
                foreach (MasterParameterItem item in e.OldItems)
                    item.PropertyChanged -= OnMasterParameterChanged;
            if (e.NewItems != null)
                foreach (MasterParameterItem item in e.NewItems)
                    item.PropertyChanged += OnMasterParameterChanged;
            OnPropertyChanged(nameof(MasterDetailRowCount));
            OnPropertyChanged(nameof(ChangedMasterCount));
        };

        _ = LoadModelsAsync();
    }

    public ObservableCollection<string> Models { get; } = new();
    public ObservableCollection<MasterFilterItem> Filters { get; } = new();

    /// <summary>
    /// Master 기준정보 테이블의 컬럼 헤더 아래에 표시할 로컬 필터 목록입니다.
    /// 상단 기준정보 필터는 서버 조회 조건이고, 이 필터는 조회된 결과를 즉시 필터링합니다.
    /// </summary>
    public ObservableCollection<RecipeColumnFilter> MasterListColumnFilters { get; } = new();

    public ObservableCollection<MasterParameterItem> MasterParameters { get; } = new();

    public string SelectedFab
    {
        get => _selectedFab;
        set => SetProperty(ref _selectedFab, value); // 사업장은 로그인 세션에 귀속됩니다.
    }

    public string SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (SetProperty(ref _selectedModel, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(CanCreateNewMaster));
                OnPropertyChanged(nameof(SelectedModelDescription));
                OnPropertyChanged(nameof(CanSearchMasterList));
                ((AsyncRelayCommand)SearchMasterListCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)OpenNewMasterCommand).RaiseCanExecuteChanged();
                _ = LoadFilterOptionsAsync();
            }
        }
    }

    public bool CanCreateNewMaster => CanManageMaster && !string.IsNullOrWhiteSpace(SelectedModel);
    public string SelectedModelDescription => string.IsNullOrWhiteSpace(SelectedModel)
        ? "선택된 Model 없음 · 먼저 Model을 선택하세요."
        : $"현재 Model: {SelectedModel}";
    public bool IsLoadingFilters
    {
        get => _isLoadingFilters;
        private set
        {
            if (!SetProperty(ref _isLoadingFilters, value)) return;
            OnPropertyChanged(nameof(FilterLoadingDescription));
            OnPropertyChanged(nameof(CanSearchMasterList));
            ((AsyncRelayCommand)SearchMasterListCommand).RaiseCanExecuteChanged();
        }
    }
    public string FilterLoadingDescription => IsLoadingFilters ? "기준정보 불러오는 중..." : string.Empty;
    public bool CanSearchMasterList => !IsLoadingFilters && !string.IsNullOrWhiteSpace(SelectedModel);
    public int ChangedMasterCount => MasterParameters.Count(x => x.IsChanged);

    private void OnMasterParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MasterParameterItem.IsChanged))
            OnPropertyChanged(nameof(ChangedMasterCount));
    }

    public string SelectedMasterKey
    {
        get => _selectedMasterKey;
        set => SetProperty(ref _selectedMasterKey, value);
    }

    public DataView? MasterListView
    {
        get => _masterListView;
        set
        {
            if (SetProperty(ref _masterListView, value))
                OnPropertyChanged(nameof(MasterListRowCount));
        }
    }

    public int MasterListRowCount => MasterListView?.Count ?? 0;
    public int MasterDetailRowCount => MasterParameters.Count;

    public bool CanManageMaster { get; }

    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (SetProperty(ref _isEditMode, value))
            {
                foreach (var parameter in MasterParameters)
                    parameter.IsEditMode = value;
            }
        }
    }

    public ICommand LoadFilterOptionsCommand { get; }
    public ICommand SearchMasterListCommand { get; }
    public ICommand LoadMasterDetailCommand { get; }
    public ICommand DeleteMasterCommand { get; }
    public ICommand BeginEditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand SaveMasterDetailCommand { get; }
    public ICommand SaveMasterJsonFileCommand { get; }
    public ICommand OpenNewMasterCommand { get; }
    public ICommand CopyMasterListCommand { get; }
    public ICommand ExportMasterListCsvCommand { get; }
    public ICommand ExportMasterListExcelCommand { get; }
    public ICommand CopyMasterDetailCommand { get; }
    public ICommand ExportMasterDetailExcelCommand { get; }

    /// <summary>선택 가능한 Model을 최초 한 번 조회하고, 새로고침 시 기존 선택을 보존합니다.</summary>
    private async Task LoadModelsAsync()
    {
        try
        {
            var previousModel = SelectedModel;
            var response = await _api.GetMasterModelOptionsAsync();
            var names = (response.Data ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Models.Clear();
            foreach (var name in names)
                Models.Add(name);

            if (!names.Contains(previousModel, StringComparer.OrdinalIgnoreCase))
                SelectedModel = string.Empty;
            else
                await LoadFilterOptionsAsync();
        }
        catch (Exception ex)
        {
            _dialog.ShowError(ex.Message);
        }
    }

    /// <summary>
    /// Model 변경 시 즉시 현재 화면을 초기화합니다. 오래된 비동기 응답은
    /// 세대 번호를 검사해 폐기하므로 FAB2의 필터 중복도 발생하지 않습니다.
    /// </summary>
    private async Task LoadFilterOptionsAsync()
    {
        var generation = System.Threading.Interlocked.Increment(ref _filterRequestVersion);
        var model = SelectedModel;
        Filters.Clear();
        ClearMasterListColumnFilters();
        MasterListView = null;
        MasterParameters.Clear();
        SelectedMasterKey = string.Empty;
        IsEditMode = false;
        // 이전 요청의 완료 이벤트는 generation 검사 후에만 현재 로딩 상태를 변경할 수 있습니다.
        IsLoadingFilters = !string.IsNullOrWhiteSpace(model);
        if (string.IsNullOrWhiteSpace(model)) return;

        try
        {
            var response = await _api.GetMasterFilterOptionsAsync(model);
            if (generation != System.Threading.Interlocked.Read(ref _filterRequestVersion)
                || !string.Equals(SelectedModel, model, StringComparison.OrdinalIgnoreCase))
                return;

            if (response.Data.ValueKind != JsonValueKind.Object) return;

            // _like 여부가 다른 동일 기준정보 및 서버의 중복 옵션을 정규화합니다.
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var orderedProperties = response.Data.EnumerateObject()
                .OrderBy(property => GetFilterOrder(property.Name))
                .ThenBy(property => property.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var property in orderedProperties)
            {
                var normalizedKey = property.Name.Replace("_like", string.Empty, StringComparison.OrdinalIgnoreCase);
                if (string.Equals(normalizedKey, "model", StringComparison.OrdinalIgnoreCase)
                    || !seenKeys.Add(normalizedKey))
                    continue;

                var item = new MasterFilterItem
                {
                    Key = property.Name,
                    DisplayName = ToDisplayName(property.Name),
                    IsLikeFilter = property.Name.EndsWith("_like", StringComparison.OrdinalIgnoreCase)
                };

                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    item.Options.Add(string.Empty);
                    foreach (var option in property.Value.EnumerateArray()
                        .Select(x => x.ValueKind == JsonValueKind.Null ? string.Empty : x.ToString())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase))
                        item.Options.Add(option);
                }
                Filters.Add(item);
            }
        }
        catch (Exception ex)
        {
            if (generation == System.Threading.Interlocked.Read(ref _filterRequestVersion))
                _dialog.ShowError(ex.Message);
        }
        finally
        {
            if (generation == System.Threading.Interlocked.Read(ref _filterRequestVersion))
                IsLoadingFilters = false;
        }
    }

    private async Task SearchMasterListAsync()
    {
        if (IsLoadingFilters) return;
        if (string.IsNullOrWhiteSpace(SelectedModel))
        {
            _dialog.ShowInfo("조회할 Model을 먼저 선택하세요.");
            return;
        }

        var requestedModel = SelectedModel;
        var requestFilters = BuildFilterRequest();
        requestFilters["model"] = new Dictionary<string, string>
        {
            ["mode"] = "equals",
            ["value"] = requestedModel
        };
        try
        {
            var response = await _api.GetMasterListAsync(new MasterListRequest { Filters = requestFilters });
            if (!string.Equals(requestedModel, SelectedModel, StringComparison.OrdinalIgnoreCase))
                return;
            var table = response.Data?.ToDataTable();
            MasterListView = table?.DefaultView;
            BuildMasterListColumnFilters(table);
            ApplyMasterListColumnFilters();
            MasterParameters.Clear();
            SelectedMasterKey = string.Empty;
            IsEditMode = false;
        }
        catch (Exception ex)
        {
            _dialog.ShowError(ex.Message);
        }
    }

    private async Task LoadMasterDetailAsync(object? rowObject)
    {
        try
        {
            var masterKey = ExtractMasterKey(rowObject);
            if (string.IsNullOrWhiteSpace(masterKey)) return;

            var response = await _api.GetMasterDetailAsync(masterKey);
            SelectedMasterKey = response.Data?.MasterKey ?? masterKey;
            MasterParameters.Clear();
            foreach (var dto in response.Data?.Parameters ?? new List<MasterParameterDto>())
                MasterParameters.Add(MasterParameterItem.FromDto(dto));

            IsEditMode = false;
        }
        catch (Exception ex)
        {
            _dialog.ShowError(ex.Message);
        }
    }

    private async Task SaveMasterDetailAsync()
    {
        if (!CanManageMaster)
        {
            _dialog.ShowInfo("Master Recipe 편집 및 저장은 관리자 계정에서만 사용할 수 있습니다.");
            return;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(SelectedMasterKey))
            {
                _dialog.ShowInfo("저장할 Master Key를 먼저 조회하세요.");
                return;
            }

            var invalidItems = MasterParameters.Where(x => !x.Validate()).ToList();
            if (invalidItems.Count > 0)
            {
                _dialog.ShowError(string.Join(Environment.NewLine, invalidItems.Select(x => x.ValidationError)));
                return;
            }

            var changedItems = MasterParameters.Where(x => x.IsChanged).ToList();
            if (changedItems.Count == 0)
            {
                _dialog.ShowInfo("변경된 Parameter가 없습니다.");
                return;
            }

            var preview = changedItems.Select(x => new MasterChangePreviewItem
            {
                ParameterName = x.ParameterName,
                OriginalValue = x.DisplayValue,
                UpdatedValue = x.GetCurrentValue()
            }).ToList();
            if (!_dialog.ConfirmMasterChanges(SelectedMasterKey, preview))
                return;

            var request = new MasterUpdateRequest
            {
                Parameters = changedItems.Select(x => x.ToEdit()).ToList()
            };

            var response = await _api.UpdateMasterDetailAsync(SelectedMasterKey, request);

            foreach (var parameter in changedItems)
                parameter.AcceptCurrentValueAsSaved();

            var savedMasterKey = SelectedMasterKey;
            IsEditMode = false;
            _dialog.ShowInfo(response.Message);

            await SearchMasterListAsync();
            await LoadMasterDetailByKeyAsync(savedMasterKey);
        }
        catch (Exception ex)
        {
            _dialog.ShowError(ex.Message);
        }
    }

    private async Task DeleteMasterAsync(object? rowObject)
    {
        if (!CanManageMaster)
        {
            _dialog.ShowInfo("Master Recipe 삭제는 관리자 계정에서만 사용할 수 있습니다.");
            return;
        }

        try
        {
            var masterKey = ExtractMasterKey(rowObject);
            if (string.IsNullOrWhiteSpace(masterKey)) return;
            if (!_dialog.Confirm($"해당 Master Key({masterKey})를 삭제하시겠습니까?")) return;

            var response = await _api.DeleteMasterAsync(masterKey);
            RemoveMasterRow(masterKey);

            if (SelectedMasterKey == masterKey)
            {
                SelectedMasterKey = string.Empty;
                MasterParameters.Clear();
                IsEditMode = false;
            }

            _dialog.ShowInfo(response.Message);
        }
        catch (Exception ex)
        {
            _dialog.ShowError(ex.Message);
        }
    }

    private void SaveMasterJsonFile()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedMasterKey)
                || MasterParameters.Count == 0)
            {
                _dialog.ShowInfo(
                    "파일로 저장할 Master 정보를 먼저 조회하세요.");
                return;
            }

            var path = _dialog.SelectSaveFile(
                "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
                $"{SelectedMasterKey}.json");

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var payload = new MasterJsonFile
            {
                MasterKey = SelectedMasterKey,
                Parameters = MasterParameters
                    .Select(MasterJsonParameter.FromItem)
                    .ToList()
            };

            var serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(
                payload,
                serializerOptions);

            File.WriteAllText(
                path,
                json,
                System.Text.Encoding.UTF8);

            _dialog.ShowInfo(
                "Master Recipe JSON 저장이 완료되었습니다.");
        }
        catch (Exception ex)
        {
            _dialog.ShowError(ex.Message);
        }
    }

    private async Task OpenNewMasterAsync()
    {
        if (!CanCreateNewMaster)
        {
            _dialog.ShowInfo("관리자 권한과 Model 선택이 필요합니다.");
            return;
        }

        try
        {
            var viewModel = new NewMasterViewModel(_api, _dialog, Filters, SelectedModel);
            await viewModel.InitializeAsync();
            var result = _dialog.ShowNewMasterDialog(viewModel);
            if (result == true || viewModel.Created)
                await SearchMasterListAsync();
        }
        catch (Exception ex)
        {
            _dialog.ShowError(ex.Message);
        }
    }

    private async Task LoadMasterDetailByKeyAsync(string masterKey)
    {
        if (string.IsNullOrWhiteSpace(masterKey)) return;
        var response = await _api.GetMasterDetailAsync(masterKey);
        SelectedMasterKey = response.Data?.MasterKey ?? masterKey;
        MasterParameters.Clear();
        foreach (var dto in response.Data?.Parameters ?? new List<MasterParameterDto>())
            MasterParameters.Add(MasterParameterItem.FromDto(dto));
        IsEditMode = false;
    }

    /// <summary>
    /// 조회된 Master 기준정보 테이블의 컬럼을 기준으로 헤더 필터를 구성합니다.
    /// 고유값 수가 적고 기준정보 성격이 강한 컬럼은 ComboBox, 그 외 컬럼은 LIKE TextBox를 사용합니다.
    /// </summary>
    /// <param name="table">서버 응답을 변환한 Master 기준정보 DataTable입니다.</param>
    private void BuildMasterListColumnFilters(DataTable? table)
    {
        ClearMasterListColumnFilters();
        if (table == null) return;

        var comboColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            //"model", "tech_prod", "tech", "prod_group", "mode"
        };

        foreach (DataColumn column in table.Columns)
        {
            if (MasterListHiddenColumns.Contains(column.ColumnName, StringComparer.OrdinalIgnoreCase))
                continue;

            var filter = new RecipeColumnFilter
            {
                ColumnName = column.ColumnName,
                DisplayName = ToDisplayName(column.ColumnName),
                IsComboBox = comboColumns.Contains(column.ColumnName)
            };

            if (filter.IsComboBox)
            {
                filter.Options.Add(string.Empty);
                foreach (var value in table.AsEnumerable()
                    .Select(row => row.IsNull(column) ? "(빈 값)" : Convert.ToString(row[column]) ?? string.Empty)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                {
                    filter.Options.Add(value);
                }
            }

            filter.PropertyChanged += MasterListColumnFilter_PropertyChanged;
            MasterListColumnFilters.Add(filter);
        }
    }

    /// <summary>
    /// 기존 Master 기준정보 헤더 필터의 이벤트를 해제하고 목록을 초기화합니다.
    /// 반복 조회 시 ViewModel이 필터 객체를 계속 참조하는 것을 방지합니다.
    /// </summary>
    private void ClearMasterListColumnFilters()
    {
        foreach (var filter in MasterListColumnFilters)
            filter.PropertyChanged -= MasterListColumnFilter_PropertyChanged;

        MasterListColumnFilters.Clear();
    }

    private void MasterListColumnFilter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecipeColumnFilter.EffectiveValue))
            ApplyMasterListColumnFilters();
    }

    /// <summary>
    /// Master 기준정보 테이블에 입력된 컬럼별 필터를 DataView.RowFilter로 적용합니다.
    /// 서버 재조회 없이 현재 결과에서 즉시 필터링하므로 입력 반응이 빠릅니다.
    /// </summary>
    private void ApplyMasterListColumnFilters()
    {
        if (MasterListView == null) return;

        var expressions = new List<string>();
        foreach (var filter in MasterListColumnFilters)
        {
            var value = filter.EffectiveValue.Trim();
            if (string.IsNullOrWhiteSpace(value)) continue;

            var column = EscapeColumnName(filter.ColumnName);
            if (filter.IsComboBox)
            {
                expressions.Add(value == "(빈 값)"
                    ? $"[{column}] IS NULL"
                    : $"Convert([{column}], 'System.String') = '{EscapeFilterValue(value)}'");
            }
            else
            {
                expressions.Add($"Convert([{column}], 'System.String') LIKE '%{EscapeLikeValue(value)}%'");
            }
        }

        MasterListView.RowFilter = string.Join(" AND ", expressions);
        OnPropertyChanged(nameof(MasterListRowCount));
    }

    private static string EscapeColumnName(string value)
        => value.Replace("]", "]]", StringComparison.Ordinal);

    private static string EscapeFilterValue(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);

    private static string EscapeLikeValue(string value)
        => EscapeFilterValue(value)
            .Replace("[", "[[]", StringComparison.Ordinal)
            .Replace("%", "[%]", StringComparison.Ordinal)
            .Replace("*", "[*]", StringComparison.Ordinal);

    private Dictionary<string, object> BuildFilterRequest()
    {
        var result = new Dictionary<string, object>();
        foreach (var filter in Filters)
        {
            var value = filter.EffectiveValue?.Trim();
            if (string.IsNullOrWhiteSpace(value)) continue;

            if (filter.IsLikeFilter)
            {
                var key = filter.Key.Replace("_like", string.Empty, StringComparison.OrdinalIgnoreCase);
                result[key] = new Dictionary<string, string> { ["mode"] = "like", ["value"] = value };
            }
            else
            {
                result[filter.Key] = new Dictionary<string, string> { ["mode"] = "equals", ["value"] = value };
            }
        }
        return result;
    }

    private void SetEditMode(bool enabled)
    {
        if (enabled && !CanManageMaster) return;
        IsEditMode = enabled;
    }

    private static string ExtractMasterKey(object? rowObject)
    {
        if (rowObject is DataRowView row && row.Row.Table.Columns.Contains("master_key"))
            return Convert.ToString(row["master_key"]) ?? string.Empty;
        return string.Empty;
    }

    private void RemoveMasterRow(string masterKey)
    {
        if (MasterListView == null) return;
        var rows = MasterListView.Table.Rows.Cast<DataRow>().ToList();
        foreach (var row in rows)
        {
            if (Convert.ToString(row["master_key"]) == masterKey)
                MasterListView.Table.Rows.Remove(row);
        }
    }

    private void CopyMasterDetailToClipboard()
    {
        DataTableExportHelper.CopyToClipboard(CreateMasterDetailTable());
    }

    private DataTable CreateMasterDetailTable()
    {
        var table = new DataTable("MasterDetail");
        table.Columns.Add("parameter_name");
        table.Columns.Add("display_value");
        table.Columns.Add("edit_value");
        table.Columns.Add("control_type");
        table.Columns.Add("is_changed", typeof(bool));

        foreach (var parameter in MasterParameters)
        {
            table.Rows.Add(
                parameter.ParameterName,
                parameter.DisplayValue,
                parameter.EditValue,
                parameter.ControlType,
                parameter.IsChanged);
        }

        return table;
    }

    private void ExportExcel(DataView? view, string defaultName, string worksheetName, IEnumerable<string>? hiddenColumns = null)
    {
        var path = _dialog.SelectSaveFile("Excel 파일 (*.xlsx)|*.xlsx|모든 파일 (*.*)|*.*", defaultName);
        if (string.IsNullOrWhiteSpace(path)) return;
        DataTableExportHelper.SaveExcel(view, path, worksheetName, hiddenColumns);
        _dialog.ShowInfo("Excel 저장이 완료되었습니다.");
    }

    private void ExportExcel(DataTable? table, string defaultName, string worksheetName)
    {
        var path = _dialog.SelectSaveFile("Excel 파일 (*.xlsx)|*.xlsx|모든 파일 (*.*)|*.*", defaultName);
        if (string.IsNullOrWhiteSpace(path)) return;
        DataTableExportHelper.SaveExcel(table, path, worksheetName);
        _dialog.ShowInfo("Excel 저장이 완료되었습니다.");
    }

    private void ExportCsv(DataView? view, string defaultName, IEnumerable<string>? hiddenColumns = null)
    {
        var path = _dialog.SelectSaveFile("CSV 파일 (*.csv)|*.csv|모든 파일 (*.*)|*.*", defaultName);
        if (string.IsNullOrWhiteSpace(path)) return;
        DataTableExportHelper.SaveCsv(view, path, hiddenColumns);
        _dialog.ShowInfo("CSV 저장이 완료되었습니다.");
    }

    private static int GetFilterOrder(string key)
    {
        var normalized = key.Replace("_like", string.Empty, StringComparison.OrdinalIgnoreCase);
        return normalized.ToLowerInvariant() switch
        {
            "tech_prod" => 0,
            "tech" => 1,
            "prod_group" => 2,
            "level" => 3,
            "layer" => 4,
            "mode" => 5,
            "model" => 6,
            _ => 100
        };
    }

    private static string ToDisplayName(string key)
    {
        return key.Replace("_like", string.Empty, StringComparison.OrdinalIgnoreCase) switch
        {
            "layer" => "Layer",
            "level" => "Level",
            "tech_prod" => "Tech Prod",
            "tech" => "Tech",
            "prod_group" => "Prod Group",
            "mode" => "Mode",
            "master_key" => "Master Key",
            "model" => "Model",
            _ => key
        };
    }
}
