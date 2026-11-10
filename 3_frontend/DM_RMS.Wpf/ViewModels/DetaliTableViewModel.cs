using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Windows.Input;
using DM_RMS.Wpf.Commands;
using DM_RMS.Wpf.Helpers;
using DM_RMS.Wpf.Models;
using DM_RMS.Wpf.Services;

namespace DM_RMS.Wpf.ViewModels;

public sealed class DetaliTableViewModel : BaseViewModel
{
    private readonly DmRmsApiService _api;
    private readonly IDialogService _dialog;
    private DataView? _detailView;
    private Dictionary<string, ColumnVisualizationInfo> _columnMetadata = new(StringComparer.OrdinalIgnoreCase);
    private bool _isLoading;
    private int _rowCount;
    private bool _isNonMatchingTree;

    public DetaliTableViewModel(DmRmsApiService api, IDialogService dialog, string searchData)
    {
        _api = api;
        _dialog = dialog;
        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync(searchData));
        CopyCommand = new RelayCommand(_ => DataTableExportHelper.CopyToClipboard(DetailView, GetHiddenColumns(searchData)));
        ExportExcelCommand = new RelayCommand(_ => ExportExcel(searchData));
        _ = LoadAsync(searchData);
    }

    public ObservableCollection<RecipeColumnFilter> ColumnFilters { get; } = new();

    public DataView? DetailView
    {
        get => _detailView;
        private set => SetProperty(ref _detailView, value);
    }

    public Dictionary<string, ColumnVisualizationInfo> ColumnMetadata
    {
        get => _columnMetadata;
        private set => SetProperty(ref _columnMetadata, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public int RowCount
    {
        get => _rowCount;
        private set => SetProperty(ref _rowCount, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand ExportExcelCommand { get; }

    public async Task LoadAsync(string searchData)
    {
        try
        {
            IsLoading = true;
            ApiResponse<CusomDetailTableResponse> response = null;
            switch (searchData)
            {
                case "non_matching":
                    response = await _api.GetNonMatchingDetailAsync();
                    break;
                case "no_master":
                    response = await _api.GetNoMasterDetailAsync();
                    break;
                case "exclude":
                    response = await _api.GetExcludeDetailAsync();
                    break;
            }

            var data = response.Data;
            if (data is null)
            {
                DetailView = null;
                RowCount = 0;
                return;
            }

            var table = data.Table.ToDataTable();
            _isNonMatchingTree = searchData == "non_matching";
            if (_isNonMatchingTree) table = BuildNonMatchingTreeTable(table, data.ColumnMetadata);
            table.CaseSensitive = false;
            DetailView = table.DefaultView;
            RowCount = data.Table.RowCount;
            ColumnMetadata = new Dictionary<string, ColumnVisualizationInfo>(data.ColumnMetadata, StringComparer.OrdinalIgnoreCase);
            BuildFilters(data.Table.Columns);

        }
        catch (Exception ex)
        {
            _dialog.ShowError(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }


    private static DataTable BuildNonMatchingTreeTable(
        DataTable source,
        IReadOnlyDictionary<string, ColumnVisualizationInfo> metadata)
    {
        const string rowTypeColumn = "_row_type";
        const string groupKeyColumn = "_tree_group_key";
        const string visibleColumn = "_tree_visible";

        if (!source.Columns.Contains(rowTypeColumn)) source.Columns.Add(rowTypeColumn, typeof(string));
        if (!source.Columns.Contains(groupKeyColumn)) source.Columns.Add(groupKeyColumn, typeof(string));
        if (!source.Columns.Contains(visibleColumn)) source.Columns.Add(visibleColumn, typeof(bool));

        var parentColumns = metadata
            .Where(x => string.Equals(x.Value.TreeRole, "parent", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Key)
            .Where(source.Columns.Contains)
            .ToArray();

        if (parentColumns.Length == 0)
        {
            parentColumns = new[] { "master_key", "model", "equip", "device", "recipe", "dt_edit_time", "tech_prod", "tech", "prod_group", "level", "layer", "mode" }
                .Where(source.Columns.Contains)
                .ToArray();
        }

        var result = source.Clone();
        string? previousKey = null;
        foreach (DataRow sourceRow in source.Rows)
        {
            var key = string.Join("\u001f", parentColumns.Select(c => Convert.ToString(sourceRow[c]) ?? string.Empty));
            if (!string.Equals(previousKey, key, StringComparison.Ordinal))
            {
                var header = result.NewRow();
                foreach (var column in parentColumns) header[column] = sourceRow[column];
                header[rowTypeColumn] = "HEADER";
                header[groupKeyColumn] = key;
                header[visibleColumn] = true;
                result.Rows.Add(header);
                previousKey = key;
            }

            var detail = result.NewRow();
            foreach (DataColumn column in source.Columns)
            {
                detail[column.ColumnName] = column.ColumnName switch
                {
                    rowTypeColumn => "DETAIL",
                    groupKeyColumn => key,
                    visibleColumn => true,
                    _ => sourceRow[column.ColumnName]
                };
            }
            result.Rows.Add(detail);
        }
        return result;
    }

    private void BuildFilters(IEnumerable<DataFrameColumn> columns)
    {
        foreach (var existing in ColumnFilters)
            existing.PropertyChanged -= OnFilterChanged;
        ColumnFilters.Clear();

        foreach (var column in columns)
        {
            if (!ColumnMetadata.TryGetValue(column.Name, out var metadata)) continue;
            var filter = new RecipeColumnFilter
            {
                ColumnName = column.Name,
                DisplayName = string.IsNullOrWhiteSpace(metadata.Header) ? column.Name : metadata.Header,
                IsComboBox = metadata.Filter.Type.Equals("combobox", StringComparison.OrdinalIgnoreCase)
            };
            if (filter.IsComboBox) filter.Options.Add(string.Empty);
            foreach (var option in metadata.Filter.Options.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
                filter.Options.Add(option);
            filter.PropertyChanged += OnFilterChanged;
            ColumnFilters.Add(filter);
        }
    }

    private void OnFilterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecipeColumnFilter.EffectiveValue)) ApplyFilters();
    }

    private void ApplyFilters()
    {
        if (DetailView is null) return;

        if (_isNonMatchingTree && DetailView.Table.Columns.Contains("_tree_visible"))
        {
            ApplyTreeFilters();
            return;
        }

        var conditions = new List<string>();
        foreach (var filter in ColumnFilters)
        {
            var value = filter.EffectiveValue?.Trim();
            if (string.IsNullOrWhiteSpace(value)) continue;
            var column = filter.ColumnName.Replace("]", "]]", StringComparison.Ordinal);
            var escaped = value.Replace("'", "''", StringComparison.Ordinal)
                               .Replace("[", "[[]", StringComparison.Ordinal)
                               .Replace("%", "[%]", StringComparison.Ordinal)
                               .Replace("*", "[*]", StringComparison.Ordinal);
            conditions.Add(filter.IsComboBox
                ? $"Convert([{column}], 'System.String') = '{escaped}'"
                : $"Convert([{column}], 'System.String') LIKE '%{escaped}%'");
        }
        DetailView.RowFilter = string.Join(" AND ", conditions);
        RowCount = DetailView.Count;
    }

    private void ApplyTreeFilters()
    {
        if (DetailView is null) return;
        var table = DetailView.Table;
        const string rowTypeColumn = "_row_type";
        const string groupKeyColumn = "_tree_group_key";
        const string visibleColumn = "_tree_visible";

        var activeFilters = ColumnFilters
            .Where(x => !string.IsNullOrWhiteSpace(x.EffectiveValue))
            .ToList();

        if (activeFilters.Count == 0)
        {
            foreach (DataRow row in table.Rows) row[visibleColumn] = true;
            DetailView.RowFilter = $"[{visibleColumn}] = true";
            RowCount = table.Rows.Cast<DataRow>().Count(x => string.Equals(Convert.ToString(x[rowTypeColumn]), "DETAIL", StringComparison.OrdinalIgnoreCase));
            return;
        }

        var parentFilters = activeFilters
            .Where(x => ColumnMetadata.TryGetValue(x.ColumnName, out var metadata)
                && string.Equals(metadata.TreeRole, "parent", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var detailFilters = activeFilters.Except(parentFilters).ToList();

        table.BeginLoadData();
        try
        {
            foreach (DataRow row in table.Rows) row[visibleColumn] = false;

            foreach (var group in table.Rows.Cast<DataRow>().GroupBy(x => Convert.ToString(x[groupKeyColumn]) ?? string.Empty))
            {
                var header = group.FirstOrDefault(x => string.Equals(Convert.ToString(x[rowTypeColumn]), "HEADER", StringComparison.OrdinalIgnoreCase));
                var details = group.Where(x => string.Equals(Convert.ToString(x[rowTypeColumn]), "DETAIL", StringComparison.OrdinalIgnoreCase)).ToList();
                if (details.Count == 0) continue;

                var parentProbe = header ?? details[0];
                if (!parentFilters.All(filter => MatchesFilter(parentProbe, filter))) continue;

                var matchingDetails = detailFilters.Count == 0
                    ? details
                    : details.Where(row => detailFilters.All(filter => MatchesFilter(row, filter))).ToList();

                if (matchingDetails.Count == 0) continue;
                if (header is not null) header[visibleColumn] = true;
                foreach (var row in matchingDetails) row[visibleColumn] = true;
            }
        }
        finally
        {
            table.EndLoadData();
        }

        DetailView.RowFilter = $"[{visibleColumn}] = true";
        RowCount = DetailView.Cast<DataRowView>().Count(x => string.Equals(Convert.ToString(x[rowTypeColumn]), "DETAIL", StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesFilter(DataRow row, RecipeColumnFilter filter)
    {
        if (!row.Table.Columns.Contains(filter.ColumnName)) return false;
        var expected = filter.EffectiveValue?.Trim() ?? string.Empty;
        var actual = row.IsNull(filter.ColumnName) ? string.Empty : Convert.ToString(row[filter.ColumnName]) ?? string.Empty;

        if (filter.IsComboBox)
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

        return actual.Contains(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> GetHiddenColumns(string searchData)
        => string.Equals(searchData, "non_matching", StringComparison.OrdinalIgnoreCase)
            ? new[] { "_row_type", "_tree_group_key", "_tree_visible" }
            : Array.Empty<string>();

    private void ExportExcel(string searchData)
    {
        string fileName = string.Empty;
        string sheetName = string.Empty;
        List<string> hiddenColumns = null;
        DataView dataView = DetailView.ToTable().DefaultView;
        switch (searchData)
        {
            case "non_matching":
                fileName = "non_matching_recipe_list.xlsx";
                sheetName = "NonMatchingRecipeDetail";
                hiddenColumns = GetHiddenColumns(searchData).ToList();
                dataView.RowFilter = "_row_type <> 'HEADER'";
                break;
            case "no_master":
                fileName = "no_master_recipe_list.xlsx";
                sheetName = "NoMasterRecipeDetail";
                break;
            case "exclude":
                fileName = "exclude_recipe_list.xlsx";
                sheetName = "ExcludeRecipeDetail";
                break;
        }
        var path = _dialog.SelectSaveFile("Excel 파일 (*.xlsx)|*.xlsx", fileName);
        if (string.IsNullOrWhiteSpace(path)) return;
        DataTableExportHelper.SaveExcel(dataView, path, sheetName, hiddenColumns);
        _dialog.ShowInfo("Excel 저장이 완료되었습니다.");
    }
}
