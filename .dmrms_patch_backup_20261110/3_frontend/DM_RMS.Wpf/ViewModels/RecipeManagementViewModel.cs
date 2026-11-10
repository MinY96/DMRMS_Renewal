using DM_RMS.Wpf.Commands;
using DM_RMS.Wpf.Helpers;
using DM_RMS.Wpf.Models;
using DM_RMS.Wpf.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Data;
using System.ComponentModel;
using System.Windows.Input;

namespace DM_RMS.Wpf.ViewModels;

public sealed class RecipeManagementViewModel : BaseViewModel
{
    private const int MaxSelectedRecipeCount = 500;
    public static readonly string[] RecipeListHiddenColumns = { "internal_id", "recipe_key", "no_master" };
    public static readonly string[] RecipeDetailHiddenColumns = { "internal_id" };
    private readonly DmRmsApiService _api;
    private readonly IDialogService _dialog;
    private readonly RecipeArchiveService _archiveService;
    private readonly AppSettingsService _settingsService;
    private string _selectedFab = "fab1";
    private string _statusMessage = "준비";
    private string? _backupPath;
    private DataView? _equipmentSummaryView;
    private DataView? _recipeListView;
    private DataView? _recipeDetailView;
    private DataTable? _subscribedRecipeTable;
    private bool _isBulkUpdatingRecipeSelection;
    private bool? _areAllVisibleRecipesSelected = false;
    private bool _isEquipmentPanelOpen;
    private bool _isProcessingPanelOpen;

    public RecipeManagementViewModel(
        DmRmsApiService api,
        IDialogService dialog,
        RecipeArchiveService archiveService,
        AppSettingsService settingsService,
        bool canDeleteRecipes)
    {
        _api = api;
        _dialog = dialog;
        _archiveService = archiveService;
        _settingsService = settingsService;
        _backupPath = _settingsService.GetBackupPath();
        CanDeleteRecipes = canDeleteRecipes;
        LoadEquipmentSummaryCommand = new AsyncRelayCommand(_ => LoadEquipmentSummaryAsync());
        SearchRecipesCommand = new AsyncRelayCommand(_ => SearchRecipesAsync());
        LoadRecipeDetailCommand = new AsyncRelayCommand(row => LoadRecipeDetailAsync(row));
        ChooseBackupPathCommand = new RelayCommand(_ => ChooseBackupPath());
        BackupSelectedRecipesCommand = new AsyncRelayCommand(
            _ => BackupSelectedRecipesAsync(),
            _ => HasBackupPath);
        DeleteSelectedRecipesCommand = new AsyncRelayCommand(
            _ => DeleteSelectedRecipesAsync(),
            _ => CanDeleteRecipes && HasBackupPath);
        CopyEquipmentSummaryCommand = new RelayCommand(_ => DataTableExportHelper.CopyToClipboard(EquipmentSummaryView));
        CopyRecipeListCommand = new RelayCommand(_ => DataTableExportHelper.CopyToClipboard(RecipeListView, RecipeListHiddenColumns));
        CopyRecipeDetailCommand = new RelayCommand(_ => DataTableExportHelper.CopyToClipboard(RecipeDetailView, RecipeDetailHiddenColumns));
        ExportRecipeListCsvCommand = new RelayCommand(_ => ExportCsv(RecipeListView, "recipe_list.csv", RecipeListHiddenColumns));
        ExportEquipmentSummaryExcelCommand = new RelayCommand(_ => ExportExcel(EquipmentSummaryView, "equipment_summary.xlsx", "EquipmentSummary"));
        ExportRecipeListExcelCommand = new RelayCommand(_ => ExportExcel(RecipeListView, "recipe_list.xlsx", "RecipeList", RecipeListHiddenColumns));
        ExportRecipeDetailExcelCommand = new RelayCommand(_ => ExportExcel(RecipeDetailView, "recipe_detail.xlsx", "RecipeDetail", RecipeDetailHiddenColumns));
        CopySelectedRecipesCommand = new RelayCommand(_ => DataTableExportHelper.CopyToClipboard(CreateSelectedRecipesTable()));
        ExportSelectedRecipesExcelCommand = new RelayCommand(_ => ExportExcel(CreateSelectedRecipesTable(), "selected_recipes.xlsx", "SelectedRecipes"));
        RemoveSelectedRecipeCommand = new RelayCommand(item => RemoveSelectedRecipe(item));
        SetVisibleRecipeSelectionCommand = new RelayCommand(value => SetVisibleRecipeSelection(value is bool selected && selected));
        ClearSelectedRecipesCommand = new RelayCommand(_ => ClearSelectedRecipes());
        ToggleEquipmentPanelCommand = new RelayCommand(_ => IsEquipmentPanelOpen = !IsEquipmentPanelOpen);
        ToggleProcessingPanelCommand = new RelayCommand(_ => IsProcessingPanelOpen = !IsProcessingPanelOpen);
        CloseEquipmentPanelCommand = new RelayCommand(_ => IsEquipmentPanelOpen = false);
        CloseProcessingPanelCommand = new RelayCommand(_ => IsProcessingPanelOpen = false);
        CloseSidePanelsCommand = new RelayCommand(_ => CloseSidePanels());
        SelectedRecipes.CollectionChanged += SelectedRecipes_CollectionChanged;
    }

    public IReadOnlyList<string> FabOptions { get; } = new[] { "fab1", "fab2" };
    public ObservableCollection<RecipeSelectionItem> SelectedRecipes { get; } = new();
    public ObservableCollection<RecipeColumnFilter> RecipeFilters { get; } = new();

    public string SelectedFab
    {
        get => _selectedFab;
        set
        {
            if (SetProperty(ref _selectedFab, value))
            {
                _ = LoadEquipmentSummaryAsync();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string? BackupPath
    {
        get => _backupPath;
        set
        {
            if (SetProperty(ref _backupPath, value))
            {
                OnPropertyChanged(nameof(HasBackupPath));
                _settingsService.SaveBackupPath(value);
                BackupSelectedRecipesCommand.RaiseCanExecuteChanged();
                DeleteSelectedRecipesCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasBackupPath => !string.IsNullOrWhiteSpace(BackupPath);

    public bool CanDeleteRecipes { get; }

    public bool IsEquipmentPanelOpen
    {
        get => _isEquipmentPanelOpen;
        set
        {
            if (!SetProperty(ref _isEquipmentPanelOpen, value)) return;
            if (value && IsProcessingPanelOpen)
                IsProcessingPanelOpen = false;
            OnPropertyChanged(nameof(IsAnySidePanelOpen));
        }
    }

    public bool IsProcessingPanelOpen
    {
        get => _isProcessingPanelOpen;
        set
        {
            if (!SetProperty(ref _isProcessingPanelOpen, value)) return;
            if (value && IsEquipmentPanelOpen)
                IsEquipmentPanelOpen = false;
            OnPropertyChanged(nameof(IsAnySidePanelOpen));
        }
    }

    public bool IsAnySidePanelOpen => IsEquipmentPanelOpen || IsProcessingPanelOpen;

    public DataView? EquipmentSummaryView
    {
        get => _equipmentSummaryView;
        set
        {
            if (SetProperty(ref _equipmentSummaryView, value))
                OnPropertyChanged(nameof(EquipmentSummaryRowCount));
        }
    }

    public DataView? RecipeListView
    {
        get => _recipeListView;
        set
        {
            if (_subscribedRecipeTable != null)
                _subscribedRecipeTable.ColumnChanged -= RecipeTable_ColumnChanged;

            _subscribedRecipeTable = value?.Table;
            if (_subscribedRecipeTable != null)
                _subscribedRecipeTable.ColumnChanged += RecipeTable_ColumnChanged;

            if (SetProperty(ref _recipeListView, value))
                OnPropertyChanged(nameof(RecipeListRowCount));
            SyncSelectedRecipesFromRecipeList();
        }
    }

    public DataView? RecipeDetailView
    {
        get => _recipeDetailView;
        set
        {
            if (SetProperty(ref _recipeDetailView, value))
                OnPropertyChanged(nameof(RecipeDetailRowCount));
        }
    }

    public int EquipmentSummaryRowCount => EquipmentSummaryView?.Count ?? 0;
    public int RecipeListRowCount => RecipeListView?.Count ?? 0;
    public int RecipeDetailRowCount => RecipeDetailView?.Count ?? 0;
    public int SelectedRecipeRowCount => SelectedRecipes.Count;
    public int SelectedRecipeLimit => MaxSelectedRecipeCount;
    public string SelectedRecipeCountText => $"{SelectedRecipes.Count:N0}/{SelectedRecipeLimit:N0}";

    /// <summary>
    /// 현재 필터 결과에 보이는 Recipe 행들의 선택 상태.
    /// true=전체 선택, false=전체 미선택, null=일부 선택.
    /// </summary>
    public bool? AreAllVisibleRecipesSelected
    {
        get => _areAllVisibleRecipesSelected;
        private set => SetProperty(ref _areAllVisibleRecipesSelected, value);
    }

    public ICommand LoadEquipmentSummaryCommand { get; }
    public ICommand SearchRecipesCommand { get; }
    public ICommand LoadRecipeDetailCommand { get; }
    public ICommand ChooseBackupPathCommand { get; }
    public AsyncRelayCommand BackupSelectedRecipesCommand { get; }
    public AsyncRelayCommand DeleteSelectedRecipesCommand { get; }
    public ICommand CopyEquipmentSummaryCommand { get; }
    public ICommand CopyRecipeListCommand { get; }
    public ICommand CopyRecipeDetailCommand { get; }
    public ICommand ExportRecipeListCsvCommand { get; }
    public ICommand ExportEquipmentSummaryExcelCommand { get; }
    public ICommand ExportRecipeListExcelCommand { get; }
    public ICommand ExportRecipeDetailExcelCommand { get; }
    public ICommand CopySelectedRecipesCommand { get; }
    public ICommand ExportSelectedRecipesExcelCommand { get; }
    public ICommand RemoveSelectedRecipeCommand { get; }
    public ICommand SetVisibleRecipeSelectionCommand { get; }
    public ICommand ClearSelectedRecipesCommand { get; }
    public ICommand ToggleEquipmentPanelCommand { get; }
    public ICommand ToggleProcessingPanelCommand { get; }
    public ICommand CloseEquipmentPanelCommand { get; }
    public ICommand CloseProcessingPanelCommand { get; }
    public ICommand CloseSidePanelsCommand { get; }

    private async Task LoadEquipmentSummaryAsync()
    {
        try
        {
            StatusMessage = $"{SelectedFab} 장비별 Recipe 현황 조회 중...";
            var response = await _api.GetEquipmentSummaryAsync();
            EquipmentSummaryView = response.Data?.ToDataTable().DefaultView;
            RecipeListView = null;
            RecipeDetailView = null;
            SelectedRecipes.Clear();
            StatusMessage = response.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = "장비별 Recipe 현황 조회 실패";
            _dialog.ShowError(ex.Message);
        }
    }

    private async Task SearchRecipesAsync()
    {
        try
        {
            var equipmentIds = GetSelectedEquipmentIds();
            if (equipmentIds.Count == 0)
            {
                _dialog.ShowInfo("조회할 장비를 먼저 체크하세요.");
                return;
            }

            StatusMessage = "Recipe 리스트 조회 중...";
            var request = new RecipeListRequest { EquipmentIds = equipmentIds };
            var response = await _api.GetRecipeListAsync(request);
            var table = response.Data?.ToDataTable();
            BuildRecipeFilters(table);
            RecipeListView = table?.DefaultView;
            RecipeDetailView = null;
            SelectedRecipes.Clear();
            ApplyLocalRecipeFilters();
            StatusMessage = response.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = "Recipe 리스트 조회 실패";
            _dialog.ShowError(ex.Message);
        }
    }

    private async Task LoadRecipeDetailAsync(object? selectedRow)
    {
        try
        {
            if (selectedRow is not DataRowView row) return;
            var eqpId = Convert.ToString(row["equip"]) ?? string.Empty;
            var recipeName = Convert.ToString(row["recipe"]) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(eqpId) || string.IsNullOrWhiteSpace(recipeName)) return;

            StatusMessage = $"Recipe 상세 조회 중: {eqpId} / {recipeName}";
            var response = await _api.GetRecipeDetailAsync(new RecipeDetailRequest { EqpId = eqpId, RecipeName = recipeName });
            RecipeDetailView = response.Data?.ToDataTable().DefaultView;
            StatusMessage = response.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = "Recipe 상세 조회 실패";
            _dialog.ShowError(ex.Message);
        }
    }

    private void ChooseBackupPath()
    {
        var path = _dialog.SelectFolder("Recipe 백업 폴더를 선택하세요.");
        if (!string.IsNullOrWhiteSpace(path)) BackupPath = path;
    }

    private async Task BackupSelectedRecipesAsync()
    {
        try
        {
            SyncSelectedRecipesFromRecipeList();
            if (!HasBackupPath)
            {
                _dialog.ShowInfo("백업 경로를 먼저 지정하세요.");
                return;
            }
            if (SelectedRecipes.Count == 0)
            {
                _dialog.ShowInfo("백업할 Recipe를 먼저 체크하세요.");
                return;
            }
            if (SelectedRecipes.Count > MaxSelectedRecipeCount)
            {
                _dialog.ShowInfo($"한 번에 백업할 수 있는 Recipe는 최대 " +
                    $"{MaxSelectedRecipeCount:N0}개입니다.");
                return;
            }
            if (!_dialog.Confirm($"{SelectedRecipes.Count}개 Recipe를 백업하시겠습니까?")) return;

            var request = new RecipeActionRequest
            {
                BackupPath = BackupPath,
                Items = SelectedRecipes.Select(x => new RecipeActionItem { EqpId = x.EqpId, RecipeName = x.RecipeName }).ToList()
            };
            var response = await _api.BackupRecipesAsync(request);
            var extracted = _archiveService.Extract(response, BackupPath!);
            var manifest = extracted.Manifest;
            StatusMessage = $"Recipe 백업 완료: {manifest.SuccessCount}개, 실패 {manifest.EffectiveFailureCount}개";
            _dialog.ShowInfo($"{StatusMessage}\n저장 위치: {extracted.DestinationDirectory}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Recipe 백업 실패";
            _dialog.ShowError(ex.Message);
        }
    }

    private async Task DeleteSelectedRecipesAsync()
    {
        if (!CanDeleteRecipes)
        {
            _dialog.ShowInfo("Recipe 삭제는 관리자 계정에서만 사용할 수 있습니다.");
            return;
        }

        try
        {
            SyncSelectedRecipesFromRecipeList();
            if (!HasBackupPath)
            {
                _dialog.ShowInfo("백업 경로를 먼저 지정하세요.");
                return;
            }
            if (SelectedRecipes.Count == 0)
            {
                _dialog.ShowInfo("삭제할 Recipe를 먼저 체크하세요.");
                return;
            }
            if (SelectedRecipes.Count > MaxSelectedRecipeCount)
            {
                _dialog.ShowInfo($"한 번에 삭제할 수 있는 Recipe는 최대 " +
                    $"{MaxSelectedRecipeCount:N0}개입니다.");
                return;
            }
            if (!_dialog.Confirm($"{SelectedRecipes.Count}개 Recipe를 삭제하시겠습니까?\n장비에서 직접 삭제되는 작업입니다.")) return;

            var request = new RecipeActionRequest
            {
                BackupPath = BackupPath,
                Items = SelectedRecipes.Select(x => new RecipeActionItem { EqpId = x.EqpId, RecipeName = x.RecipeName }).ToList()
            };
            var response = await _api.DeleteRecipesAsync(request);
            var extracted = _archiveService.Extract(response, BackupPath!);
            var manifest = extracted.Manifest;
            RemoveDeletedRows(manifest.DeletedItems);
            SyncSelectedRecipesFromRecipeList();
            StatusMessage = $"Recipe 삭제 완료: {manifest.DeletedCount}개, 실패 {manifest.EffectiveFailureCount}개";
            _dialog.ShowInfo($"{StatusMessage}\n선행 백업 위치: {extracted.DestinationDirectory}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Recipe 삭제 실패";
            _dialog.ShowError(ex.Message);
        }
    }

    private List<string> GetSelectedEquipmentIds()
    {
        if (EquipmentSummaryView == null) return new List<string>();
        return EquipmentSummaryView.Cast<DataRowView>()
            .Where(row => row.Row.Table.Columns.Contains("is_selected") && row["is_selected"] is bool b && b)
            .Select(row => Convert.ToString(row["equip"]) ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
    }

    private void SyncSelectedRecipesFromRecipeList()
    {
        SelectedRecipes.Clear();

        if (RecipeListView?.Table == null)
        {
            return;
        }

        var selectedCount = 0;

        try
        {
            _isBulkUpdatingRecipeSelection = true;

            foreach (DataRow row in RecipeListView.Table.Rows)
            {
                if (!row.Table.Columns.Contains("is_selected") ||
                    row["is_selected"] is not bool isSelected ||
                    !isSelected)
                {
                    continue;
                }

                if (selectedCount >= MaxSelectedRecipeCount)
                {
                    row["is_selected"] = false;
                    continue;
                }

                SelectedRecipes.Add(
                    new RecipeSelectionItem
                    {
                        EqpId =
                            Convert.ToString(row["equip"]) ??
                            string.Empty,

                        RecipeName =
                            Convert.ToString(row["recipe"]) ??
                            string.Empty
                    });

                selectedCount++;
            }
        }
        finally
        {
            _isBulkUpdatingRecipeSelection = false;
        }
    }

    private void RecipeTable_ColumnChanged(object sender, DataColumnChangeEventArgs e)
    {
        if (!string.Equals(e.Column.ColumnName, "is_selected", StringComparison.OrdinalIgnoreCase)) return;
        if (_isBulkUpdatingRecipeSelection) return;

        var isSelected = e.Row["is_selected"] is bool selected && selected;
        if (isSelected)
        {
            var selectedCount = CountSelectedRecipeRows();

            if (selectedCount > MaxSelectedRecipeCount)
            {
                try
                {
                    _isBulkUpdatingRecipeSelection = true;
                    e.Row["is_selected"] = false;
                }
                finally
                {
                    _isBulkUpdatingRecipeSelection = false;
                }

                SyncSelectedRecipesFromRecipeList();
                UpdateVisibleSelectionState();

                _dialog.ShowInfo(
                    $"처리 Recipe는 최대 " +
                    $"{MaxSelectedRecipeCount:N0}개까지만 선택할 수 있습니다.");

                return;
            }
        }

        SyncSelectedRecipesFromRecipeList();
        UpdateVisibleSelectionState();
    }

    private int CountSelectedRecipeRows()
    {
        if (RecipeListView?.Table == null || !RecipeListView.Table.Columns.Contains("is_selected"))
        {
            return 0;
        }

        var selectedCount = 0;

        foreach (DataRow row in RecipeListView.Table.Rows)
        {
            if (row["is_selected"] is bool selected && selected)
            {
                selectedCount++;
            }
        }
        return selectedCount;
    }

    private void SetVisibleRecipeSelection(bool isSelected)
    {
        if (RecipeListView?.Table == null || !RecipeListView.Table.Columns.Contains("is_selected")) return;

        var selectionLimitReached = false;

        try
        {
            _isBulkUpdatingRecipeSelection = true;

            if (!isSelected)
            {
                foreach (DataRowView rowView in RecipeListView)
                {
                    rowView["is_selected"] = false;
                }
            }
            else
            {
                var selectedCount = CountSelectedRecipeRows();
                foreach (DataRowView rowView in RecipeListView)
                {
                    var alreadySelected = rowView["is_selected"] is bool selected && selected;
                    if (alreadySelected)
                    {
                        continue;
                    }
                    if (selectedCount >= MaxSelectedRecipeCount)
                    {
                        selectionLimitReached = true;
                        break;
                    }
                    rowView["is_selected"] = true;
                    selectedCount++;
                }
            }

        }
        finally
        {
            _isBulkUpdatingRecipeSelection = false;
        }

        SyncSelectedRecipesFromRecipeList();
        UpdateVisibleSelectionState();

        if (selectionLimitReached)
        {
            _dialog.ShowInfo(
                $"처리 Recipe는 최대 " +
                $"{MaxSelectedRecipeCount:N0}개까지만 선택할 수 있습니다.\n\n" +
                $"현재 {SelectedRecipes.Count:N0}개가 선택되었습니다.");
        }
    }

    private void ClearSelectedRecipes()
    {
        if (RecipeListView?.Table == null)
        {
            SelectedRecipes.Clear();
            AreAllVisibleRecipesSelected = false;
            return;
        }

        try
        {
            _isBulkUpdatingRecipeSelection = true;
            if (RecipeListView.Table.Columns.Contains("is_selected"))
            {
                foreach (DataRow row in RecipeListView.Table.Rows)
                    row["is_selected"] = false;
            }
        }
        finally
        {
            _isBulkUpdatingRecipeSelection = false;
        }

        SelectedRecipes.Clear();
        UpdateVisibleSelectionState();
    }

    private void UpdateVisibleSelectionState()
    {
        if (RecipeListView == null || RecipeListView.Count == 0)
        {
            AreAllVisibleRecipesSelected = false;
            return;
        }

        var selectedCount = 0;
        foreach (DataRowView rowView in RecipeListView)
        {
            if (rowView.Row.Table.Columns.Contains("is_selected")
                && rowView["is_selected"] is bool selected
                && selected)
            {
                selectedCount++;
            }
        }

        AreAllVisibleRecipesSelected = selectedCount == 0
            ? false
            : selectedCount == RecipeListView.Count
                ? true
                : null;
    }

    private void RemoveSelectedRecipe(object? item)
    {
        if (item is not RecipeSelectionItem target) return;

        if (RecipeListView != null)
        {
            foreach (var row in RecipeListView.Table.Rows.Cast<DataRow>())
            {
                var eqpId = Convert.ToString(row["equip"]) ?? string.Empty;
                var recipeName = Convert.ToString(row["recipe"]) ?? string.Empty;
                if (eqpId == target.EqpId && recipeName == target.RecipeName && row.Table.Columns.Contains("is_selected"))
                {
                    row["is_selected"] = false;
                    break;
                }
            }
        }

        var existing = SelectedRecipes.FirstOrDefault(x => x.EqpId == target.EqpId && x.RecipeName == target.RecipeName);
        if (existing != null)
            SelectedRecipes.Remove(existing);
    }

    private void BuildRecipeFilters(DataTable? table)
    {
        foreach (var filter in RecipeFilters)
            filter.PropertyChanged -= RecipeFilter_PropertyChanged;
        RecipeFilters.Clear();

        if (table == null) return;

        foreach (DataColumn column in table.Columns)
        {
            if (string.Equals(column.ColumnName, "is_selected", StringComparison.OrdinalIgnoreCase)
                || RecipeListHiddenColumns.Contains(column.ColumnName, StringComparer.OrdinalIgnoreCase))
                continue;

            var filter = new RecipeColumnFilter
            {
                ColumnName = column.ColumnName,
                DisplayName = ToDisplayName(column.ColumnName),
                IsComboBox = string.Equals(column.ColumnName, "matching", StringComparison.OrdinalIgnoreCase)
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

            filter.PropertyChanged += RecipeFilter_PropertyChanged;
            RecipeFilters.Add(filter);
        }
    }

    private void RecipeFilter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecipeColumnFilter.EffectiveValue))
            ApplyLocalRecipeFilters();
    }

    private void ApplyLocalRecipeFilters()
    {
        if (RecipeListView == null) return;

        var expressions = new List<string>();
        foreach (var filter in RecipeFilters)
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

        RecipeListView.RowFilter = string.Join(" AND ", expressions);
        OnPropertyChanged(nameof(RecipeListRowCount));
        UpdateVisibleSelectionState();
    }

    private static string EscapeColumnName(string value) => value.Replace("]", "]]", StringComparison.Ordinal);
    private static string EscapeFilterValue(string value) => value.Replace("'", "''", StringComparison.Ordinal);
    private static string EscapeLikeValue(string value)
        => EscapeFilterValue(value)
            .Replace("[", "[[]", StringComparison.Ordinal)
            .Replace("%", "[%]", StringComparison.Ordinal)
            .Replace("*", "[*]", StringComparison.Ordinal);

    private static string ToDisplayName(string key)
        => key.Replace("_", " ", StringComparison.Ordinal)
            .Trim();

    private void RemoveDeletedRows(IEnumerable<RecipeSelectionItem> deletedItems)
    {
        if (RecipeListView == null) return;
        var targets = deletedItems.Select(x => $"{x.EqpId}|{x.RecipeName}").ToHashSet();
        var rows = RecipeListView.Table.Rows.Cast<DataRow>().ToList();
        foreach (var row in rows)
        {
            var eqpColumn = row.Table.Columns.Contains("equip") ? "equip" : "eqp_id";
            var recipeColumn = row.Table.Columns.Contains("recipe") ? "recipe" : "recipe_name";
            var key = $"{row[eqpColumn]}|{row[recipeColumn]}";
            if (targets.Contains(key)) RecipeListView.Table.Rows.Remove(row);
        }
        OnPropertyChanged(nameof(RecipeListRowCount));
    }


    private void SelectedRecipes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedRecipeRowCount));
        OnPropertyChanged(nameof(SelectedRecipeCountText));
    }

    private DataTable CreateSelectedRecipesTable()
    {
        var table = new DataTable("SelectedRecipes");
        table.Columns.Add("eqp_id", typeof(string));
        table.Columns.Add("recipe_name", typeof(string));
        foreach (var item in SelectedRecipes)
            table.Rows.Add(item.EqpId, item.RecipeName);
        return table;
    }

    private void CloseSidePanels()
    {
        IsEquipmentPanelOpen = false;
        IsProcessingPanelOpen = false;
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
}
