namespace NSJLock.Audio;

public sealed record DynamicLimiterTickInput(
    bool IsProtectionEnabled,
    bool HasDevice,
    bool IsPeakAvailable,
    int CurrentVolumePercent,
    int PeakPercent,
    DynamicLimiterState State);
