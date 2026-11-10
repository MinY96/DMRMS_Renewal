using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Windows.Input;
using DM_RMS.Wpf.Commands;
using DM_RMS.Wpf.Helpers;
using DM_RMS.Wpf.Models;
using DM_RMS.Wpf.Services;

namespace DM_RMS.Wpf.ViewModels;

public sealed class DashboardViewModel : BaseViewModel
{
    private readonly DmRmsApiService _api;
    private readonly IDialogService _dialog;

    private string _selectedFab = "fab1";
    private string _selectedModel = string.Empty;
    private string _selectedEquipment = string.Empty;

    private bool _isInitializing;
    private bool _isLoadingModels;
    private bool _isLoadingEquipments;
    private bool _isLoadingDashboard;

    private long _dashboardRequestVersion;

    private DataView? _matchingRateView;
    private DataView? _updateHistoryView;
    private DataView? _equipmentStatusView;
    private DataView? _excludeStatusView;
    private DataView? _nonMatchingParameterView;

    private string _matchingRateHtml = string.Empty;
    private string _updateHistoryHtml = string.Empty;
    private string _excludeStatusHtml = string.Empty;
    private string _nonMatchingParameterHtml = string.Empty;

    public DashboardViewModel(DmRmsApiService api, IDialogService dialog)
    {
        _api = api;
        _dialog = dialog;

        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());

        DownloadDetailCommand = new AsyncRelayCommand(_ => DownloadDetailAsync());

        CopyMatchingRateCommand = new RelayCommand(
            _ => DataTableExportHelper.CopyToClipboard(
                MatchingRateView));

        ExportMatchingRateCommand = new RelayCommand(
            _ => Export(
                MatchingRateView,
                "matching_rate_history.xlsx",
                "MatchingRateHistory"));

        CopyUpdateHistoryCommand = new RelayCommand(
            _ => DataTableExportHelper.CopyToClipboard(
                UpdateHistoryView));

        ExportUpdateHistoryCommand = new RelayCommand(
            _ => Export(
                UpdateHistoryView,
                "update_history.xlsx",
                "UpdateHistory"));

        CopyExcludeStatusCommand = new RelayCommand(
            _ => DataTableExportHelper.CopyToClipboard(
                ExcludeStatusView));

        ExportExcludeStatusCommand = new RelayCommand(
            _ => Export(
                ExcludeStatusView,
                "exclude_status.xlsx",
                "ExcludeStatus"));

        CopyNonMatchingParaCommand = new RelayCommand(
            _ => DataTableExportHelper.CopyToClipboard(
                NonMatchingParameterView));

        ExportNonMatchingParaCommand = new RelayCommand(
            _ => Export(
                NonMatchingParameterView,
                "non_matching_parameter_status.xlsx",
                "NonMatchingParameter"));

        _ = InitializeAsync();
    }

    public ObservableCollection<DashboardCard> Cards { get; } = new();

    public ObservableCollection<string> ModelOptions { get; } = new();

    public ObservableCollection<string> EquipmentOptions { get; } = new();

    public string SelectedFab
    {
        get => _selectedFab;
        set
        {
            if (!SetProperty(ref _selectedFab, value))
            {
                return;
            }

            _ = InitializeAsync();
        }
    }

    public string SelectedModel
    {
        get => _selectedModel;
        set
        {
            string normalizedValue = NormalizeOption(value);

            if (!SetProperty(
                    ref _selectedModel,
                    normalizedValue))
            {
                return;
            }

            if (_isInitializing || _isLoadingModels)
            {
                return;
            }

            _ = HandleModelChangedAsync();
        }
    }

    public string SelectedEquipment
    {
        get => _selectedEquipment;
        set
        {
            string normalizedValue = NormalizeOption(value);

            if (!SetProperty(
                    ref _selectedEquipment,
                    normalizedValue))
            {
                return;
            }

            if (_isInitializing || _isLoadingEquipments)
            {
                return;
            }

            _ = LoadDashboardAsync();
        }
    }

    public bool IsLoading
    {
        get => _isLoadingModels || _isLoadingEquipments || _isLoadingDashboard;
    }

    public DataView? MatchingRateView
    {
        get => _matchingRateView;
        set => SetProperty(ref _matchingRateView, value);
    }

    public DataView? UpdateHistoryView
    {
        get => _updateHistoryView;
        set => SetProperty(ref _updateHistoryView, value);
    }

    public DataView? EquipmentStatusView
    {
        get => _equipmentStatusView;
        set => SetProperty(ref _equipmentStatusView, value);
    }

    public DataView? ExcludeStatusView
    {
        get => _excludeStatusView;
        set => SetProperty(ref _excludeStatusView, value);
    }

    public DataView? NonMatchingParameterView
    {
        get => _nonMatchingParameterView;
        set => SetProperty(ref _nonMatchingParameterView, value);
    }

    public string MatchingRateHtml
    {
        get => _matchingRateHtml;
        set => SetProperty(ref _matchingRateHtml, value);
    }

    public string UpdateHistoryHtml
    {
        get => _updateHistoryHtml;
        set => SetProperty(ref _updateHistoryHtml, value);
    }

    public string ExcludeStatusHtml
    {
        get => _excludeStatusHtml;
        set => SetProperty(ref _excludeStatusHtml, value);
    }

    public string NonMatchingParameterHtml
    {
        get => _nonMatchingParameterHtml;
        set => SetProperty(ref _nonMatchingParameterHtml, value);
    }

    public ICommand RefreshCommand { get; }

    public ICommand DownloadDetailCommand { get; }

    public ICommand CopyMatchingRateCommand { get; }

    public ICommand ExportMatchingRateCommand { get; }

    public ICommand CopyUpdateHistoryCommand { get; }

    public ICommand ExportUpdateHistoryCommand { get; }

    public ICommand CopyExcludeStatusCommand { get; }

    public ICommand ExportExcludeStatusCommand { get; }

    public ICommand CopyNonMatchingParaCommand { get; }

    public ICommand ExportNonMatchingParaCommand { get; }

    private async Task InitializeAsync()
    {
        if (_isInitializing)
        {
            return;
        }

        try
        {
            _isInitializing = true;

            await LoadModelOptionsAsync();

            // 초기 선택값은 비어 있는 상태로 유지한다.
            SelectedModel = string.Empty;
            EquipmentOptions.Clear();
            SelectedEquipment = string.Empty;
            ClearDashboard();
        }
        catch (Exception exception)
        {
            _dialog.ShowError(exception.Message);
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            await LoadModelOptionsAsync(preserveSelection: true);

            await LoadEquipmentOptionsAsync(preserveSelection: true);

            await LoadDashboardAsync();
        }
        catch (Exception exception)
        {
            _dialog.ShowError(exception.Message);
        }
    }

    private async Task HandleModelChangedAsync()
    {
        try
        {
            SelectedEquipment = string.Empty;
            await LoadEquipmentOptionsAsync();

            // TOTAL은 서버 조회 결과에 포함된 실제 값이다. 하드코딩으로 추가하지 않는다.
            string? total = EquipmentOptions.FirstOrDefault(
                item => item.Equals("TOTAL", StringComparison.OrdinalIgnoreCase));

            SelectedEquipment = total ?? string.Empty;

            if (string.IsNullOrWhiteSpace(SelectedEquipment))
            {
                ClearDashboard();
                return;
            }

            await LoadDashboardAsync();
        }
        catch (Exception exception)
        {
            _dialog.ShowError(exception.Message);
        }
    }

    private async Task LoadModelOptionsAsync(bool preserveSelection = false)
    {
        _isLoadingModels = true;
        OnPropertyChanged(nameof(IsLoading));

        try
        {
            string previousSelection = SelectedModel;

            ApiResponse<List<string>> response =
                await _api.GetDashboardModelOptionsAsync();

            ModelOptions.Clear();

            foreach (string model in response.Data ?? [])
            {
                if (string.IsNullOrWhiteSpace(model))
                {
                    continue;
                }

                if (!ModelOptions.Contains(model))
                {
                    ModelOptions.Add(model);
                }
            }

            if (preserveSelection &&
                ModelOptions.Contains(previousSelection))
            {
                SelectedModel = previousSelection;
            }
            else
            {
                SelectedModel = string.Empty;
            }
        }
        finally
        {
            _isLoadingModels = false;
            OnPropertyChanged(nameof(IsLoading));
        }
    }

    private async Task LoadEquipmentOptionsAsync(bool preserveSelection = false)
    {
        _isLoadingEquipments = true;
        OnPropertyChanged(nameof(IsLoading));

        try
        {
            string previousSelection = SelectedEquipment;

            string? model = ToApiFilter(SelectedModel);

            ApiResponse<List<string>> response =
                await _api.GetDashboardEquipmentOptionsAsync(
                    model);

            EquipmentOptions.Clear();

            foreach (string equipment in response.Data ?? [])
            {
                if (string.IsNullOrWhiteSpace(equipment))
                {
                    continue;
                }

                if (!EquipmentOptions.Contains(equipment))
                {
                    EquipmentOptions.Add(equipment);
                }
            }

            if (preserveSelection &&
                EquipmentOptions.Contains(previousSelection))
            {
                SelectedEquipment = previousSelection;
            }
            else
            {
                SelectedEquipment = string.Empty;
            }
        }
        finally
        {
            _isLoadingEquipments = false;
            OnPropertyChanged(nameof(IsLoading));
        }
    }

    private async Task LoadDashboardAsync()
    {
        if (_isInitializing)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedModel) ||
            string.IsNullOrWhiteSpace(SelectedEquipment))
        {
            ClearDashboard();
            return;
        }

        long requestVersion = Interlocked.Increment(ref _dashboardRequestVersion);

        _isLoadingDashboard = true;
        OnPropertyChanged(nameof(IsLoading));

        try
        {
            string? model = ToApiFilter(SelectedModel);
            string? equipment = ToApiFilter(SelectedEquipment);

            Task<ApiResponse<DashboardSummaryResponse>>
                summaryTask = _api.GetDashboardSummaryAsync(model, equipment);

            Task<ApiResponse<DashboardChartsResponse>>
                chartsTask = _api.GetDashboardChartsAsync(model, equipment);

            await Task.WhenAll(summaryTask, chartsTask);

            /*
             * 사용자가 콤보박스를 빠르게 변경했을 때
             * 이전 요청 결과가 최신 화면을 덮어쓰지 않도록 방지한다.
             */
            if (requestVersion != Interlocked.Read(ref _dashboardRequestVersion))
            {
                return;
            }

            ApiResponse<DashboardSummaryResponse> summaryResponse = await summaryTask;

            ApiResponse<DashboardChartsResponse> chartsResponse = await chartsTask;

            Cards.Clear();

            foreach (DashboardCard card in summaryResponse.Data?.Cards ?? [])
            {
                Cards.Add(card);
            }

            MatchingRateView = summaryResponse.Data?.MatchingRate.ToDataTable().DefaultView;

            UpdateHistoryView = summaryResponse.Data?.UpdateHistory.ToDataTable().DefaultView;

            EquipmentStatusView = summaryResponse.Data?.EquipmentStatus.ToDataTable().DefaultView;

            ExcludeStatusView = summaryResponse.Data?.ExcludeStatus.ToDataTable().DefaultView;

            NonMatchingParameterView = summaryResponse.Data?.NonMatchingParameter.ToDataTable().DefaultView;

            MatchingRateHtml = chartsResponse.Data?.MatchingRateHtml ?? string.Empty;

            UpdateHistoryHtml = chartsResponse.Data?.UpdateHistoryHtml ?? string.Empty;

            ExcludeStatusHtml = chartsResponse.Data?.ExcludeStatusHtml ?? string.Empty;

            NonMatchingParameterHtml = chartsResponse.Data?.NonMatchingParameterHtml ?? string.Empty;
        }
        catch (Exception exception)
        {
            _dialog.ShowError(exception.Message);
        }
        finally
        {
            if (requestVersion == Interlocked.Read(ref _dashboardRequestVersion))
            {
                _isLoadingDashboard = false;
                OnPropertyChanged(nameof(IsLoading));
            }
        }
    }

    private async Task DownloadDetailAsync()
    {
        try
        {
            string modelName =
                NormalizeFileNamePart(SelectedModel);

            string equipmentName =
                NormalizeFileNamePart(
                    SelectedEquipment);

            string defaultFileName =
                $"matching_detail_{modelName}_{equipmentName}.xlsx";

            string? path = _dialog.SelectSaveFile(
                "Excel 파일 (*.xlsx)|*.xlsx",
                defaultFileName);

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            byte[] fileBytes =
                await _api.DownloadDashboardDetailAsync(
                    ToApiFilter(SelectedModel),
                    ToApiFilter(SelectedEquipment));

            File.WriteAllBytes(
                path,
                fileBytes);

            _dialog.ShowInfo(
                "Matching 상세 정보 저장 완료");
        }
        catch (Exception exception)
        {
            _dialog.ShowError(exception.Message);
        }
    }

    private void Export(
        DataView? view,
        string fileName,
        string sheetName)
    {
        string? path = _dialog.SelectSaveFile(
            "Excel 파일 (*.xlsx)|*.xlsx",
            fileName);

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        DataTableExportHelper.SaveExcel(
            view,
            path,
            sheetName);
    }

    private static string NormalizeOption(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string? ToApiFilter(string? value)
    {
        string normalized = NormalizeOption(value);
        return normalized.Length == 0 ? null : normalized;
    }

    private void ClearDashboard()
    {
        Cards.Clear();
        MatchingRateView = null;
        UpdateHistoryView = null;
        EquipmentStatusView = null;
        MatchingRateHtml = string.Empty;
        UpdateHistoryHtml = string.Empty;
    }

    private static string NormalizeFileNamePart(
        string? value)
    {
        string normalized = NormalizeOption(value);
        if (normalized.Length == 0)
        {
            normalized = "EMPTY";
        }

        foreach (
            char invalidCharacter
            in Path.GetInvalidFileNameChars())
        {
            normalized =
                normalized.Replace(
                    invalidCharacter,
                    '_');
        }

        return normalized;
    }
}