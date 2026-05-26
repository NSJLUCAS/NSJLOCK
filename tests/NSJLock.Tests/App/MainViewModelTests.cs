using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSJLock.App.ViewModels;
using NSJLock.Audio;
using NSJLock.Config;

namespace NSJLock.Tests.App;

[TestClass]
public sealed class MainViewModelTests
{
    [TestMethod]
    public async Task InitializeAsync_LoadsSettingsAndChecksProtectionOnce()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 55, true, null));
        var store = new FakeSettingsStore(new AppSettings(true, 35, AppThemeMode.Light));
        using var viewModel = new MainViewModel(
            new VolumeProtectionService(controller),
            store);

        await viewModel.InitializeAsync();

        Assert.AreEqual("Speakers", viewModel.DeviceName);
        Assert.AreEqual(35, viewModel.CurrentVolumePercent);
        Assert.AreEqual(35, viewModel.MaxVolumePercent);
        Assert.AreEqual("35%", viewModel.SoundLockLimitText);
        Assert.AreEqual("锁定音量", viewModel.SoundLockStrengthText);
        Assert.IsTrue(viewModel.IsProtectionEnabled);
        Assert.AreEqual(AppThemeMode.Light, viewModel.ThemeMode);
        Assert.AreEqual("浅色", viewModel.ThemeButtonText);
        Assert.AreEqual(1, controller.SetCalls.Count);
        Assert.AreEqual(35, controller.SetCalls[0]);
    }

    [TestMethod]
    public async Task InitializeAsync_WhenZoomSessionExists_ShowsRaisedMeetingAudioBoundary()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 40, true, null))
        {
            MeetingDiagnostics = new MeetingAudioDiagnosticSnapshot(
                "Speakers",
                40,
                true,
                true,
                25,
                false,
                "Speakers",
                40,
                true,
                null)
        };
        var store = new FakeSettingsStore(new AppSettings(true, 40, AppThemeMode.Dark));
        using var viewModel = new MainViewModel(
            new VolumeProtectionService(controller),
            store);

        await viewModel.InitializeAsync();

        StringAssert.Contains(viewModel.MeetingAudioStatusText, "锁定目标 Speakers 40%");
        StringAssert.Contains(viewModel.MeetingAudioStatusText, "系统默认输出 Speakers");
        StringAssert.Contains(viewModel.MeetingAudioStatusText, "Zoom 输出设备 Speakers");
        StringAssert.Contains(viewModel.MeetingAudioStatusText, "Zoom 应用音量 100%");
        StringAssert.Contains(viewModel.ZoomOutputText, "Zoom 输出设备：Speakers");
        StringAssert.Contains(viewModel.ZoomOutputText, "Zoom 应用音量：100%");
    }

    [TestMethod]
    public async Task InitializeAsync_WhenZoomSessionIsMissing_ShowsSystemVolumeOnlyBoundary()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 40, true, null))
        {
            MeetingDiagnostics = new MeetingAudioDiagnosticSnapshot(
                "Speakers",
                40,
                true,
                false,
                null,
                null,
                null,
                null,
                false,
                null)
        };
        var store = new FakeSettingsStore(new AppSettings(true, 40, AppThemeMode.Dark));
        using var viewModel = new MainViewModel(
            new VolumeProtectionService(controller),
            store);

        await viewModel.InitializeAsync();

        StringAssert.Contains(viewModel.MeetingAudioStatusText, "锁定目标 Speakers 40%");
        StringAssert.Contains(viewModel.MeetingAudioStatusText, "系统默认输出 Speakers");
        StringAssert.Contains(viewModel.MeetingAudioStatusText, "Zoom 输出 未检测到音频会话");
    }

    [TestMethod]
    public async Task InitializeAsync_WhenZoomUsesAnotherOutputDevice_ShowsDeviceMismatch()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 55, true, null))
        {
            MeetingDiagnostics = new MeetingAudioDiagnosticSnapshot(
                "Speakers",
                55,
                true,
                true,
                25,
                false,
                "Meeting Headset",
                67,
                false,
                null)
        };
        var store = new FakeSettingsStore(new AppSettings(true, 55, AppThemeMode.Dark));
        using var viewModel = new MainViewModel(
            new VolumeProtectionService(controller),
            store);

        await viewModel.InitializeAsync();

        StringAssert.Contains(viewModel.MeetingAudioStatusText, "锁定目标 Speakers 55%");
        StringAssert.Contains(viewModel.MeetingAudioStatusText, "系统默认输出 Speakers");
        StringAssert.Contains(viewModel.MeetingAudioStatusText, "Zoom 输出设备 Meeting Headset");
        StringAssert.Contains(viewModel.MeetingAudioStatusText, "设备音量 67%");
        StringAssert.Contains(viewModel.MeetingAudioStatusText, "Zoom 应用音量 100%");
        StringAssert.Contains(viewModel.MeetingAudioStatusText, "提示：当前未锁 Zoom 输出设备");
        StringAssert.Contains(viewModel.ZoomOutputText, "Zoom 输出设备：Meeting Headset（设备音量 67%）");
        StringAssert.Contains(viewModel.ZoomOutputText, "Zoom 应用音量：100%");
    }

    [TestMethod]
    public async Task InitializeAsync_WhenZoomSessionVolumeIsLow_RaisesZoomAppVolumeToFull()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 55, true, null))
        {
            MeetingDiagnostics = new MeetingAudioDiagnosticSnapshot(
                "Speakers",
                55,
                true,
                true,
                45,
                false,
                "Speakers",
                55,
                true,
                null)
        };
        var store = new FakeSettingsStore(new AppSettings(true, 55, AppThemeMode.Dark));
        using var viewModel = new MainViewModel(
            new VolumeProtectionService(controller),
            store);

        await viewModel.InitializeAsync();

        CollectionAssert.AreEqual(new[] { 100 }, controller.ZoomSetCalls);
        StringAssert.Contains(viewModel.MeetingAudioStatusText, "Zoom 应用音量 100%");
    }

    [TestMethod]
    public async Task InitializeAsync_WhenVolumeIsAdjusted_KeepsStableProtectionStatus()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 80, true, null));
        var store = new FakeSettingsStore(new AppSettings(true, 40, AppThemeMode.Dark));
        using var viewModel = new MainViewModel(
            new VolumeProtectionService(controller),
            store);

        await viewModel.InitializeAsync();

        Assert.AreEqual("保护中", viewModel.StatusText);
        CollectionAssert.AreEqual(new[] { 40 }, controller.SetCalls);
    }

    [TestMethod]
    public async Task MaxVolumePercent_WhenProtectionAdjustsVolume_KeepsStableProtectionStatus()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 40, true, null));
        var store = new FakeSettingsStore(new AppSettings(true, 40, AppThemeMode.Dark));
        using var viewModel = new MainViewModel(
            new VolumeProtectionService(controller),
            store);
        await viewModel.InitializeAsync();

        viewModel.MaxVolumePercent = 35;
        InvokeCheckProtection(viewModel);

        Assert.AreEqual("保护中", viewModel.StatusText);
        CollectionAssert.AreEqual(new[] { 35 }, controller.SetCalls);
    }

    [TestMethod]
    public async Task ToggleProtectionCommand_FlipsProtectionAndSavesSettings()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 20, true, null));
        var store = new FakeSettingsStore(new AppSettings(true, 40, AppThemeMode.Dark));
        using var viewModel = new MainViewModel(
            new VolumeProtectionService(controller),
            store);
        await viewModel.InitializeAsync();

        viewModel.ToggleProtectionCommand.Execute(null);
        await viewModel.SaveSettingsAsync();

        Assert.IsFalse(viewModel.IsProtectionEnabled);
        Assert.AreEqual("开启保护", viewModel.ProtectionButtonText);
        CollectionAssert.Contains(store.SavedSettings, new AppSettings(false, 40, AppThemeMode.Dark));
    }

    [TestMethod]
    public async Task SaveSettingsAsync_SerializesConcurrentSaves()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 20, true, null));
        var store = new FakeSettingsStore(new AppSettings(true, 40, AppThemeMode.Dark))
        {
            SaveDelay = TimeSpan.FromMilliseconds(25)
        };
        using var viewModel = new MainViewModel(
            new VolumeProtectionService(controller),
            store);
        await viewModel.InitializeAsync();
        viewModel.MaxVolumePercent = 41;
        viewModel.MaxVolumePercent = 42;

        await Task.WhenAll(viewModel.SaveSettingsAsync(), viewModel.SaveSettingsAsync());

        Assert.IsTrue(store.MaxConcurrentSaves <= 1);
        Assert.AreEqual(new AppSettings(true, 42, AppThemeMode.Dark), store.SavedSettings[^1]);
    }

    [TestMethod]
    public async Task SaveSettingsAsync_WhenStoreFails_ReturnsFalseAndUpdatesStatus()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 20, true, null));
        var store = new FakeSettingsStore(new AppSettings(true, 40, AppThemeMode.Dark))
        {
            SaveException = new IOException("磁盘不可写")
        };
        using var viewModel = new MainViewModel(
            new VolumeProtectionService(controller),
            store);
        await viewModel.InitializeAsync();

        var saved = await viewModel.SaveSettingsAsync();

        Assert.IsFalse(saved);
        Assert.AreEqual("保存设置失败：磁盘不可写", viewModel.StatusText);
    }

    [TestMethod]
    public async Task InitializeAsync_WhenLanguageIsEnglish_ShowsEnglishText()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 55, true, null));
        var store = new FakeSettingsStore(new AppSettings(true, 35, AppThemeMode.Light, AppLanguage.English));
        using var viewModel = new MainViewModel(
            new VolumeProtectionService(controller),
            store);

        await viewModel.InitializeAsync();

        Assert.AreEqual(AppLanguage.English, viewModel.Language);
        Assert.AreEqual("Light", viewModel.ThemeButtonText);
        Assert.AreEqual("Turn protection off", viewModel.ProtectionButtonText);
        Assert.AreEqual("Protected", viewModel.ProtectionStateText);
        Assert.AreEqual("Volume lock", viewModel.SoundLockStrengthText);
        Assert.AreEqual("Master volume", viewModel.SystemMasterVolumeLabelText);
        StringAssert.Contains(viewModel.SoundLockDescriptionText, "System volume will be pulled back to 35%");
    }

    [TestMethod]
    public async Task InitializeAsync_WhenFollowingSystemDefault_SelectsDefaultOutputOption()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 55, true, null));
        var store = new FakeSettingsStore(new AppSettings(true, 35, AppThemeMode.Light, AppLanguage.English));
        using var viewModel = new MainViewModel(
            new VolumeProtectionService(controller),
            store);

        await viewModel.InitializeAsync();

        Assert.IsNotNull(viewModel.SelectedOutputDeviceOption);
        Assert.IsNull(viewModel.SelectedOutputDeviceOption.DeviceId);
        Assert.AreEqual("Follow system default output", viewModel.SelectedOutputDeviceOption.DisplayName);

        viewModel.Language = AppLanguage.Chinese;

        Assert.IsNotNull(viewModel.SelectedOutputDeviceOption);
        Assert.IsNull(viewModel.SelectedOutputDeviceOption.DeviceId);
        Assert.AreEqual("跟随系统默认输出", viewModel.SelectedOutputDeviceOption.DisplayName);
    }

    [TestMethod]
    public async Task Language_WhenChanged_SavesSelectedLanguageAndRefreshesText()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 20, true, null));
        var store = new FakeSettingsStore(new AppSettings(true, 40, AppThemeMode.Dark, AppLanguage.Chinese));
        using var viewModel = new MainViewModel(
            new VolumeProtectionService(controller),
            store);
        await viewModel.InitializeAsync();

        viewModel.Language = AppLanguage.English;
        await viewModel.SaveSettingsAsync();

        Assert.AreEqual("EN", viewModel.SelectedLanguageLabel);
        Assert.AreEqual("Turn protection off", viewModel.ProtectionButtonText);
        Assert.AreEqual(new AppSettings(true, 40, AppThemeMode.Dark, AppLanguage.English), store.SavedSettings[^1]);

        viewModel.Language = AppLanguage.Chinese;

        Assert.AreEqual("中", viewModel.SelectedLanguageLabel);
        Assert.AreEqual("关闭保护", viewModel.ProtectionButtonText);
    }

    [TestMethod]
    public async Task Language_WhenChanged_ReformatsStatusWithoutCheckingProtection()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 80, true, null));
        var store = new FakeSettingsStore(new AppSettings(true, 40, AppThemeMode.Dark, AppLanguage.Chinese));
        using var viewModel = new MainViewModel(
            new VolumeProtectionService(controller),
            store);
        await viewModel.InitializeAsync();

        viewModel.Language = AppLanguage.English;

        Assert.AreEqual("Protected", viewModel.StatusText);
        CollectionAssert.AreEqual(new[] { 40 }, controller.SetCalls);
    }

    [TestMethod]
    public async Task SaveSettingsAsync_WhenStoreFails_UsesCurrentLanguageForError()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 20, true, null));
        var store = new FakeSettingsStore(new AppSettings(true, 40, AppThemeMode.Dark, AppLanguage.English))
        {
            SaveException = new IOException("disk is read-only")
        };
        using var viewModel = new MainViewModel(
            new VolumeProtectionService(controller),
            store);
        await viewModel.InitializeAsync();

        var saved = await viewModel.SaveSettingsAsync();

        Assert.IsFalse(saved);
        Assert.AreEqual("Failed to save settings: disk is read-only", viewModel.StatusText);
    }

    [TestMethod]
    public async Task SaveSettingsAsync_PreservesCoreFields()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 20, true, null));
        var store = new FakeSettingsStore(new AppSettings(true, 40, AppThemeMode.System));
        using var viewModel = new MainViewModel(
            new VolumeProtectionService(controller),
            store);
        await viewModel.InitializeAsync();

        viewModel.MaxVolumePercent = 41;
        await viewModel.SaveSettingsAsync();

        Assert.AreEqual(new AppSettings(true, 41, AppThemeMode.System), store.SavedSettings[^1]);
    }

    [TestMethod]
    public async Task MaxVolumePercent_QueuesOnlyOneDebouncedSave()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 20, true, null));
        var store = new FakeSettingsStore(new AppSettings(true, 40, AppThemeMode.Dark));
        using var viewModel = new MainViewModel(
            new VolumeProtectionService(controller),
            store);
        await viewModel.InitializeAsync();

        viewModel.MaxVolumePercent = 41;
        viewModel.MaxVolumePercent = 42;

        await Task.Delay(700);

        Assert.AreEqual(1, store.SavedSettings.Count);
        Assert.AreEqual(42, store.SavedSettings[^1].MaxVolumePercent);
        Assert.AreEqual(AppThemeMode.Dark, store.SavedSettings[^1].ThemeMode);
    }

    [TestMethod]
    public async Task MaxVolumePercent_UpdatesExactLockText()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 20, true, null));
        var store = new FakeSettingsStore(new AppSettings(true, 40, AppThemeMode.Dark));
        using var viewModel = new MainViewModel(
            new VolumeProtectionService(controller),
            store);
        await viewModel.InitializeAsync();

        viewModel.MaxVolumePercent = 20;

        Assert.AreEqual("20%", viewModel.SoundLockLimitText);
        Assert.AreEqual("锁定音量", viewModel.SoundLockStrengthText);
        StringAssert.Contains(viewModel.SoundLockDescriptionText, "系统音量会被拉回 20%");
    }

    [TestMethod]
    public async Task ThemeMode_WhenChanged_SavesSelectedTheme()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 20, true, null));
        var store = new FakeSettingsStore(new AppSettings(true, 40, AppThemeMode.Dark));
        using var viewModel = new MainViewModel(
            new VolumeProtectionService(controller),
            store);
        await viewModel.InitializeAsync();

        viewModel.ThemeMode = AppThemeMode.System;
        await viewModel.SaveSettingsAsync();

        Assert.AreEqual(AppThemeMode.System, viewModel.ThemeMode);
        Assert.AreEqual("跟随系统", viewModel.ThemeButtonText);
        Assert.AreEqual(new AppSettings(true, 40, AppThemeMode.System), store.SavedSettings[^1]);

        viewModel.ThemeMode = AppThemeMode.Light;
        await viewModel.SaveSettingsAsync();

        Assert.AreEqual(AppThemeMode.Light, viewModel.ThemeMode);
        Assert.AreEqual("浅色", viewModel.ThemeButtonText);
        Assert.AreEqual(new AppSettings(true, 40, AppThemeMode.Light), store.SavedSettings[^1]);
    }

    private sealed class FakeSettingsStore(AppSettings settings) : ISettingsStore
    {
        private int activeSaves;

        public List<AppSettings> SavedSettings { get; } = [];

        public TimeSpan SaveDelay { get; init; }

        public Exception? SaveException { get; init; }

        public int MaxConcurrentSaves { get; private set; }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(settings);
        }

        public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            var currentSaves = Interlocked.Increment(ref activeSaves);
            MaxConcurrentSaves = Math.Max(MaxConcurrentSaves, currentSaves);

            try
            {
                if (SaveException is not null)
                {
                    throw SaveException;
                }

                if (SaveDelay > TimeSpan.Zero)
                {
                    await Task.Delay(SaveDelay, cancellationToken);
                }

                SavedSettings.Add(settings);
            }
            finally
            {
                Interlocked.Decrement(ref activeSaves);
            }
        }
    }

    private static void InvokeCheckProtection(MainViewModel viewModel)
    {
        var method = typeof(MainViewModel).GetMethod(
            "CheckProtection",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        method.Invoke(viewModel, null);
    }

    private sealed class FakeAudioEndpointController(AudioEndpointSnapshot snapshot) : IAudioEndpointController
    {
        public List<int> SetCalls { get; } = [];

        public List<string?> SetDeviceIds { get; } = [];

        public List<int> ZoomSetCalls { get; } = [];

        public IReadOnlyList<AudioOutputDevice> OutputDevices { get; set; } =
        [
            new("default-device", "Speakers", true)
        ];

        public MeetingAudioDiagnosticSnapshot MeetingDiagnostics { get; set; } = new(
            snapshot.DeviceName ?? "Speakers",
            snapshot.CurrentVolumePercent,
            snapshot.HasDefaultDevice,
            false,
            null,
            null,
            null,
            null,
            false,
            snapshot.ErrorMessage,
            snapshot.DeviceName ?? "Speakers");

        public IReadOnlyList<AudioOutputDevice> GetActiveOutputDevices()
        {
            return OutputDevices;
        }

        public AudioEndpointSnapshot GetBasicSnapshot(string? deviceId = null)
        {
            return snapshot;
        }

        public MeetingAudioDiagnosticSnapshot GetMeetingAudioDiagnostics(string? lockedDeviceId = null)
        {
            return MeetingDiagnostics;
        }

        public void SetZoomSessionVolumePercent(int volumePercent)
        {
            ZoomSetCalls.Add(volumePercent);
            MeetingDiagnostics = MeetingDiagnostics with
            {
                ZoomVolumePercent = Math.Clamp(volumePercent, 0, 100)
            };
        }

        public void ReleaseAudioSampling()
        {
        }

        public void SetMasterVolumePercent(int volumePercent, string? deviceId = null)
        {
            SetCalls.Add(volumePercent);
            SetDeviceIds.Add(deviceId);
        }
    }

}
