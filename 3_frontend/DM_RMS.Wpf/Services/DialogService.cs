using DM_RMS.Wpf.ViewModels;
using DM_RMS.Wpf.Views;

namespace DM_RMS.Wpf.Services;

public interface IDialogService
{
    void ShowInfo(string message, string title = "알림");
    void ShowError(string message, string title = "오류");
    bool Confirm(string message, string title = "확인");
    string? SelectFolder(string description = "폴더를 선택하세요.");
    string? SelectSaveFile(string filter, string defaultFileName);
    string? SelectOpenFile(string filter, string title = "파일을 선택하세요.");
    bool? ShowNewMasterDialog(NewMasterViewModel viewModel);
    bool ConfirmMasterChanges(string masterKey, IReadOnlyList<DM_RMS.Wpf.Models.MasterChangePreviewItem> changes);
}

public sealed class DialogService : IDialogService
{
    public void ShowInfo(string message, string title = "알림")
        => System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

    public void ShowError(string message, string title = "오류")
        => System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);

    public bool Confirm(string message, string title = "확인")
        => System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes;

    public string? SelectFolder(string description = "폴더를 선택하세요.")
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = description, UseDescriptionForTitle = true };
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }

    public string? SelectSaveFile(string filter, string defaultFileName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = filter, FileName = defaultFileName };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SelectOpenFile(string filter, string title = "파일을 선택하세요.")
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = filter, Title = title };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public bool ConfirmMasterChanges(string masterKey, IReadOnlyList<DM_RMS.Wpf.Models.MasterChangePreviewItem> changes)
    {
        var viewModel = new MasterChangePreviewViewModel(masterKey, changes);
        var window = new MasterChangePreviewWindow { DataContext = viewModel };
        viewModel.CloseRequested += confirmed => window.DialogResult = confirmed;
        return window.ShowDialog() == true;
    }

    public bool? ShowNewMasterDialog(NewMasterViewModel viewModel)
    {
        var window = new NewMasterWindow { DataContext = viewModel };
        return window.ShowDialog();
    }
}
