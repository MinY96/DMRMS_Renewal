using DM_RMS.Wpf.Models;
using DM_RMS.Wpf.Services;

namespace DM_RMS.Wpf.ViewModels;

public sealed class MainViewModel : BaseViewModel
{
    private readonly DmRmsApiService _usageApi;
    private string _selectedFab;

    public MainViewModel(UserSession session)
    {
        Session = session;
        _selectedFab = session.Fab;

        var api = new DmRmsApiService(new ApiClient(session.ApiBaseUrl), new ApiClient(session.ApiBaseUrl, 300));
        _usageApi = new DmRmsApiService(new ApiClient(session.ApiBaseUrl));
        var dialog = new DialogService();
        var isAdministrator = string.Equals(session.Role, "admin", StringComparison.OrdinalIgnoreCase);

        Recipe = new RecipeManagementViewModel(api, dialog, new RecipeArchiveService(), new AppSettingsService(), isAdministrator);
        Master = new MasterManagementViewModel(api, dialog, isAdministrator);
        Dashboard = new DashboardViewModel(api, dialog);
        NonMatchingRecipe = new DetaliTableViewModel(api, dialog, "non_matching");
        NoMasterRecipe = new DetaliTableViewModel(api, dialog, "no_master");
        ExcludeRecipe = new DetaliTableViewModel(api, dialog, "exclude");

        Recipe.SelectedFab = SelectedFab;
        Master.SelectedFab = SelectedFab;
        Dashboard.SelectedFab = SelectedFab;        
    }

    public UserSession Session { get; }
    public RecipeManagementViewModel Recipe { get; }
    public MasterManagementViewModel Master { get; }
    public DashboardViewModel Dashboard { get; }
    public DetaliTableViewModel NonMatchingRecipe { get; }
    public DetaliTableViewModel NoMasterRecipe { get; }
    public DetaliTableViewModel ExcludeRecipe { get; }

    public string SelectedFab
    {
        get => _selectedFab;
        private set
        {
            if (SetProperty(ref _selectedFab, value))
            {
                Recipe.SelectedFab = value;
                Master.SelectedFab = value;
                Dashboard.SelectedFab = value;
            }
        }
    }

    public bool IsAdministrator => string.Equals(
        Session.Role,
        "admin",
        StringComparison.OrdinalIgnoreCase);

    public async Task EndUsageSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(Session.UsageSessionId))
            return;

        try
        {
            await _usageApi.EndUsageSessionAsync(new UsageSessionEndRequest
            {
                SessionId = Session.UsageSessionId,
                UserId = Session.UserId
            });
        }
        catch
        {
            // 종료 시 이력 저장 실패가 프로그램 종료 자체를 막지 않도록 합니다.
        }
    }

    public string HeaderTitle => $"DM_RMS 통합 프로그램 · {SelectedFab.ToUpperInvariant()}";
    public string UserDisplayText => $"{Session.DisplayName} / {(IsAdministrator ? "관리자" : "사용자")}";
}
