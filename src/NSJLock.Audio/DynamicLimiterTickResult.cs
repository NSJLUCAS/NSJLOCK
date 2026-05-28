namespace NSJLock.Audio;

public sealed record DynamicLimiterTickResult(
    DynamicLimiterState State,
    int? VolumeToWritePercent,
    DynamicLimiterStatus Status);
