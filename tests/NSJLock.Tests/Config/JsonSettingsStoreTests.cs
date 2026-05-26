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
        StringAssert.DoesNotMatch(json, new System.Text.RegularExpressions.Regex("limiter", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
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
