namespace NSJLock.Audio;

public sealed record ProtectionTickResult(
    string DeviceName,
    int CurrentVolumePercent,
    bool HasDefaultDevice,
    bool WasLimited,
    DateTimeOffset? LastLimitedAt,
    ProtectionTickStatus StatusCode,
    string? StatusDetail,
    ProtectionMode ProtectionMode = ProtectionMode.FixedLock,
    int? CurrentPeakPercent = null,
    int? UserTargetVolumePercent = null);
