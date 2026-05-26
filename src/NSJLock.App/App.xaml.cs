using System.Windows;
using NSJLock.App.Services;
using NSJLock.App.ViewModels;
using NSJLock.Audio;
using NSJLock.Config;

namespace NSJLock.App;

public partial class App : System.Windows.Application
{
    private NAudioEndpointController? audioEndpointController;
    private MainViewModel? mainViewModel;
    private MainWindow? mainWindow;
    private TrayIconService? trayIconService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            audioEndpointController = new NAudioEndpointController();
            var volumeProtectionService = new VolumeProtectionService(audioEndpointController);
            var settingsStore = new JsonSettingsStore();
            mainViewModel = new MainViewModel(volumeProtectionService, settingsStore);
            mainWindow = new MainWindow(mainViewModel);
            trayIconService = new TrayIconService(mainWindow, mainViewModel);

            await mainViewModel.InitializeAsync();
            mainWindow.Show();
        }
        catch (Exception exception)
        {
            DisposeServices();
            System.Windows.MessageBox.Show(
                $"NSJ Lock 启动失败：{exception.Message}",
                "NSJ Lock",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DisposeServices();
        base.OnExit(e);
    }

    private void DisposeServices()
    {
        trayIconService?.Dispose();
        trayIconService = null;
        mainViewModel?.Dispose();
        mainViewModel = null;
        audioEndpointController?.Dispose();
        audioEndpointController = null;
    }
}
