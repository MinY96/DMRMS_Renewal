using DM_RMS.Wpf.Models;
using DM_RMS.Wpf.ViewModels;

namespace DM_RMS.Wpf.Views;

public partial class LoginWindow : System.Windows.Window
{
    public UserSession? Session { get; private set; }

    public LoginWindow()
    {
        InitializeComponent();

        var viewModel = new LoginViewModel();
        viewModel.LoginSucceeded += (_, session) =>
        {
            Session = session;
            DialogResult = true;
        };
        viewModel.RegisterRequested += (_, _) =>
        {
            var registerWindow = new RegisterWindow(viewModel.SelectedFab)
            {
                Owner = this
            };
            registerWindow.ShowDialog();
        };
        viewModel.CancelRequested += (_, _) =>
        {
            DialogResult = false;
        };

        DataContext = viewModel;
    }
}
