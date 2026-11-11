using DM_RMS.Wpf.Models;
using DM_RMS.Wpf.ViewModels;
using System.ComponentModel;

namespace DM_RMS.Wpf;

public partial class MainWindow : System.Windows.Window
{
    private bool _isClosingInProgress;
    private bool _allowClose;
    public MainWindow(UserSession session)
    {
        InitializeComponent();
        DataContext = new MainViewModel(session);
        Loaded += (_, _) => ((MainViewModel)DataContext).Recipe.LoadEquipmentSummaryCommand.Execute(null);
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
            return;

        e.Cancel = true;
        if (_isClosingInProgress)
            return;

        _isClosingInProgress = true;
        await ((MainViewModel)DataContext).EndUsageSessionAsync();
        _allowClose = true;
        Close();
    }
}
