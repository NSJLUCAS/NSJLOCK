namespace NSJLock.Config;

public sealed record AppSettings(
    bool IsProtectionEnabled,
    int MaxVolumePercent,
    AppThemeMode ThemeMode,
    AppLanguage Language = AppLanguage.Chinese,
    string? LockedDeviceId = null,
    ProtectionMode ProtectionMode = ProtectionMode.FixedLock,
    int LimiterPeakThresholdPercent = 80,
    int LimiterReleaseThresholdPercent = 65,
    int LimiterMinimumVolumePercent = 10)
{
    public static AppSettings Defaults { get; } = new(
        true,
        40,
        AppThemeMode.Dark,
        AppLanguage.Chinese,
        null,
        ProtectionMode.FixedLock,
        80,
        65,
        10);

    public AppSettings Normalize()
    {
        var limiterPeakThresholdPercent = ClampPercent(LimiterPeakThresholdPercent);
        var limiterReleaseThresholdPercent = Math.Min(
            ClampPercent(LimiterReleaseThresholdPercent),
            limiterPeakThresholdPercent);

        return this with
        {
            MaxVolumePercent = ClampPercent(MaxVolumePercent),
            ThemeMode = NormalizeThemeMode(ThemeMode),
            Language = NormalizeLanguage(Language),
            LockedDeviceId = NormalizeLockedDeviceId(LockedDeviceId),
            ProtectionMode = NormalizeProtectionMode(ProtectionMode),
            LimiterPeakThresholdPercent = limiterPeakThresholdPercent,
            LimiterReleaseThresholdPercent = limiterReleaseThresholdPercent,
            LimiterMinimumVolumePercent = ClampPercent(LimiterMinimumVolumePercent)
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

    private static ProtectionMode NormalizeProtectionMode(ProtectionMode value)
    {
        return Enum.IsDefined(value) ? value : Defaults.ProtectionMode;
    }

    private static string? NormalizeLockedDeviceId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
