using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSJLock.Config;

namespace NSJLock.Tests.Config;

[TestClass]
public sealed class JsonSettingsStoreTests
{
    [TestMethod]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsDefaults()
    {
        var directory = CreateTempDirectory();
        var store = new JsonSettingsStore(directory);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.IsTrue(settings.IsProtectionEnabled);
        Assert.AreEqual(40, settings.MaxVolumePercent);
        Assert.AreEqual(AppThemeMode.Dark, settings.ThemeMode);
    }

    [TestMethod]
    public async Task LoadAsync_WhenJsonIsInvalid_ReturnsDefaults()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "settings.json"), "{broken json");
        var store = new JsonSettingsStore(directory);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.IsTrue(settings.IsProtectionEnabled);
        Assert.AreEqual(40, settings.MaxVolumePercent);
        Assert.AreEqual(AppThemeMode.Dark, settings.ThemeMode);
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_RoundTripsSettings()
    {
        var directory = CreateTempDirectory();
        var store = new JsonSettingsStore(directory);
        var original = new AppSettings(false, 35, AppThemeMode.Light, AppLanguage.English);

        await store.SaveAsync(original, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.IsFalse(loaded.IsProtectionEnabled);
        Assert.AreEqual(35, loaded.MaxVolumePercent);
        Assert.AreEqual(AppThemeMode.Light, loaded.ThemeMode);
        Assert.AreEqual(AppLanguage.English, loaded.Language);
    }

    [TestMethod]
    public async Task LoadAsync_WhenOldJsonHasOnlyCoreFields_LoadsCoreFields()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "settings.json"),
            """{"isProtectionEnabled":false,"maxVolumePercent":35}""");
        var store = new JsonSettingsStore(directory);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.IsFalse(settings.IsProtectionEnabled);
        Assert.AreEqual(35, settings.MaxVolumePercent);
        Assert.AreEqual(AppThemeMode.Dark, settings.ThemeMode);
    }

    [TestMethod]
    public async Task LoadAsync_WhenProtectionModeIsMissing_UsesFixedLockAndDefaultLimiterSettings()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "settings.json"),
            """{"isProtectionEnabled":true,"maxVolumePercent":35}""");
        var store = new JsonSettingsStore(directory);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(ProtectionMode.FixedLock, settings.ProtectionMode);
        Assert.AreEqual(80, settings.LimiterPeakThresholdPercent);
        Assert.AreEqual(65, settings.LimiterReleaseThresholdPercent);
        Assert.AreEqual(10, settings.LimiterMinimumVolumePercent);
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_RoundTripsDynamicLimiterSettings()
    {
        var directory = CreateTempDirectory();
        var store = new JsonSettingsStore(directory);
        var original = new AppSettings(
            false,
            55,
            AppThemeMode.Light,
            AppLanguage.English,
            LockedDeviceId: "device-1",
            ProtectionMode: ProtectionMode.DynamicLimiter,
            LimiterPeakThresholdPercent: 75,
            LimiterReleaseThresholdPercent: 60,
            LimiterMinimumVolumePercent: 15);

        await store.SaveAsync(original, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(ProtectionMode.DynamicLimiter, loaded.ProtectionMode);
        Assert.AreEqual(75, loaded.LimiterPeakThresholdPercent);
        Assert.AreEqual(60, loaded.LimiterReleaseThresholdPercent);
        Assert.AreEqual(15, loaded.LimiterMinimumVolumePercent);
        Assert.AreEqual("device-1", loaded.LockedDeviceId);
    }

    [TestMethod]
    public async Task LoadAsync_WhenLimiterValuesAreOutOfRange_NormalizesThem()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "settings.json"),
            """{"protectionMode":"dynamicLimiter","limiterPeakThresholdPercent":150,"limiterReleaseThresholdPercent":120,"limiterMinimumVolumePercent":-10}""");
        var store = new JsonSettingsStore(directory);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(ProtectionMode.DynamicLimiter, settings.ProtectionMode);
        Assert.AreEqual(100, settings.LimiterPeakThresholdPercent);
        Assert.AreEqual(100, settings.LimiterReleaseThresholdPercent);
        Assert.AreEqual(0, settings.LimiterMinimumVolumePercent);
    }

    [TestMethod]
    public async Task LoadAsync_WhenReleaseThresholdExceedsPeakThreshold_ClampsReleaseToPeak()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "settings.json"),
            """{"limiterPeakThresholdPercent":70,"limiterReleaseThresholdPercent":90}""");
        var store = new JsonSettingsStore(directory);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(70, settings.LimiterPeakThresholdPercent);
        Assert.AreEqual(70, settings.LimiterReleaseThresholdPercent);
    }

    [TestMethod]
    public async Task LoadAsync_WhenProtectionModeIsInvalid_UsesFixedLock()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "settings.json"),
            """{"protectionMode":"banana","maxVolumePercent":35}""");
        var store = new JsonSettingsStore(directory);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(ProtectionMode.FixedLock, settings.ProtectionMode);
        Assert.AreEqual(35, settings.MaxVolumePercent);
    }

    [TestMethod]
    public async Task LoadAsync_WhenProtectionModeIsNotString_UsesFixedLockAndKeepsOtherFields()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "settings.json"),
            """{"isProtectionEnabled":false,"maxVolumePercent":35,"protectionMode":123,"limiterPeakThresholdPercent":75}""");
        var store = new JsonSettingsStore(directory);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.IsFalse(settings.IsProtectionEnabled);
        Assert.AreEqual(35, settings.MaxVolumePercent);
        Assert.AreEqual(ProtectionMode.FixedLock, settings.ProtectionMode);
        Assert.AreEqual(75, settings.LimiterPeakThresholdPercent);
    }

    [TestMethod]
    public async Task LoadAsync_WhenLanguageIsMissing_UsesChinese()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "settings.json"),
            """{"isProtectionEnabled":false,"maxVolumePercent":35,"themeMode":"light"}""");
        var store = new JsonSettingsStore(directory);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(AppLanguage.Chinese, settings.Language);
    }

    [TestMethod]
    public async Task LoadAsync_WhenLanguageExists_LoadsLanguage()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "settings.json"),
            """{"isProtectionEnabled":true,"maxVolumePercent":45,"themeMode":"dark","language":"english"}""");
        var store = new JsonSettingsStore(directory);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(AppLanguage.English, settings.Language);
    }

    [TestMethod]
    public async Task LoadAsync_WhenLanguageIsInvalid_UsesChinese()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "settings.json"),
            """{"isProtectionEnabled":true,"maxVolumePercent":45,"themeMode":"dark","language":"klingon"}""");
        var store = new JsonSettingsStore(directory);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(AppLanguage.Chinese, settings.Language);
    }

    [TestMethod]
    public async Task SaveAsync_WhenSettingsFileExists_ReplacesItWithNewSettings()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "settings.json"),
            """{"isProtectionEnabled":true,"maxVolumePercent":55}""");
        var store = new JsonSettingsStore(directory);

        await store.SaveAsync(new AppSettings(false, 25, AppThemeMode.Light), CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.IsFalse(loaded.IsProtectionEnabled);
        Assert.AreEqual(25, loaded.MaxVolumePercent);
        Assert.AreEqual(AppThemeMode.Light, loaded.ThemeMode);
        Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp").Length);
    }

    [TestMethod]
    public async Task SaveAsync_WhenCanceledBeforeReplace_DoesNotLeaveTempFile()
    {
        var directory = CreateTempDirectory();
        var store = new JsonSettingsStore(directory);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => store.SaveAsync(new AppSettings(false, 20, AppThemeMode.Light), cancellationTokenSource.Token));

        Assert.IsFalse(File.Exists(Path.Combine(directory, "settings.json")));
        Assert.AreEqual(0, Directory.Exists(directory) ? Directory.GetFiles(directory, "*.tmp").Length : 0);
    }

    [TestMethod]
    public async Task LoadAsync_WhenVolumeIsOutOfRange_ClampsValue()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "settings.json"),
            """{"isProtectionEnabled":true,"maxVolumePercent":150}""");
        var store = new JsonSettingsStore(directory);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(100, settings.MaxVolumePercent);
        Assert.AreEqual(AppThemeMode.Dark, settings.ThemeMode);
    }

    [TestMethod]
    public async Task LoadAsync_WhenOldMeetingOrLimiterFieldsExist_IgnoresThem()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "settings.json"),
            """{"isProtectionEnabled":true,"maxVolumePercent":150,"limiterThresholdDb":-18,"limiterRatio":6,"isMeetingModeEnabled":true,"meetingSafeVolumePercent":200}""");
        var store = new JsonSettingsStore(directory);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.IsTrue(settings.IsProtectionEnabled);
        Assert.AreEqual(100, settings.MaxVolumePercent);
        Assert.AreEqual(AppThemeMode.Dark, settings.ThemeMode);
    }

    [TestMethod]
    public async Task LoadAsync_WhenJsonIsEmptyObject_ReturnsDefaults()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "settings.json"), "{}");
        var store = new JsonSettingsStore(directory);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.IsTrue(settings.IsProtectionEnabled);
        Assert.AreEqual(40, settings.MaxVolumePercent);
        Assert.AreEqual(AppThemeMode.Dark, settings.ThemeMode);
    }

    [TestMethod]
    public async Task LoadAsync_WhenJsonRootIsNotObject_ReturnsDefaults()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "settings.json"), "[]");
        var store = new JsonSettingsStore(directory);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.IsTrue(settings.IsProtectionEnabled);
        Assert.AreEqual(40, settings.MaxVolumePercent);
        Assert.AreEqual(AppThemeMode.Dark, settings.ThemeMode);
        Assert.AreEqual(ProtectionMode.FixedLock, settings.ProtectionMode);
    }

    [TestMethod]
    public async Task LoadAsync_WhenProtectionModeIsNumericString_UsesFixedLockAndKeepsOtherFields()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "settings.json"),
            """{"maxVolumePercent":35,"protectionMode":"1"}""");
        var store = new JsonSettingsStore(directory);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(35, settings.MaxVolumePercent);
        Assert.AreEqual(ProtectionMode.FixedLock, settings.ProtectionMode);
    }

    [TestMethod]
    public async Task SaveAsync_WhenCanceledBeforeWrite_DoesNotOverwriteExistingFile()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        var settingsPath = Path.Combine(directory, "settings.json");
        const string originalJson = """{"isProtectionEnabled":true,"maxVolumePercent":55}""";
        await File.WriteAllTextAsync(settingsPath, originalJson);
        var store = new JsonSettingsStore(directory);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => store.SaveAsync(new AppSettings(false, 20, AppThemeMode.Light), cancellationTokenSource.Token));

        Assert.AreEqual(originalJson, await File.ReadAllTextAsync(settingsPath));
    }

    [TestMethod]
    public async Task SaveAsync_WritesNormalizedCamelCaseIndentedJson()
    {
        var directory = CreateTempDirectory();
        var store = new JsonSettingsStore(directory);

        await store.SaveAsync(new AppSettings(false, 150, AppThemeMode.Light, AppLanguage.English), CancellationToken.None);

        var json = await File.ReadAllTextAsync(Path.Combine(directory, "settings.json"));
        StringAssert.Contains(json, "\"isProtectionEnabled\": false");
        StringAssert.Contains(json, "\"maxVolumePercent\": 100");
        StringAssert.Contains(json, "\"themeMode\": \"light\"");
        StringAssert.Contains(json, "\"language\": \"english\"");
        StringAssert.Contains(json, "\"protectionMode\": \"fixedLock\"");
        StringAssert.Contains(json, "\"limiterPeakThresholdPercent\": 80");
        StringAssert.Contains(json, "\"limiterReleaseThresholdPercent\": 65");
        StringAssert.Contains(json, "\"limiterMinimumVolumePercent\": 10");
        StringAssert.DoesNotMatch(json, new System.Text.RegularExpressions.Regex("meeting", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        StringAssert.DoesNotMatch(json, new System.Text.RegularExpressions.Regex("IsProtectionEnabled"));
    }

    [TestMethod]
    public async Task LoadAsync_WhenThemeModeExists_LoadsThemeMode()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "settings.json"),
            """{"isProtectionEnabled":true,"maxVolumePercent":45,"themeMode":"system"}""");
        var store = new JsonSettingsStore(directory);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(AppThemeMode.System, settings.ThemeMode);
    }

    [TestMethod]
    public async Task LoadAsync_WhenThemeModeIsInvalid_UsesDefaultTheme()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "settings.json"),
            """{"isProtectionEnabled":false,"maxVolumePercent":45,"themeMode":"neon"}""");
        var store = new JsonSettingsStore(directory);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.IsFalse(settings.IsProtectionEnabled);
        Assert.AreEqual(45, settings.MaxVolumePercent);
        Assert.AreEqual(AppThemeMode.Dark, settings.ThemeMode);
    }

    private static string CreateTempDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "NSJLock.Tests", Guid.NewGuid().ToString("N"));
    }
}
