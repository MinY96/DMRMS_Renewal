using System.Text.Json;
using DM_RMS.Wpf.Models;

namespace DM_RMS.Wpf.Services;

public sealed class DmRmsApiService
{
    private readonly ApiClient _api;
    private readonly ApiClient _longtermApi;

    public DmRmsApiService(ApiClient api, ApiClient longtermApi = null)
    {
        _api = api;
        _longtermApi = longtermApi;
    }

    public Task<ApiResponse<LoginResult>> LoginAsync(LoginRequest request)
        => _api.PostAsync<LoginResult>("/api/auth/login", request);

    public Task<ApiResponse<RegisterResult>> RegisterAsync(RegisterRequest request)
        => _api.PostAsync<RegisterResult>("/api/auth/register", request);

    public Task<ApiResponse<UsageSessionResult>> StartUsageSessionAsync(UsageSessionStartRequest request)
        => _api.PostAsync<UsageSessionResult>("/api/usage/start", request);

    public Task<ApiResponse<UsageSessionResult>> EndUsageSessionAsync(UsageSessionEndRequest request)
        => _api.PostAsync<UsageSessionResult>("/api/usage/end", request);

    public Task<ApiResponse<DataFrameResponse>> GetEquipmentSummaryAsync()
        => _api.GetAsync<DataFrameResponse>("/api/recipe/equipment-summary");

    public Task<ApiResponse<DataFrameResponse>> GetRecipeListAsync(RecipeListRequest request)
        => _api.PostAsync<DataFrameResponse>("/api/recipe/list", request);

    public Task<ApiResponse<DataFrameResponse>> GetRecipeDetailAsync(RecipeDetailRequest request)
        => _api.PostAsync<DataFrameResponse>("/api/recipe/detail", request);

    public Task<DownloadFileResult> BackupRecipesAsync(RecipeActionRequest request)
        => _api.PostDownloadFileAsync("/api/recipe/backup", request);

    public Task<DownloadFileResult> DeleteRecipesAsync(RecipeActionRequest request)
        => _api.PostDownloadFileAsync("/api/recipe/delete", request);

    public Task<byte[]> DownloadRecipeReportAsync()
        => _api.DownloadFileAsync("/api/recipe/report");

    public Task<ApiResponse<List<string>>> GetMasterModelOptionsAsync()
        => _api.GetAsync<List<string>>("/api/master/models");

    public Task<ApiResponse<JsonElement>> GetMasterFilterOptionsAsync(string model)
        => _api.GetAsync<JsonElement>($"/api/master/filter-options?model={Uri.EscapeDataString(model)}");

    public Task<ApiResponse<DataFrameResponse>> GetMasterListAsync(MasterListRequest request)
        => _api.PostAsync<DataFrameResponse>("/api/master/list", request);

    public Task<ApiResponse<MasterDetailResponse>> GetMasterDetailAsync(string masterKey)
        => _api.GetAsync<MasterDetailResponse>($"/api/master/detail/{Uri.EscapeDataString(masterKey)}");

    public Task<ApiResponse<JsonElement>> UpdateMasterDetailAsync(string masterKey, MasterUpdateRequest request)
        => _api.PutAsync<JsonElement>($"/api/master/detail/{Uri.EscapeDataString(masterKey)}", request);

    public Task<ApiResponse<JsonElement>> DeleteMasterAsync(string masterKey)
        => _api.DeleteAsync<JsonElement>($"/api/master/detail/{Uri.EscapeDataString(masterKey)}");

    public Task<ApiResponse<List<MasterParameterDto>>> GetMasterParameterOptionsAsync(string model)
        => _api.GetAsync<List<MasterParameterDto>>($"/api/master/parameters/options?model={Uri.EscapeDataString(model)}");

    public Task<ApiResponse<MasterDetailResponse>> GetNewMasterTemplateAsync(string model)
        => _api.GetAsync<MasterDetailResponse>($"/api/master/template?model={Uri.EscapeDataString(model)}");

    public Task<ApiResponse<JsonElement>> CreateMasterAsync(string model, MasterCreateRequest request)
        => _api.PostAsync<JsonElement>($"/api/master/create?model={Uri.EscapeDataString(model)}", request);

    public Task<ApiResponse<List<string>>> GetDashboardModelOptionsAsync()
    {
        return _api.GetAsync<List<string>>(
            "/api/dashboard/models"
        );
    }

    public Task<ApiResponse<List<string>>> GetDashboardEquipmentOptionsAsync(
        string? model = null)
    {
        string path = AppendDashboardFilter(
            "/api/dashboard/equipments",
            model,
            equipment: null);

        return _api.GetAsync<List<string>>(path);
    }

    public Task<ApiResponse<DashboardSummaryResponse>> GetDashboardSummaryAsync(
        string? model = null,
        string? equipment = null)
    {
        string path = AppendDashboardFilter(
            "/api/dashboard/summary",
            model,
            equipment);

        return _api.GetAsync<DashboardSummaryResponse>(path);
    }

    public Task<ApiResponse<DashboardChartsResponse>> GetDashboardChartsAsync(
        string? model = null,
        string? equipment = null)
    {
        string path = AppendDashboardFilter(
            "/api/dashboard/charts",
            model,
            equipment);

        return _api.GetAsync<DashboardChartsResponse>(path);
    }

    public Task<byte[]> DownloadDashboardDetailAsync(
        string? model = null,
        string? equipment = null)
    {
        string path = AppendDashboardFilter(
            "/api/dashboard/matching-detail/latest",
            model,
            equipment);

        return _longtermApi.DownloadFileAsync(path);
    }

    private static string AppendDashboardFilter(
        string path,
        string? model,
        string? equipment)
    {
        List<string> queryParameters = new();

        if (!string.IsNullOrWhiteSpace(model))
        {
            queryParameters.Add(
                $"model={Uri.EscapeDataString(model)}");
        }

        if (!string.IsNullOrWhiteSpace(equipment))
        {
            queryParameters.Add(
                $"equipment={Uri.EscapeDataString(equipment)}");
        }

        if (queryParameters.Count == 0)
        {
            return path;
        }

        return $"{path}?{string.Join("&", queryParameters)}";
    }

    public Task<ApiResponse<List<string>>> GetNoMasterModelOptionsAsync()
        => _api.GetAsync<List<string>>("/api/no_master_detail/models");

    public Task<ApiResponse<CusomDetailTableResponse>> GetNoMasterDetailAsync(string model)
        => _api.GetAsync<CusomDetailTableResponse>(
            $"/api/no_master_detail/data?model={Uri.EscapeDataString(model)}");

    public Task<ApiResponse<CusomDetailTableResponse>> GetNonMatchingDetailAsync()
    => _api.GetAsync<CusomDetailTableResponse>("/api/non_matching_detail/data");

    public Task<ApiResponse<CusomDetailTableResponse>> GetExcludeDetailAsync()
    => _api.GetAsync<CusomDetailTableResponse>("/api/exclude_detail/data");

    public Task<byte[]> DownloadMasterJsonAsync(string masterKey)
        => _api.DownloadFileAsync($"/api/master/download/{Uri.EscapeDataString(masterKey)}");
}
