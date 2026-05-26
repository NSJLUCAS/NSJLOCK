namespace NSJLock.Config;

public sealed record AppSettings(
    bool IsProtectionEnabled,
    int MaxVolumePercent,
    AppThemeMode ThemeMode,
    AppLanguage Language = AppLanguage.Chinese,
    string? LockedDeviceId = null)
{
    public static AppSettings Defaults { get; } = new(
        true,
        40,
        AppThemeMode.Dark,
        AppLanguage.Chinese,
        null);

    public AppSettings Normalize()
    {
        return this with
        {
            MaxVolumePercent = ClampPercent(MaxVolumePercent),
            ThemeMode = NormalizeThemeMode(ThemeMode),
            Language = NormalizeLanguage(Language),
            LockedDeviceId = NormalizeLockedDeviceId(LockedDeviceId)
        };
    }

    private static int ClampPercent(int value)
    {
        return Math.Clamp(value, 0, 100);
    }

    private static AppThemeMode NormalizeThemeMode(AppThemeMode value)
    {
        return Enum.IsDefined(value) ? value : Defaults.ThemeMode;
    }

    private static AppLanguage NormalizeLanguage(AppLanguage value)
    {
        return Enum.IsDefined(value) ? value : Defaults.Language;
    }

    private static string? NormalizeLockedDeviceId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
