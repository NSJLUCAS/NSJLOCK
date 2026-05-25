using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using NSJLock.Audio;
using NSJLock.Config;

namespace NSJLock.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly VolumeProtectionService volumeProtectionService;
    private readonly ISettingsStore settingsStore;
    private readonly DispatcherTimer timer;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly SemaphoreSlim saveLock = new(1, 1);
    private readonly object queuedSaveLock = new();
    private readonly object protectionCheckLock = new();
    private readonly object queuedProtectionCheckLock = new();
    private CancellationTokenSource? queuedSaveCancellationTokenSource;
    private CancellationTokenSource? queuedProtectionCheckCancellationTokenSource;
    private bool isDisposed;
    private bool isCheckingProtection;
    private string deviceName = "正在检测...";
    private int currentVolumePercent;
    private int maxVolumePercent = AppSettings.Defaults.MaxVolumePercent;
    private bool isProtectionEnabled = AppSettings.Defaults.IsProtectionEnabled;
    private AppThemeMode themeMode = AppSettings.Defaults.ThemeMode;
    private string? lockedDeviceId = AppSettings.Defaults.LockedDeviceId;
    private string statusText = "正在启动保护...";
    private string meetingAudioStatusText = "会议诊断：等待检测 Zoom 音频会话";
    private string lockedTargetDetailText = "锁定目标：正在检测...";
    private string systemDefaultOutputText = "系统默认输出：正在检测...";
    private string zoomOutputText = "Zoom 输出：等待检测音频会话";
    private string outputWarningText = string.Empty;

    public MainViewModel(
        VolumeProtectionService volumeProtectionService,
        ISettingsStore settingsStore)
    {
        this.volumeProtectionService = volumeProtectionService
            ?? throw new ArgumentNullException(nameof(volumeProtectionService));
        this.settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));

        timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        timer.Tick += HandleTimerTick;
        ToggleProtectionCommand = new RelayCommand(_ => ToggleProtection());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DeviceName
    {
        get => deviceName;
        private set => SetField(ref deviceName, value);
    }

    public int CurrentVolumePercent
    {
        get => currentVolumePercent;
        private set => SetField(ref currentVolumePercent, value);
    }

    public int MaxVolumePercent
    {
        get => maxVolumePercent;
        set
        {
            var normalized = Math.Clamp(value, 0, 100);
            if (SetField(ref maxVolumePercent, normalized))
            {
                OnPropertyChanged(nameof(SoundLockLimitText));
                OnPropertyChanged(nameof(SoundLockStrengthText));
                OnPropertyChanged(nameof(SoundLockDescriptionText));
                QueueSettingsSave();
            }
        }
    }

    public string SoundLockLimitText => $"{MaxVolumePercent}%";

    public string SoundLockStrengthText => "锁定音量";

    public string SoundLockDescriptionText => $"系统音量会被拉回 {MaxVolumePercent}%，高了会降下去，低了会拉上来。";

    public bool IsProtectionEnabled
    {
        get => isProtectionEnabled;
        set
        {
            if (SetField(ref isProtectionEnabled, value))
            {
                OnPropertyChanged(nameof(ProtectionButtonText));
                OnPropertyChanged(nameof(ProtectionStateText));
                QueueSettingsSave();
            }
        }
    }

    public string ProtectionButtonText => IsProtectionEnabled ? "关闭保护" : "开启保护";

    public string ProtectionStateText => IsProtectionEnabled ? "保护中" : "暂停中";

    public AppThemeMode ThemeMode
    {
        get => themeMode;
        set
        {
            if (SetField(ref themeMode, value))
            {
                OnPropertyChanged(nameof(ThemeButtonText));
                QueueSettingsSave();
            }
        }
    }

    public string ThemeButtonText => ThemeMode switch
    {
        AppThemeMode.System => "跟随系统",
        AppThemeMode.Light => "浅色",
        _ => "深色"
    };

    public ObservableCollection<OutputDeviceOption> OutputDeviceOptions { get; } = [];

    public string? LockedDeviceId
    {
        get => lockedDeviceId;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? null : value;
            if (SetField(ref lockedDeviceId, normalized))
            {
                QueueSettingsSave();
                LockedTargetDetailText = "锁定目标：正在切换...";
                ZoomOutputText = "Zoom 输出：等待刷新";
                OutputWarningText = string.Empty;
                QueueProtectionCheck();
            }
        }
    }

    public string StatusText
    {
        get => statusText;
        private set => SetField(ref statusText, value);
    }

    public string MeetingAudioStatusText
    {
        get => meetingAudioStatusText;
        private set => SetField(ref meetingAudioStatusText, value);
    }

    public string LockedTargetDetailText
    {
        get => lockedTargetDetailText;
        private set
        {
            if (SetField(ref lockedTargetDetailText, value))
            {
                OnPropertyChanged(nameof(LockedTargetValueText));
            }
        }
    }

    public string LockedTargetValueText => StripDetailPrefix(LockedTargetDetailText);

    public string SystemDefaultOutputText
    {
        get => systemDefaultOutputText;
        private set
        {
            if (SetField(ref systemDefaultOutputText, value))
            {
                OnPropertyChanged(nameof(SystemDefaultOutputValueText));
            }
        }
    }

    public string SystemDefaultOutputValueText => StripDetailPrefix(SystemDefaultOutputText);

    public string ZoomOutputText
    {
        get => zoomOutputText;
        private set
        {
            if (SetField(ref zoomOutputText, value))
            {
                OnPropertyChanged(nameof(ZoomOutputValueText));
            }
        }
    }

    public string ZoomOutputValueText => StripDetailPrefix(ZoomOutputText);

    public string OutputWarningText
    {
        get => outputWarningText;
        private set => SetField(ref outputWarningText, value);
    }

    public ICommand ToggleProtectionCommand { get; }

    public async Task InitializeAsync()
    {
        ThrowIfDisposed();

        var settings = await settingsStore.LoadAsync(cancellationTokenSource.Token);
        maxVolumePercent = settings.MaxVolumePercent;
        isProtectionEnabled = settings.IsProtectionEnabled;
        themeMode = settings.ThemeMode;
        lockedDeviceId = settings.LockedDeviceId;

        RefreshOutputDevices();
        OnPropertyChanged(nameof(MaxVolumePercent));
        OnPropertyChanged(nameof(SoundLockLimitText));
        OnPropertyChanged(nameof(SoundLockStrengthText));
        OnPropertyChanged(nameof(SoundLockDescriptionText));
        OnPropertyChanged(nameof(IsProtectionEnabled));
        OnPropertyChanged(nameof(ProtectionButtonText));
        OnPropertyChanged(nameof(ProtectionStateText));
        OnPropertyChanged(nameof(ThemeMode));
        OnPropertyChanged(nameof(ThemeButtonText));
        OnPropertyChanged(nameof(LockedDeviceId));

        CheckProtection();
        timer.Start();
    }

    public async Task<bool> SaveSettingsAsync()
    {
        return await SaveSettingsAsyncCore(cancellationTokenSource.Token);
    }

    public void ToggleProtection()
    {
        IsProtectionEnabled = !IsProtectionEnabled;
        CheckProtection();
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        timer.Stop();
        timer.Tick -= HandleTimerTick;
        cancellationTokenSource.Cancel();
        CancelQueuedSave();
        CancelQueuedProtectionCheck();
        cancellationTokenSource.Dispose();
        saveLock.Dispose();
    }

    private async Task<bool> SaveSettingsAsyncCore(CancellationToken cancellationToken)
    {
        if (isDisposed)
        {
            return true;
        }

        try
        {
            await saveLock.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (isDisposed)
        {
            return true;
        }
        catch (ObjectDisposedException) when (isDisposed)
        {
            return true;
        }

        try
        {
            if (isDisposed)
            {
                return true;
            }

            var settings = new AppSettings(
                IsProtectionEnabled,
                MaxVolumePercent,
                ThemeMode,
                LockedDeviceId);
            await settingsStore.SaveAsync(settings, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return true;
        }
        catch (OperationCanceledException) when (isDisposed)
        {
            return true;
        }
        catch (ObjectDisposedException) when (isDisposed)
        {
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusText = $"保存设置失败：{exception.Message}";
            return false;
        }
        finally
        {
            saveLock.Release();
        }
    }

    private void QueueSettingsSave()
    {
        if (isDisposed)
        {
            return;
        }

        CancellationTokenSource? previous;
        CancellationTokenSource current;

        lock (queuedSaveLock)
        {
            previous = queuedSaveCancellationTokenSource;
            current = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
            queuedSaveCancellationTokenSource = current;
        }

        previous?.Cancel();
        _ = DebouncedSaveSettingsAsync(current);
    }

    private async Task DebouncedSaveSettingsAsync(CancellationTokenSource queuedSaveCancellation)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), queuedSaveCancellation.Token);
            await SaveSettingsAsyncCore(queuedSaveCancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            lock (queuedSaveLock)
            {
                if (ReferenceEquals(queuedSaveCancellationTokenSource, queuedSaveCancellation))
                {
                    queuedSaveCancellationTokenSource = null;
                }
            }

            queuedSaveCancellation.Dispose();
        }
    }

    private void CancelQueuedSave()
    {
        CancellationTokenSource? queuedSaveCancellation;

        lock (queuedSaveLock)
        {
            queuedSaveCancellation = queuedSaveCancellationTokenSource;
            queuedSaveCancellationTokenSource = null;
        }

        queuedSaveCancellation?.Cancel();
    }

    private void QueueProtectionCheck()
    {
        if (isDisposed)
        {
            return;
        }

        CancellationTokenSource? previous;
        CancellationTokenSource current;

        lock (queuedProtectionCheckLock)
        {
            previous = queuedProtectionCheckCancellationTokenSource;
            current = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
            queuedProtectionCheckCancellationTokenSource = current;
        }

        previous?.Cancel();
        _ = RunQueuedProtectionCheckAsync(current);
    }

    private async Task RunQueuedProtectionCheckAsync(CancellationTokenSource queuedCheckCancellation)
    {
        try
        {
            var isProtectionEnabledSnapshot = IsProtectionEnabled;
            var maxVolumePercentSnapshot = MaxVolumePercent;
            var lockedDeviceIdSnapshot = LockedDeviceId;
            var now = DateTimeOffset.Now;

            await Task.Yield();
            var result = await Task.Run(
                () => CheckProtectionAndMeetingDiagnosticsCore(
                    isProtectionEnabledSnapshot,
                    maxVolumePercentSnapshot,
                    now,
                    lockedDeviceIdSnapshot),
                queuedCheckCancellation.Token);

            if (!isDisposed && !queuedCheckCancellation.IsCancellationRequested)
            {
                ApplyProtectionResult(result.ProtectionResult);
                ApplyMeetingAudioDiagnostics(result.MeetingDiagnostics);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception) when (isDisposed)
        {
        }
        catch (Exception exception)
        {
            StatusText = $"读取音频失败：{exception.Message}";
        }
        finally
        {
            lock (queuedProtectionCheckLock)
            {
                if (ReferenceEquals(queuedProtectionCheckCancellationTokenSource, queuedCheckCancellation))
                {
                    queuedProtectionCheckCancellationTokenSource = null;
                }
            }

            queuedCheckCancellation.Dispose();
        }
    }

    private void CancelQueuedProtectionCheck()
    {
        CancellationTokenSource? queuedCheckCancellation;

        lock (queuedProtectionCheckLock)
        {
            queuedCheckCancellation = queuedProtectionCheckCancellationTokenSource;
            queuedProtectionCheckCancellationTokenSource = null;
        }

        queuedCheckCancellation?.Cancel();
    }

    private void RefreshOutputDevices()
    {
        var previousSelection = LockedDeviceId;
        var devices = volumeProtectionService.GetActiveOutputDevices();

        OutputDeviceOptions.Clear();
        OutputDeviceOptions.Add(new OutputDeviceOption(null, "跟随系统默认输出", false));
        foreach (var device in devices)
        {
            OutputDeviceOptions.Add(new OutputDeviceOption(
                device.Id,
                device.Name,
                device.IsDefault));
        }

        if (previousSelection is not null
            && !devices.Any(device => string.Equals(device.Id, previousSelection, StringComparison.OrdinalIgnoreCase)))
        {
            lockedDeviceId = null;
            OnPropertyChanged(nameof(LockedDeviceId));
            OutputWarningText = "提示：之前选择的输出设备已不可用，已改为跟随系统默认输出。";
            QueueSettingsSave();
        }
    }

    private async void HandleTimerTick(object? sender, EventArgs e)
    {
        if (isDisposed || isCheckingProtection)
        {
            return;
        }

        isCheckingProtection = true;
        var isProtectionEnabledSnapshot = IsProtectionEnabled;
        var maxVolumePercentSnapshot = MaxVolumePercent;
        var lockedDeviceIdSnapshot = LockedDeviceId;
        var now = DateTimeOffset.Now;

        try
        {
            var result = await Task.Run(
                () => CheckProtectionAndMeetingDiagnosticsCore(isProtectionEnabledSnapshot, maxVolumePercentSnapshot, now, lockedDeviceIdSnapshot),
                cancellationTokenSource.Token);

            if (!isDisposed)
            {
                ApplyProtectionResult(result.ProtectionResult);
                ApplyMeetingAudioDiagnostics(result.MeetingDiagnostics);
            }
        }
        catch (OperationCanceledException) when (isDisposed || cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception) when (isDisposed)
        {
        }
        catch (Exception exception)
        {
            StatusText = $"读取音频失败：{exception.Message}";
        }
        finally
        {
            isCheckingProtection = false;
        }
    }

    private void CheckProtection()
    {
        if (isDisposed)
        {
            return;
        }

        var result = CheckProtectionAndMeetingDiagnosticsCore(
            IsProtectionEnabled,
            MaxVolumePercent,
            DateTimeOffset.Now,
            LockedDeviceId);

        ApplyProtectionResult(result.ProtectionResult);
        ApplyMeetingAudioDiagnostics(result.MeetingDiagnostics);
    }

    private (ProtectionTickResult ProtectionResult, MeetingAudioDiagnosticSnapshot MeetingDiagnostics) CheckProtectionAndMeetingDiagnosticsCore(
        bool isProtectionEnabled,
        int maxVolumePercent,
        DateTimeOffset now,
        string? lockedDeviceIdSnapshot)
    {
        lock (protectionCheckLock)
        {
            var protectionResult = volumeProtectionService.CheckOnce(
                isProtectionEnabled,
                maxVolumePercent,
                now,
                lockedDeviceIdSnapshot);
            var meetingDiagnostics = volumeProtectionService.GetMeetingAudioDiagnostics(lockedDeviceIdSnapshot);
            if (ShouldRaiseZoomVolume(meetingDiagnostics))
            {
                volumeProtectionService.SetZoomSessionVolumePercent(100);
                meetingDiagnostics = volumeProtectionService.GetMeetingAudioDiagnostics(lockedDeviceIdSnapshot);
            }

            return (protectionResult, meetingDiagnostics);
        }
    }

    private void ApplyProtectionResult(ProtectionTickResult result)
    {
        DeviceName = result.DeviceName;
        CurrentVolumePercent = result.CurrentVolumePercent;
        StatusText = FormatProtectionStatus(result);
    }

    private void ApplyMeetingAudioDiagnostics(MeetingAudioDiagnosticSnapshot diagnostics)
    {
        MeetingAudioStatusText = FormatMeetingAudioStatus(diagnostics);
        LockedTargetDetailText = diagnostics.HasDefaultDevice
            ? $"锁定目标：{diagnostics.DeviceName} {diagnostics.SystemVolumePercent}%"
            : "锁定目标：未检测到可用输出设备";
        SystemDefaultOutputText = diagnostics.SystemDefaultDeviceName is null
            ? "系统默认输出：未检测到"
            : $"系统默认输出：{diagnostics.SystemDefaultDeviceName}";
        ZoomOutputText = FormatZoomOutputText(diagnostics);
        OutputWarningText = FormatOutputWarningText(diagnostics);
    }

    private static bool ShouldRaiseZoomVolume(MeetingAudioDiagnosticSnapshot diagnostics)
    {
        return diagnostics.HasDefaultDevice
            && diagnostics.HasZoomSession
            && diagnostics.IsZoomMuted != true
            && diagnostics.ZoomVolumePercent is < 100;
    }

    private static string FormatProtectionStatus(ProtectionTickResult result)
    {
        return result.StatusCode switch
        {
            ProtectionTickStatus.AudioReadFailed => $"读取音频失败：{result.StatusDetail}",
            ProtectionTickStatus.NoDefaultDevice => "未检测到默认输出设备",
            ProtectionTickStatus.ProtectionPaused => "保护已暂停",
            ProtectionTickStatus.BaselineUpdated => "已更新基准音量",
            ProtectionTickStatus.Protecting => "保护中",
            ProtectionTickStatus.VolumeWriteFailed => $"设置音量失败：{result.StatusDetail}",
            ProtectionTickStatus.VolumeAdjusted => "保护中",
            _ => "保护中"
        };
    }

    private static string FormatMeetingAudioStatus(MeetingAudioDiagnosticSnapshot diagnostics)
    {
        if (!diagnostics.HasDefaultDevice)
        {
            return diagnostics.ErrorMessage is null
                ? "会议诊断：未检测到默认输出设备"
                : $"会议诊断：读取失败：{diagnostics.ErrorMessage}";
        }

        if (!diagnostics.HasZoomSession)
        {
            return $"输出诊断：锁定目标 {diagnostics.DeviceName} {diagnostics.SystemVolumePercent}%；系统默认输出 {diagnostics.DeviceName}；Zoom 输出 未检测到音频会话";
        }

        if (diagnostics.IsZoomMuted == true)
        {
            return $"输出诊断：锁定目标 {diagnostics.DeviceName} {diagnostics.SystemVolumePercent}%；系统默认输出 {diagnostics.DeviceName}；Zoom 输出 {diagnostics.ZoomDeviceName ?? diagnostics.DeviceName}，Zoom 已静音";
        }

        if (!diagnostics.IsZoomOnLockedDevice)
        {
            return $"输出诊断：锁定目标 {diagnostics.DeviceName} {diagnostics.SystemVolumePercent}%；系统默认输出 {diagnostics.DeviceName}；Zoom 输出设备 {diagnostics.ZoomDeviceName}，设备音量 {diagnostics.ZoomDeviceVolumePercent}%；Zoom 应用音量 {diagnostics.ZoomVolumePercent}%；提示：当前未锁 Zoom 输出设备";
        }

        return $"输出诊断：锁定目标 {diagnostics.DeviceName} {diagnostics.SystemVolumePercent}%；系统默认输出 {diagnostics.DeviceName}；Zoom 输出设备 {diagnostics.ZoomDeviceName ?? diagnostics.DeviceName}；Zoom 应用音量 {diagnostics.ZoomVolumePercent}%";
    }

    private static string FormatZoomOutputText(MeetingAudioDiagnosticSnapshot diagnostics)
    {
        if (!diagnostics.HasDefaultDevice)
        {
            return "Zoom 输出：未检测到";
        }

        if (!diagnostics.HasZoomSession)
        {
            return "Zoom 输出：未检测到音频会话";
        }

        if (diagnostics.IsZoomMuted == true)
        {
            return $"Zoom 输出：{diagnostics.ZoomDeviceName ?? diagnostics.DeviceName}，已静音";
        }

        var zoomDevice = diagnostics.ZoomDeviceName ?? diagnostics.DeviceName;
        var zoomDeviceVolume = diagnostics.ZoomDeviceVolumePercent is null
            ? string.Empty
            : $"（设备音量 {diagnostics.ZoomDeviceVolumePercent}%）";
        return $"Zoom 输出设备：{zoomDevice}{zoomDeviceVolume}；Zoom 应用音量：{diagnostics.ZoomVolumePercent}%";
    }

    private static string FormatOutputWarningText(MeetingAudioDiagnosticSnapshot diagnostics)
    {
        if (!diagnostics.HasDefaultDevice || !diagnostics.HasZoomSession || diagnostics.IsZoomMuted == true)
        {
            return string.Empty;
        }

        return diagnostics.IsZoomOnLockedDevice
            ? string.Empty
            : "提示：当前锁定目标不是 Zoom 正在输出的设备。";
    }

    private static string StripDetailPrefix(string value)
    {
        var separatorIndex = value.IndexOf('：', StringComparison.Ordinal);
        return separatorIndex >= 0 && separatorIndex + 1 < value.Length
            ? value[(separatorIndex + 1)..].Trim()
            : value;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
    }
}
