namespace NSJLock.Audio;

public enum ProtectionTickStatus
{
    AudioReadFailed,
    NoDefaultDevice,
    ProtectionPaused,
    BaselineUpdated,
    Protecting,
    VolumeWriteFailed,
    VolumeAdjusted
}
