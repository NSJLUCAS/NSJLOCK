namespace NSJLock.Audio;

public sealed class DynamicLimiterEngine
{
    private readonly DynamicLimiterOptions options;

    public DynamicLimiterEngine(DynamicLimiterOptions options)
    {
        this.options = (options ?? throw new ArgumentNullException(nameof(options))).Normalize();
    }

    public DynamicLimiterTickResult Tick(DynamicLimiterTickInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.State);

        var currentVolume = ClampPercent(input.CurrentVolumePercent);
        var peak = ClampPercent(input.PeakPercent);
        var state = TrackUserTarget(input.State, currentVolume);

        if (!input.HasDevice)
        {
            return new DynamicLimiterTickResult(state, null, DynamicLimiterStatus.NoDevice);
        }

        if (!input.IsProtectionEnabled)
        {
            return new DynamicLimiterTickResult(
                DynamicLimiterState.Create(currentVolume),
                null,
                DynamicLimiterStatus.Paused);
        }

        if (!input.IsPeakAvailable)
        {
            return new DynamicLimiterTickResult(state, null, DynamicLimiterStatus.LevelReadFailed);
        }

        if (peak >= options.PeakThresholdPercent)
        {
            var limitedState = state with
            {
                IsLimited = true,
                SafeTickCount = 0
            };

            if (currentVolume < options.MinimumVolumePercent)
            {
                return new DynamicLimiterTickResult(limitedState, null, DynamicLimiterStatus.Limiting);
            }

            var limitedVolume = Math.Max(options.MinimumVolumePercent, currentVolume - options.AttackStepPercent);
            limitedState = limitedState with
            {
                LastProgramWriteVolumePercent = limitedVolume
            };

            return new DynamicLimiterTickResult(limitedState, limitedVolume, DynamicLimiterStatus.Limiting);
        }

        if (!state.IsLimited)
        {
            return new DynamicLimiterTickResult(
                DynamicLimiterState.Create(currentVolume),
                null,
                DynamicLimiterStatus.Monitoring);
        }

        if (peak > options.ReleaseThresholdPercent)
        {
            return new DynamicLimiterTickResult(
                state with { SafeTickCount = 0 },
                null,
                DynamicLimiterStatus.Limiting);
        }

        var safeState = state with { SafeTickCount = state.SafeTickCount + 1 };
        if (safeState.SafeTickCount < options.ReleaseSafeTickCount)
        {
            return new DynamicLimiterTickResult(safeState, null, DynamicLimiterStatus.Monitoring);
        }

        if (currentVolume >= safeState.UserTargetVolumePercent)
        {
            var completeState = safeState with
            {
                IsLimited = false,
                SafeTickCount = 0,
                LastProgramWriteVolumePercent = null
            };

            return new DynamicLimiterTickResult(completeState, null, DynamicLimiterStatus.Restored);
        }

        var restoredVolume = Math.Min(safeState.UserTargetVolumePercent, currentVolume + options.ReleaseStepPercent);
        var isStillLimited = restoredVolume < safeState.UserTargetVolumePercent;
        var restoredState = safeState with
        {
            IsLimited = isStillLimited,
            SafeTickCount = isStillLimited ? safeState.SafeTickCount : 0,
            LastProgramWriteVolumePercent = restoredVolume
        };

        return new DynamicLimiterTickResult(
            restoredState,
            restoredVolume,
            isStillLimited ? DynamicLimiterStatus.Restoring : DynamicLimiterStatus.Restored);
    }

    private static DynamicLimiterState TrackUserTarget(DynamicLimiterState state, int currentVolume)
    {
        if (!state.IsLimited)
        {
            return state with { UserTargetVolumePercent = currentVolume };
        }

        if (state.LastProgramWriteVolumePercent is null ||
            state.LastProgramWriteVolumePercent.Value != currentVolume)
        {
            return state with { UserTargetVolumePercent = currentVolume };
        }

        return state;
    }

    private static int ClampPercent(int value)
    {
        return Math.Clamp(value, 0, 100);
    }
}
