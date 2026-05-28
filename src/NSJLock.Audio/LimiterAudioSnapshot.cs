namespace NSJLock.Audio;

public sealed record LimiterAudioSnapshot(
    string? DeviceName,
    int CurrentVolumePercent,
    int PeakPercent,
    bool HasDefaultDevice,
    bool IsPeakAvailable,
    string? ErrorMessage,
    string? DeviceId = null);
