namespace NSJLock.Audio;

public enum DynamicLimiterStatus
{
    Paused,
    NoDevice,
    LevelReadFailed,
    Monitoring,
    Limiting,
    Restoring,
    Restored
}
