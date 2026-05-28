using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using NSJLock.Audio;
using NSJLock.Config;
using AudioProtectionMode = NSJLock.Audio.ProtectionMode;
using ConfigProtectionMode = NSJLock.Config.ProtectionMode;

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
    private AppLanguage language = AppSettings.Defaults.Language;
    private ProtectionTickResult? lastProtectionResult;
    private MeetingAudioDiagnosticSnapshot? lastMeetingDiagnostics;
    private string deviceName = "正在检测...";
    private int currentVolumePercent;
    private int currentPeakPercent;
    private int maxVolumePercent = AppSettings.Defaults.MaxVolumePercent;
    private bool isProtectionEnabled = AppSettings.Defaults.IsProtectionEnabled;
    private AppThemeMode themeMode = AppSettings.Defaults.ThemeMode;
    private string? lockedDeviceId = AppSettings.Defaults.LockedDeviceId;
    private ConfigProtectionMode protectionMode = AppSettings.Defaults.ProtectionMode;
    private int limiterPeakThresholdPercent = AppSettings.Defaults.LimiterPeakThresholdPercent;
    private int limiterReleaseThresholdPercent = AppSettings.Defaults.LimiterReleaseThresholdPercent;
    private int limiterMinimumVolumePercent = AppSettings.Defaults.LimiterMinimumVolumePercent;
    private OutputDeviceOption? selectedOutputDeviceOption;
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
        SelectChineseCommand = new RelayCommand(_ => Language = AppLanguage.Chinese);
        SelectEnglishCommand = new RelayCommand(_ => Language = AppLanguage.English);
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

    public int CurrentPeakPercent
    {
        get => currentPeakPercent;
        private set => SetField(ref currentPeakPercent, value);
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

    public string SoundLockStrengthText => MainWindowText.SoundLockStrength(Language, ProtectionMode);

    public string SoundLockDescriptionText => MainWindowText.SoundLockDescription(Language, MaxVolumePercent, ProtectionMode);

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

    public string ProtectionButtonText => MainWindowText.ProtectionButton(Language, IsProtectionEnabled);

    public string ProtectionStateText => MainWindowText.ProtectionState(Language, IsProtectionEnabled);

    public ConfigProtectionMode ProtectionMode
    {
        get => protectionMode;
        set
        {
            if (SetField(ref protectionMode, value))
            {
                OnPropertyChanged(nameof(IsFixedLockSelected));
                OnPropertyChanged(nameof(IsDynamicLimiterSelected));
                OnPropertyChanged(nameof(SoundLockStrengthText));
                OnPropertyChanged(nameof(SoundLockDescriptionText));
                OnPropertyChanged(nameof(LockedTargetLabelText));
                OnPropertyChanged(nameof(AdjustLockValueText));
                OnPropertyChanged(nameof(AdjustLockDescriptionText));
                QueueSettingsSave();
                QueueProtectionCheck();
            }
        }
    }

    public bool IsFixedLockSelected
    {
        get => ProtectionMode == ConfigProtectionMode.FixedLock;
        set
        {
            if (value)
            {
                ProtectionMode = ConfigProtectionMode.FixedLock;
            }
        }
    }

    public bool IsDynamicLimiterSelected
    {
        get => ProtectionMode == ConfigProtectionMode.DynamicLimiter;
        set
        {
            if (value)
            {
                ProtectionMode = ConfigProtectionMode.DynamicLimiter;
            }
        }
    }

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

    public string ThemeButtonText => MainWindowText.Theme(Language, ThemeMode);

    public string ThemeSystemText => MainWindowText.Theme(Language, AppThemeMode.System);

    public string ThemeLightText => MainWindowText.Theme(Language, AppThemeMode.Light);

    public string ThemeDarkText => MainWindowText.Theme(Language, AppThemeMode.Dark);

    public AppLanguage Language
    {
        get => language;
        set
        {
            if (SetField(ref language, value))
            {
                OnLanguageChanged();
                QueueSettingsSave();
            }
        }
    }

    public bool IsChineseSelected => Language == AppLanguage.Chinese;

    public bool IsEnglishSelected => Language == AppLanguage.English;

    public string SelectedLanguageLabel => MainWindowText.SelectedLanguageLabel(Language);

    public string WindowSubtitleText => MainWindowText.Subtitle(Language);

    public string SoundLockTitleText => MainWindowText.SoundLockTitle(Language);

    public string CurrentVolumeLabelText => MainWindowText.CurrentVolume(Language);

    public string SystemMasterVolumeLabelText => MainWindowText.SystemMasterVolume(Language);

    public string LockedTargetLabelText => MainWindowText.LockedTarget(Language, ProtectionMode);

    public string FixedLockModeText => MainWindowText.FixedLockMode(Language);

    public string DynamicLimiterModeText => MainWindowText.DynamicLimiterMode(Language);

    public string OutputPeakLabelText => MainWindowText.OutputPeak(Language);

    public string TargetLabelText => MainWindowText.Target(Language);

    public string SystemOutputLabelText => MainWindowText.SystemOutput(Language);

    public string ZoomLabelText => MainWindowText.Zoom(Language);

    public string FollowSystemDefaultOutputText => MainWindowText.FollowSystemDefaultOutput(Language);

    public string AdjustLockValueText => MainWindowText.AdjustLockValue(Language, ProtectionMode);

    public string AdjustLockDescriptionText => MainWindowText.AdjustLockDescription(Language, ProtectionMode);

    public string QuieterText => MainWindowText.Quieter(Language);

    public string LouderText => MainWindowText.Louder(Language);

    public ObservableCollection<OutputDeviceOption> OutputDeviceOptions { get; } = [];

    public OutputDeviceOption? SelectedOutputDeviceOption
    {
        get => selectedOutputDeviceOption;
        set
        {
            if (value is null)
            {
                SetSelectedOutputDeviceOption(null);
                return;
            }

            SetSelectedOutputDeviceOption(value);
            LockedDeviceId = value.DeviceId;
        }
    }

    public string? LockedDeviceId
    {
        get => lockedDeviceId;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? null : value;
            if (SetField(ref lockedDeviceId, normalized))
            {
                RefreshSelectedOutputDeviceOption();
                QueueSettingsSave();
                LockedTargetDetailText = MainWindowText.LockedTargetSwitching(Language);
                ZoomOutputText = MainWindowText.ZoomRefreshWaiting(Language);
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

    public ICommand SelectChineseCommand { get; }

    public ICommand SelectEnglishCommand { get; }

    public async Task InitializeAsync()
    {
        ThrowIfDisposed();

        var settings = await settingsStore.LoadAsync(cancellationTokenSource.Token);
        maxVolumePercent = settings.MaxVolumePercent;
        isProtectionEnabled = settings.IsProtectionEnabled;
        themeMode = settings.ThemeMode;
        language = settings.Language;
        lockedDeviceId = settings.LockedDeviceId;
        protectionMode = settings.ProtectionMode;
        limiterPeakThresholdPercent = settings.LimiterPeakThresholdPercent;
        limiterReleaseThresholdPercent = settings.LimiterReleaseThresholdPercent;
        limiterMinimumVolumePercent = settings.LimiterMinimumVolumePercent;

        RefreshOutputDevices();
        OnPropertyChanged(nameof(MaxVolumePercent));
        OnPropertyChanged(nameof(SoundLockLimitText));
        OnPropertyChanged(nameof(SoundLockStrengthText));
        OnPropertyChanged(nameof(SoundLockDescriptionText));
        OnPropertyChanged(nameof(IsProtectionEnabled));
        OnPropertyChanged(nameof(ProtectionButtonText));
        OnPropertyChanged(nameof(ProtectionStateText));
        OnPropertyChanged(nameof(ProtectionMode));
        OnPropertyChanged(nameof(IsFixedLockSelected));
        OnPropertyChanged(nameof(IsDynamicLimiterSelected));
        OnPropertyChanged(nameof(ThemeMode));
        OnPropertyChanged(nameof(ThemeButtonText));
        OnLanguageChanged(reattachStatus: false);
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

    private void OnLanguageChanged(bool reattachStatus = true)
    {
        OnPropertyChanged(nameof(IsChineseSelected));
        OnPropertyChanged(nameof(IsEnglishSelected));
        OnPropertyChanged(nameof(SelectedLanguageLabel));
        OnPropertyChanged(nameof(WindowSubtitleText));
        OnPropertyChanged(nameof(ThemeButtonText));
        OnPropertyChanged(nameof(ThemeSystemText));
        OnPropertyChanged(nameof(ThemeLightText));
        OnPropertyChanged(nameof(ThemeDarkText));
        OnPropertyChanged(nameof(ProtectionButtonText));
        OnPropertyChanged(nameof(ProtectionStateText));
        OnPropertyChanged(nameof(SoundLockStrengthText));
        OnPropertyChanged(nameof(SoundLockTitleText));
        OnPropertyChanged(nameof(SoundLockDescriptionText));
        OnPropertyChanged(nameof(CurrentVolumeLabelText));
        OnPropertyChanged(nameof(SystemMasterVolumeLabelText));
        OnPropertyChanged(nameof(LockedTargetLabelText));
        OnPropertyChanged(nameof(FixedLockModeText));
        OnPropertyChanged(nameof(DynamicLimiterModeText));
        OnPropertyChanged(nameof(OutputPeakLabelText));
        OnPropertyChanged(nameof(TargetLabelText));
        OnPropertyChanged(nameof(SystemOutputLabelText));
        OnPropertyChanged(nameof(ZoomLabelText));
        OnPropertyChanged(nameof(FollowSystemDefaultOutputText));
        OnPropertyChanged(nameof(AdjustLockValueText));
        OnPropertyChanged(nameof(AdjustLockDescriptionText));
        OnPropertyChanged(nameof(QuieterText));
        OnPropertyChanged(nameof(LouderText));
        RefreshOutputDeviceLabels();
        RefreshSelectedOutputDeviceOption();

        if (!reattachStatus)
        {
            return;
        }

        if (lastProtectionResult is not null)
        {
            ApplyProtectionResult(lastProtectionResult);
        }

        if (lastMeetingDiagnostics is not null)
        {
            ApplyMeetingAudioDiagnostics(lastMeetingDiagnostics);
        }
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
                Language,
                LockedDeviceId: LockedDeviceId,
                ProtectionMode: ProtectionMode,
                LimiterPeakThresholdPercent: limiterPeakThresholdPercent,
                LimiterReleaseThresholdPercent: limiterReleaseThresholdPercent,
                LimiterMinimumVolumePercent: limiterMinimumVolumePercent);
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
            StatusText = MainWindowText.SaveFailed(Language, exception.Message);
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
            var protectionModeSnapshot = ProtectionMode;
            var now = DateTimeOffset.Now;

            await Task.Yield();
            var result = await Task.Run(
                () => CheckProtectionAndMeetingDiagnosticsCore(
                    isProtectionEnabledSnapshot,
                    maxVolumePercentSnapshot,
                    now,
                    lockedDeviceIdSnapshot,
                    protectionModeSnapshot),
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
            StatusText = MainWindowText.AudioReadFailed(Language, exception.Message);
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
        OutputDeviceOptions.Add(new OutputDeviceOption(null, FollowSystemDefaultOutputText, false));
        foreach (var device in devices)
        {
            OutputDeviceOptions.Add(new OutputDeviceOption(
                device.Id,
                device.Name,
                device.IsDefault,
                MainWindowText.SystemDefaultSuffix(Language)));
        }

        if (previousSelection is not null
            && !devices.Any(device => string.Equals(device.Id, previousSelection, StringComparison.OrdinalIgnoreCase)))
        {
            lockedDeviceId = null;
            OnPropertyChanged(nameof(LockedDeviceId));
            OutputWarningText = MainWindowText.MissingPreviousOutputDevice(Language);
            QueueSettingsSave();
        }

        RefreshSelectedOutputDeviceOption();
    }

    private void RefreshOutputDeviceLabels()
    {
        for (var index = 0; index < OutputDeviceOptions.Count; index++)
        {
            var option = OutputDeviceOptions[index];
            OutputDeviceOptions[index] = option.DeviceId is null
                ? new OutputDeviceOption(null, FollowSystemDefaultOutputText, false)
                : option with { DefaultSuffix = MainWindowText.SystemDefaultSuffix(Language) };
        }
    }

    private void RefreshSelectedOutputDeviceOption()
    {
        SetSelectedOutputDeviceOption(OutputDeviceOptions.FirstOrDefault(option =>
            string.Equals(option.DeviceId, LockedDeviceId, StringComparison.OrdinalIgnoreCase)));
    }

    private void SetSelectedOutputDeviceOption(OutputDeviceOption? option)
    {
        if (Equals(selectedOutputDeviceOption, option))
        {
            return;
        }

        selectedOutputDeviceOption = option;
        OnPropertyChanged(nameof(SelectedOutputDeviceOption));
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
        var protectionModeSnapshot = ProtectionMode;
        var now = DateTimeOffset.Now;

        try
        {
            var result = await Task.Run(
                () => CheckProtectionAndMeetingDiagnosticsCore(
                    isProtectionEnabledSnapshot,
                    maxVolumePercentSnapshot,
                    now,
                    lockedDeviceIdSnapshot,
                    protectionModeSnapshot),
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
            StatusText = MainWindowText.AudioReadFailed(Language, exception.Message);
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
            LockedDeviceId,
            ProtectionMode);

        ApplyProtectionResult(result.ProtectionResult);
        ApplyMeetingAudioDiagnostics(result.MeetingDiagnostics);
    }

    private (ProtectionTickResult ProtectionResult, MeetingAudioDiagnosticSnapshot MeetingDiagnostics) CheckProtectionAndMeetingDiagnosticsCore(
        bool isProtectionEnabled,
        int maxVolumePercent,
        DateTimeOffset now,
        string? lockedDeviceIdSnapshot,
        ConfigProtectionMode protectionModeSnapshot)
    {
        lock (protectionCheckLock)
        {
            var protectionResult = volumeProtectionService.CheckOnce(
                isProtectionEnabled,
                maxVolumePercent,
                now,
                lockedDeviceIdSnapshot,
                MapProtectionMode(protectionModeSnapshot));
            var meetingDiagnostics = volumeProtectionService.GetMeetingAudioDiagnostics(lockedDeviceIdSnapshot);
            try
            {
                if (ShouldRaiseZoomVolume(meetingDiagnostics))
                {
                    volumeProtectionService.SetZoomSessionVolumePercent(100);
                    meetingDiagnostics = volumeProtectionService.GetMeetingAudioDiagnostics(lockedDeviceIdSnapshot);
                }
            }
            catch (InvalidOperationException exception)
            {
                meetingDiagnostics = meetingDiagnostics with
                {
                    ErrorMessage = exception.Message
                };
            }

            return (protectionResult, meetingDiagnostics);
        }
    }

    private void ApplyProtectionResult(ProtectionTickResult result)
    {
        lastProtectionResult = result;
        DeviceName = result.DeviceName;
        CurrentVolumePercent = result.CurrentVolumePercent;
        CurrentPeakPercent = result.CurrentPeakPercent ?? 0;
        StatusText = MainWindowText.ProtectionStatus(Language, result);
    }

    private void ApplyMeetingAudioDiagnostics(MeetingAudioDiagnosticSnapshot diagnostics)
    {
        lastMeetingDiagnostics = diagnostics;
        MeetingAudioStatusText = MainWindowText.MeetingAudioStatus(Language, diagnostics);
        LockedTargetDetailText = MainWindowText.LockedTargetDetail(Language, diagnostics);
        SystemDefaultOutputText = MainWindowText.SystemDefaultOutput(Language, diagnostics);
        ZoomOutputText = MainWindowText.ZoomOutput(Language, diagnostics);
        OutputWarningText = MainWindowText.OutputWarning(Language, diagnostics);
    }

    private static bool ShouldRaiseZoomVolume(MeetingAudioDiagnosticSnapshot diagnostics)
    {
        return diagnostics.HasDefaultDevice
            && diagnostics.HasZoomSession
            && diagnostics.IsZoomMuted != true
            && diagnostics.ZoomVolumePercent is < 100;
    }

    private static AudioProtectionMode MapProtectionMode(ConfigProtectionMode protectionMode)
    {
        return protectionMode switch
        {
            ConfigProtectionMode.DynamicLimiter => AudioProtectionMode.DynamicLimiter,
            _ => AudioProtectionMode.FixedLock
        };
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
        if (separatorIndex < 0)
        {
            separatorIndex = value.IndexOf(':', StringComparison.Ordinal);
        }

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
