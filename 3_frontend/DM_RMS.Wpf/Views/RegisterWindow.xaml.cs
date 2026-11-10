using DM_RMS.Wpf.ViewModels;

namespace DM_RMS.Wpf.Views;

public partial class RegisterWindow : System.Windows.Window
{
    public RegisterWindow(string selectedFab)
    {
        InitializeComponent();

        var viewModel = new RegisterViewModel(selectedFab);
        viewModel.RegisterSucceeded += (_, _) => Close();
        viewModel.CloseRequested += (_, _) => Close();

        DataContext = viewModel;
    }
}
