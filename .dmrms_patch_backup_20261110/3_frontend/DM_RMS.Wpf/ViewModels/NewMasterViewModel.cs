using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using DM_RMS.Wpf.Commands;
using DM_RMS.Wpf.Models;
using DM_RMS.Wpf.Services;

namespace DM_RMS.Wpf.ViewModels;

public sealed class NewMasterViewModel : BaseViewModel
{
    private readonly DmRmsApiService _api;
    private readonly IDialogService _dialog;
    private MasterParameterDto? _selectedParameterOption;
    private string _statusMessage = "준비";

    public NewMasterViewModel(
        DmRmsApiService api,
        IDialogService dialog,
        IEnumerable<MasterFilterItem> sourceFilters)
    {
        _api = api;
        _dialog = dialog;

        Criteria = new ObservableCollection<MasterFilterItem>(CloneCriteriaFilters(sourceFilters));
        Parameters = new ObservableCollection<MasterParameterItem>();
        ParameterOptions = new ObservableCollection<MasterParameterDto>();

        AddParameterCommand = new RelayCommand(_ => AddSelectedParameter());
        LoadJsonFileCommand = new RelayCommand(_ => LoadJsonFile());
        RemoveParameterCommand = new RelayCommand(parameter => RemoveParameter(parameter));
        CreateMasterCommand = new AsyncRelayCommand(_ => CreateMasterAsync());
        CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
    }

    public ObservableCollection<MasterFilterItem> Criteria { get; }
    public ObservableCollection<MasterParameterItem> Parameters { get; }
    public ObservableCollection<MasterParameterDto> ParameterOptions { get; }

    public MasterParameterDto? SelectedParameterOption
    {
        get => _selectedParameterOption;
        set => SetProperty(ref _selectedParameterOption, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool Created { get; private set; }

    public ICommand AddParameterCommand { get; }
    public ICommand LoadJsonFileCommand { get; }
    public ICommand RemoveParameterCommand { get; }
    public ICommand CreateMasterCommand { get; }
    public ICommand CloseCommand { get; }

    public event Action<bool?>? RequestClose;

    public async Task InitializeAsync()
    {
        try
        {
            StatusMessage = "신규 Master 기본 데이터 조회 중...";

            var parameterResponse = await _api.GetMasterParameterOptionsAsync();
            ParameterOptions.Clear();
            foreach (var option in parameterResponse.Data ?? new List<MasterParameterDto>())
                ParameterOptions.Add(option);

            var templateResponse = await _api.GetNewMasterTemplateAsync();
            Parameters.Clear();
            foreach (var dto in templateResponse.Data?.Parameters ?? new List<MasterParameterDto>())
            {
                var item = MasterParameterItem.FromDto(dto, isEditMode: true);
                Parameters.Add(item);
            }

            StatusMessage = "신규 Master 생성 준비 완료";
        }
        catch (Exception ex)
        {
            StatusMessage = "신규 Master 기본 데이터 조회 실패";
            _dialog.ShowError(ex.Message);
        }
    }

    private void AddSelectedParameter()
    {
        if (SelectedParameterOption == null)
        {
            _dialog.ShowInfo("추가할 Parameter를 선택하세요.");
            return;
        }

        if (Parameters.Any(x => x.ParameterName == SelectedParameterOption.ParameterName))
        {
            _dialog.ShowInfo("이미 추가된 Parameter입니다.");
            return;
        }

        Parameters.Add(MasterParameterItem.FromDto(SelectedParameterOption, isEditMode: true));
    }

    private void RemoveParameter(object? parameter)
    {
        if (parameter is MasterParameterItem item)
            Parameters.Remove(item);
    }

    private void LoadJsonFile()
    {
        try
        {
            var path = _dialog.SelectOpenFile(
                "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
                "Master Recipe JSON 파일 선택");

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var json = File.ReadAllText(
                path,
                System.Text.Encoding.UTF8);

            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var payload = JsonSerializer.Deserialize<MasterJsonFile>(
                json,
                serializerOptions);

            if (payload is null)
            {
                _dialog.ShowError(
                    "JSON 파일을 읽을 수 없습니다.");
                return;
            }

            if (payload.Parameters.Count == 0)
            {
                _dialog.ShowInfo(
                    "JSON 파일에 Parameter 정보가 없습니다.");
                return;
            }

            var loadedItems = new List<MasterParameterItem>();

            foreach (var loadedParameter in payload.Parameters)
            {
                if (string.IsNullOrWhiteSpace(
                        loadedParameter.ParameterName))
                {
                    continue;
                }

                var dto = BuildLoadedParameterDto(
                    loadedParameter);

                var item = MasterParameterItem.FromDto(
                    dto,
                    isEditMode: true);

                loadedItems.Add(item);
            }

            if (loadedItems.Count == 0)
            {
                _dialog.ShowError(
                    "불러올 수 있는 Parameter가 없습니다.");
                return;
            }

            Parameters.Clear();

            foreach (var item in loadedItems)
            {
                Parameters.Add(item);
            }

            StatusMessage =
                $"JSON 불러오기 완료: {payload.MasterKey} "
                + $"({loadedItems.Count:N0}개 Parameter)";
        }
        catch (JsonException ex)
        {
            _dialog.ShowError(
                $"JSON 형식이 올바르지 않습니다.{Environment.NewLine}"
                + ex.Message);
        }
        catch (IOException ex)
        {
            _dialog.ShowError(
                $"JSON 파일을 읽는 중 오류가 발생했습니다.{Environment.NewLine}"
                + ex.Message);
        }
        catch (Exception ex)
        {
            _dialog.ShowError(ex.Message);
        }
    }

    private async Task CreateMasterAsync()
    {
        try
        {
            if (Parameters.Count == 0)
            {
                _dialog.ShowInfo("생성할 Parameter를 하나 이상 추가하세요.");
                return;
            }

            var invalidItems = Parameters.Where(x => !x.Validate()).ToList();
            if (invalidItems.Count > 0)
            {
                _dialog.ShowError(string.Join(Environment.NewLine, invalidItems.Select(x => x.ValidationError)));
                return;
            }

            var criteria = Criteria
                .Where(x => !string.IsNullOrWhiteSpace(x.EffectiveValue))
                .ToDictionary(x => x.Key.Replace("_like", string.Empty, StringComparison.OrdinalIgnoreCase), x => x.EffectiveValue);

            if (criteria.Count == 0)
            {
                _dialog.ShowInfo("좌측 기준정보를 하나 이상 입력하세요.");
                return;
            }

            var request = new MasterCreateRequest
            {
                Criteria = criteria,
                Parameters = Parameters.Select(x => x.ToEdit()).ToList()
            };

            StatusMessage = "신규 Master 생성 요청 중...";
            var response = await _api.CreateMasterAsync(request);
            Created = true;
            StatusMessage = response.Message;
            _dialog.ShowInfo(response.Message);
            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            StatusMessage = "신규 Master 생성 실패";
            _dialog.ShowError(ex.Message);
        }
    }

    private static IEnumerable<MasterFilterItem> CloneCriteriaFilters(IEnumerable<MasterFilterItem> sourceFilters)
    {
        foreach (var source in sourceFilters)
        {
            var clone = new MasterFilterItem
            {
                Key = source.Key,
                DisplayName = source.DisplayName,
                IsLikeFilter = source.IsLikeFilter,
                TextValue = string.Empty,
                SelectedValue = string.Empty
            };

            foreach (var option in source.Options)
                clone.Options.Add(option);

            yield return clone;
        }
    }

    /// <summary>
    /// JSON 파일의 Parameter를 화면 생성용 DTO로 변환합니다.
    ///
    /// 새 JSON은 파일 안의 control_type과 allowed_values를 우선 사용합니다.
    /// 과거 JSON처럼 타입 정보가 없는 경우에는 서버에서 조회한
    /// ParameterOptions의 정의를 보완 정보로 사용합니다.
    /// </summary>
    /// <param name="loadedParameter">
    /// JSON 파일에서 읽은 Parameter 정보입니다.
    /// </param>
    /// <returns>
    /// 사용자 컨트롤 선택과 초기값 설정에 사용할 Parameter DTO입니다.
    /// </returns>
    private MasterParameterDto BuildLoadedParameterDto(
        MasterJsonParameter loadedParameter)
    {
        var serverOption = ParameterOptions.FirstOrDefault(
            option => string.Equals(
                option.ParameterName,
                loadedParameter.ParameterName,
                StringComparison.OrdinalIgnoreCase));

        var dto = loadedParameter.ToDto();

        // 구버전 JSON은 control_type 또는 allowed_values가 없을 수 있습니다.
        // 이 경우 서버의 최신 Parameter 정의를 사용해 하위 호환성을 유지합니다.
        if (serverOption is not null)
        {
            if (string.IsNullOrWhiteSpace(
                    loadedParameter.ControlType))
            {
                dto.ControlType = serverOption.ControlType;
            }

            if (dto.AllowedValues.Count == 0)
            {
                dto.AllowedValues =
                    serverOption.AllowedValues.ToList();
            }
        }

        dto.DisplayValue =
            loadedParameter.DisplayValue ?? string.Empty;

        return dto;
    }
}
