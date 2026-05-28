namespace NSJLock.Audio;

public sealed record DynamicLimiterState(
    int UserTargetVolumePercent,
    bool IsLimited,
    int SafeTickCount,
    int? LastProgramWriteVolumePercent)
{
    public static DynamicLimiterState Create(int currentVolumePercent)
    {
        return new DynamicLimiterState(Math.Clamp(currentVolumePercent, 0, 100), false, 0, null);
    }
}
