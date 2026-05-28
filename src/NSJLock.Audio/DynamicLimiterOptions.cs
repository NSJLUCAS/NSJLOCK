namespace NSJLock.Audio;

public sealed record DynamicLimiterOptions(
    int PeakThresholdPercent,
    int ReleaseThresholdPercent,
    int MinimumVolumePercent,
    int AttackStepPercent,
    int ReleaseStepPercent,
    int ReleaseSafeTickCount)
{
    public static DynamicLimiterOptions Defaults { get; } = new(80, 65, 10, 10, 3, 3);

    public DynamicLimiterOptions Normalize()
    {
        var peakThreshold = ClampPercent(PeakThresholdPercent);
        var releaseThreshold = Math.Min(ClampPercent(ReleaseThresholdPercent), peakThreshold);

        return this with
        {
            PeakThresholdPercent = peakThreshold,
            ReleaseThresholdPercent = releaseThreshold,
            MinimumVolumePercent = ClampPercent(MinimumVolumePercent),
            AttackStepPercent = Math.Clamp(AttackStepPercent, 1, 100),
            ReleaseStepPercent = Math.Clamp(ReleaseStepPercent, 1, 100),
            ReleaseSafeTickCount = Math.Max(1, ReleaseSafeTickCount)
        };
    }

    private static int ClampPercent(int value)
    {
        return Math.Clamp(value, 0, 100);
    }
}
