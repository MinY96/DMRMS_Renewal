using DM_RMS.Wpf.Views;

namespace DM_RMS.Wpf;

public partial class App : System.Windows.Application
{
    private void Application_Startup(object sender, System.Windows.StartupEventArgs e)
    {
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        var loginWindow = new LoginWindow();
        bool? loginResult = loginWindow.ShowDialog();

        if (loginResult == true && loginWindow.Session is not null)
        {
            var mainWindow = new MainWindow(loginWindow.Session);
            MainWindow = mainWindow;
            ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }
        else
        {
            Shutdown();
        }
    }
}
