namespace NSJLock.Audio;

internal sealed record AudioSessionDiagnosticInfo(
    string DisplayName,
    string? ProcessName,
    int ProcessId,
    int VolumePercent,
    bool IsMuted);
