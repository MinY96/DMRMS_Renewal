using DM_RMS.Wpf.ViewModels;

namespace DM_RMS.Wpf.Views;

public partial class NewMasterWindow : System.Windows.Window
{
    public NewMasterWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is NewMasterViewModel oldViewModel)
            oldViewModel.RequestClose -= OnRequestClose;

        if (e.NewValue is NewMasterViewModel newViewModel)
            newViewModel.RequestClose += OnRequestClose;
    }

    private void OnRequestClose(bool? dialogResult)
    {
        DialogResult = dialogResult;
        Close();
    }
}
