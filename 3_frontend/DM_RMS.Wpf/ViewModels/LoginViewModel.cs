using DM_RMS.Wpf.Commands;
using DM_RMS.Wpf.Models;
using DM_RMS.Wpf.Services;
using System.Windows.Input;

namespace DM_RMS.Wpf.ViewModels;

public sealed class LoginViewModel : BaseViewModel
{
    private readonly ApiHostProvider _apiHostProvider;
    private readonly IDialogService _dialogs = new DialogService();
    private string _selectedFab = "fab1";
    private string _userId = "";
    //private string _password = "admin";
    private string _password = "";
    private string _statusMessage = "";
    private bool _isBusy;

    public LoginViewModel()
    {
        _apiHostProvider = new ApiHostProvider();
        LoginCommand = new AsyncRelayCommand(_ => LoginAsync(), _ => !IsBusy);
        OpenRegisterCommand = new RelayCommand(_ => RegisterRequested?.Invoke(this, EventArgs.Empty));
        CancelCommand = new RelayCommand(_ => CancelRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler<UserSession>? LoginSucceeded;
    public event EventHandler? RegisterRequested;
    public event EventHandler? CancelRequested;

    public IReadOnlyList<string> FabOptions { get; } = new[] { "fab1", "fab2" };

    public string SelectedFab
    {
        get => _selectedFab;
        set => SetProperty(ref _selectedFab, value);
    }

    public string UserId
    {
        get => _userId;
        set => SetProperty(ref _userId, value);
    }

    public string BoundPassword
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                if (LoginCommand is AsyncRelayCommand asyncLogin)
                    asyncLogin.RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand LoginCommand { get; }
    public ICommand OpenRegisterCommand { get; }
    public ICommand CancelCommand { get; }

    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(BoundPassword))
        {
            System.Windows.MessageBox.Show(
                "아이디와 패스워드를 입력하세요.",
                "로그인",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        await UiOperationRunner.RunAsync(async () =>
        {
            string baseUrl = _apiHostProvider.GetBaseUrl(SelectedFab);
            var api = new DmRmsApiService(new ApiClient(baseUrl));

            var response = await api.LoginAsync(new LoginRequest
            {
                UserId = UserId.Trim(),
                Password = BoundPassword
            });

            var data = response.Data ?? throw new InvalidOperationException("로그인 응답 데이터가 없습니다.");
            if (!string.Equals(data.Fab, SelectedFab, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"선택한 FAB({SelectedFab})과 연결된 API 서버의 FAB({data.Fab})가 일치하지 않습니다. api-hosts.json 설정을 확인하세요.");
            }

            var usageSessionId = Guid.NewGuid().ToString("D");
            var sessionWithoutUsage = new UserSession
            {
                Fab = data.Fab,
                UserId = data.UserId,
                DisplayName = data.DisplayName,
                Role = data.Role,
                ApiBaseUrl = baseUrl
            };

            try
            {
                var usageApi = new DmRmsApiService(new ApiClient(baseUrl, 20));
                var usageResponse = await usageApi.StartUsageSessionAsync(
                    ClientEnvironmentInfo.CreateUsageStartRequest(sessionWithoutUsage, usageSessionId));
                usageSessionId = usageResponse.Data?.SessionId ?? usageSessionId;
            }
            catch (Exception usageException)
            {
                usageSessionId = string.Empty;
                System.Windows.MessageBox.Show(
                    $"로그인은 완료되었지만 시스템 사용 이력 저장에 실패했습니다.\n{usageException.Message}",
                    "사용 이력 저장 실패",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }

            var session = new UserSession
            {
                Fab = sessionWithoutUsage.Fab,
                UserId = sessionWithoutUsage.UserId,
                DisplayName = sessionWithoutUsage.DisplayName,
                Role = sessionWithoutUsage.Role,
                ApiBaseUrl = sessionWithoutUsage.ApiBaseUrl,
                UsageSessionId = usageSessionId
            };

            StatusMessage = "로그인 성공";
            LoginSucceeded?.Invoke(this, session);
        }, _dialogs, value => IsBusy = value, value => StatusMessage = value,
           "로그인 요청 중...", "로그인 실패");
    }
}
