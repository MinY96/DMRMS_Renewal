using DM_RMS.Wpf.Commands;
using DM_RMS.Wpf.Models;
using DM_RMS.Wpf.Services;
using System.Windows.Input;

namespace DM_RMS.Wpf.ViewModels;

public sealed class RegisterViewModel : BaseViewModel
{
    private readonly ApiHostProvider _apiHostProvider;
    private readonly IDialogService _dialogs = new DialogService();
    private string _selectedFab = "fab1";
    private string _userId = string.Empty;
    private string _password = string.Empty;
    private string _displayName = string.Empty;
    private string _statusMessage = "신규 사용자 정보를 입력하세요.";
    private bool _isBusy;

    public RegisterViewModel(string selectedFab)
    {
        _apiHostProvider = new ApiHostProvider();
        _selectedFab = selectedFab;
        RegisterCommand = new AsyncRelayCommand(_ => RegisterAsync(), _ => !IsBusy);
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? RegisterSucceeded;
    public event EventHandler? CloseRequested;

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

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
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
                if (RegisterCommand is AsyncRelayCommand asyncRegister)
                    asyncRegister.RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand RegisterCommand { get; }
    public ICommand CloseCommand { get; }

    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(Password))
        {
            System.Windows.MessageBox.Show(
                "아이디와 패스워드를 입력하세요.",
                "신규 등록",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        await UiOperationRunner.RunAsync(async () =>
        {
            string baseUrl = _apiHostProvider.GetBaseUrl(SelectedFab);
            var api = new DmRmsApiService(new ApiClient(baseUrl));

            var response = await api.RegisterAsync(new RegisterRequest
            {
                UserId = UserId.Trim(),
                Password = Password,
                DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? UserId.Trim() : DisplayName.Trim()
            });

            StatusMessage = "신규 등록 완료";
            System.Windows.MessageBox.Show(
                response.Message,
                "신규 등록",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

            RegisterSucceeded?.Invoke(this, EventArgs.Empty);
        }, _dialogs, value => IsBusy = value, value => StatusMessage = value,
           "신규 등록 요청 중...", "신규 등록 실패");
    }
}
