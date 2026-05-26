namespace NSJLock.Audio;

public sealed record AudioOutputDevice(
    string Id,
    string Name,
    bool IsDefault);
