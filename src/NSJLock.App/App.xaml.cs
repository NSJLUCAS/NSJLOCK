using System.Windows;
using NSJLock.App.Services;
using NSJLock.App.ViewModels;
using NSJLock.Audio;
using NSJLock.Config;

namespace NSJLock.App;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\NSJLock.SingleInstance";
    private const string ShowMainWindowEventName = @"Local\NSJLock.ShowMainWindow";

    private Mutex? singleInstanceMutex;
    private EventWaitHandle? showMainWindowEvent;
    private CancellationTokenSource? singleInstanceSignalCancellation;
    private NAudioEndpointController? audioEndpointController;
    private MainViewModel? mainViewModel;
    private MainWindow? mainWindow;
    private TrayIconService? trayIconService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!TryAcquireSingleInstance())
        {
            Shutdown(0);
            return;
        }

        try
        {
            audioEndpointController = new NAudioEndpointController();
            var volumeProtectionService = new VolumeProtectionService(audioEndpointController);
            var settingsStore = new JsonSettingsStore();
            mainViewModel = new MainViewModel(volumeProtectionService, settingsStore);
            mainWindow = new MainWindow(mainViewModel);
            trayIconService = new TrayIconService(mainWindow, mainViewModel);
            StartSingleInstanceSignalListener();

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
        DisposeSingleInstanceGuard();
        base.OnExit(e);
    }

    private bool TryAcquireSingleInstance()
    {
        singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            name: SingleInstanceMutexName,
            createdNew: out var isFirstInstance);

        if (!isFirstInstance)
        {
            singleInstanceMutex.Dispose();
            singleInstanceMutex = null;
            SignalExistingInstance();
            return false;
        }

        showMainWindowEvent = new EventWaitHandle(
            initialState: false,
            mode: EventResetMode.AutoReset,
            name: ShowMainWindowEventName);
        singleInstanceSignalCancellation = new CancellationTokenSource();

        return true;
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var showMainWindowSignal = EventWaitHandle.OpenExisting(ShowMainWindowEventName);
            showMainWindowSignal.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void StartSingleInstanceSignalListener()
    {
        if (showMainWindowEvent is null || singleInstanceSignalCancellation is null)
        {
            return;
        }

        var cancellationToken = singleInstanceSignalCancellation.Token;
        var signal = showMainWindowEvent;

        _ = Task.Run(() =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                signal.WaitOne();
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                Dispatcher.BeginInvoke(() => trayIconService?.ShowMainWindow());
            }
        }, cancellationToken);
    }

    private void DisposeSingleInstanceGuard()
    {
        singleInstanceSignalCancellation?.Cancel();
        showMainWindowEvent?.Set();
        singleInstanceSignalCancellation?.Dispose();
        singleInstanceSignalCancellation = null;
        showMainWindowEvent?.Dispose();
        showMainWindowEvent = null;

        try
        {
            singleInstanceMutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }

        singleInstanceMutex?.Dispose();
        singleInstanceMutex = null;
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
