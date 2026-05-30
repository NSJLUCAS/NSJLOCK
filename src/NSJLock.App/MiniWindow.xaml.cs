using System.ComponentModel;
using System.Windows;
using NSJLock.App.ViewModels;

namespace NSJLock.App;

public partial class MiniWindow : Window
{
    private readonly Action returnToMainWindow;
    private bool allowClose;

    public MiniWindow(MainViewModel viewModel, Action returnToMainWindow)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.returnToMainWindow = returnToMainWindow ?? throw new ArgumentNullException(nameof(returnToMainWindow));
    }

    public void AllowClose()
    {
        allowClose = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (allowClose || System.Windows.Application.Current.Dispatcher.HasShutdownStarted)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        Hide();
        returnToMainWindow();
    }

    private void HandleReturnToMainWindowClick(object sender, RoutedEventArgs e)
    {
        Hide();
        returnToMainWindow();
    }
}
