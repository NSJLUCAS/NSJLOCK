namespace NSJLock.Audio;

public sealed record AudioEndpointSnapshot(
    string? DeviceName,
    int CurrentVolumePercent,
    bool HasDefaultDevice,
    string? ErrorMessage);
