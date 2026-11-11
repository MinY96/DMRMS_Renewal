namespace DM_RMS.Wpf.Services;

/// <summary>
/// API 연동 ViewModel에서 반복되는 Busy / 상태메시지 / 오류 다이얼로그 수명 주기를 통일합니다.
/// UI 컨텍스트를 유지하므로 WPF 바인딩 속성은 호출 UI 스레드에서 갱신합니다.
/// </summary>
public static class UiOperationRunner
{
    public static async Task RunAsync(
        Func<Task> action,
        IDialogService dialogs,
        Action<bool> setBusy,
        Action<string> setStatus,
        string loadingMessage,
        string failedMessage)
    {
        setBusy(true);
        setStatus(loadingMessage);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            setStatus(failedMessage);
            dialogs.ShowError(ex.Message, failedMessage);
        }
        finally
        {
            setBusy(false);
        }
    }
}
