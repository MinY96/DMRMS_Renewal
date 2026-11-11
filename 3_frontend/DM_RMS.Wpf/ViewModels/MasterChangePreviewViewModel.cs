using DM_RMS.Wpf.Commands;
using DM_RMS.Wpf.Models;
using System.Windows.Input;

namespace DM_RMS.Wpf.ViewModels;

/// <summary>저장 미리보기 확인/취소를 WPF View와 분리합니다.</summary>
public sealed class MasterChangePreviewViewModel : BaseViewModel
{
    public MasterChangePreviewViewModel(string masterKey, IReadOnlyList<MasterChangePreviewItem> items)
    {
        MasterKey = masterKey;
        Changes = items;
        ConfirmCommand = new RelayCommand(_ => CloseRequested?.Invoke(true));
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
    }
    public string MasterKey { get; }
    public IReadOnlyList<MasterChangePreviewItem> Changes { get; }
    public int ChangedCount => Changes.Count;
    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }
    public event Action<bool>? CloseRequested;
}
