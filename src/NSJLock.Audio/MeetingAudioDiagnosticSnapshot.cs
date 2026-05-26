namespace NSJLock.Audio;

public sealed record MeetingAudioDiagnosticSnapshot(
    string DeviceName,
    int SystemVolumePercent,
    bool HasDefaultDevice,
    bool HasZoomSession,
    int? ZoomVolumePercent,
    bool? IsZoomMuted,
    string? ZoomDeviceName,
    int? ZoomDeviceVolumePercent,
    bool IsZoomOnLockedDevice,
    string? ErrorMessage,
    string? SystemDefaultDeviceName = null);
