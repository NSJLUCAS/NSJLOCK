namespace NSJLock.Audio;

public enum ProtectionTickStatus
{
    AudioReadFailed,
    NoDefaultDevice,
    ProtectionPaused,
    BaselineUpdated,
    Protecting,
    VolumeWriteFailed,
    VolumeAdjusted,
    LevelReadFailed,
    DynamicMonitoring,
    DynamicLimited,
    DynamicRestoring,
    DynamicRestored
}
