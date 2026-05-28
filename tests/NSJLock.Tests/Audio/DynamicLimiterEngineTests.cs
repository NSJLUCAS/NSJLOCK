using NSJLock.Audio;

namespace NSJLock.Tests.Audio;

[TestClass]
public sealed class DynamicLimiterEngineTests
{
    [TestMethod]
    public void Tick_WhenPeakIsSafe_DoesNotWriteVolumeAndTracksUserTarget()
    {
        var engine = new DynamicLimiterEngine(DynamicLimiterOptions.Defaults);
        var state = DynamicLimiterState.Create(60);

        var result = engine.Tick(new DynamicLimiterTickInput(
            IsProtectionEnabled: true,
            HasDevice: true,
            IsPeakAvailable: true,
            CurrentVolumePercent: 70,
            PeakPercent: 40,
            State: state));

        Assert.IsNull(result.VolumeToWritePercent);
        Assert.AreEqual(70, result.State.UserTargetVolumePercent);
        Assert.AreEqual(DynamicLimiterStatus.Monitoring, result.Status);
    }

    [TestMethod]
    public void Tick_WhenPeakExceedsThreshold_LowersVolumeByAttackStep()
    {
        var engine = new DynamicLimiterEngine(DynamicLimiterOptions.Defaults);
        var state = DynamicLimiterState.Create(70);

        var result = engine.Tick(new DynamicLimiterTickInput(
            IsProtectionEnabled: true,
            HasDevice: true,
            IsPeakAvailable: true,
            CurrentVolumePercent: 70,
            PeakPercent: 90,
            State: state));

        Assert.AreEqual(60, result.VolumeToWritePercent);
        Assert.AreEqual(70, result.State.UserTargetVolumePercent);
        Assert.AreEqual(60, result.State.LastProgramWriteVolumePercent);
        Assert.AreEqual(DynamicLimiterStatus.Limiting, result.Status);
    }

    [TestMethod]
    public void Tick_WhenPeakStaysHigh_DoesNotGoBelowMinimumVolume()
    {
        var options = DynamicLimiterOptions.Defaults with { MinimumVolumePercent = 10, AttackStepPercent = 10 };
        var engine = new DynamicLimiterEngine(options);
        var state = DynamicLimiterState.Create(70);

        var first = engine.Tick(new DynamicLimiterTickInput(true, true, true, 15, 95, state));
        var second = engine.Tick(new DynamicLimiterTickInput(true, true, true, 10, 95, first.State));

        Assert.AreEqual(10, first.VolumeToWritePercent);
        Assert.AreEqual(10, second.VolumeToWritePercent);
        Assert.AreEqual(DynamicLimiterStatus.Limiting, second.Status);
    }

    [TestMethod]
    public void Tick_WhenPeakIsHighAndCurrentVolumeIsBelowMinimum_DoesNotRaiseVolume()
    {
        var options = DynamicLimiterOptions.Defaults with { MinimumVolumePercent = 10, AttackStepPercent = 10 };
        var engine = new DynamicLimiterEngine(options);
        var state = DynamicLimiterState.Create(70);

        var result = engine.Tick(new DynamicLimiterTickInput(true, true, true, 5, 95, state));

        Assert.IsNull(result.VolumeToWritePercent);
        Assert.AreEqual(5, result.State.UserTargetVolumePercent);
        Assert.IsTrue(result.State.IsLimited);
        Assert.AreEqual(DynamicLimiterStatus.Limiting, result.Status);
    }

    [TestMethod]
    public void Tick_WhenLimitedAndUserChangedVolumeDuringHighPeak_TracksNewUserTargetBeforeLimiting()
    {
        var options = DynamicLimiterOptions.Defaults with { MinimumVolumePercent = 10, AttackStepPercent = 10 };
        var engine = new DynamicLimiterEngine(options);
        var state = DynamicLimiterState.Create(70) with
        {
            IsLimited = true,
            LastProgramWriteVolumePercent = 40
        };

        var result = engine.Tick(new DynamicLimiterTickInput(true, true, true, 55, 95, state));

        Assert.AreEqual(45, result.VolumeToWritePercent);
        Assert.AreEqual(55, result.State.UserTargetVolumePercent);
        Assert.AreEqual(45, result.State.LastProgramWriteVolumePercent);
        Assert.AreEqual(DynamicLimiterStatus.Limiting, result.Status);
    }

    [TestMethod]
    public void Tick_WhenPeakIsSafeForRequiredTicks_RestoresTowardUserTarget()
    {
        var options = DynamicLimiterOptions.Defaults with
        {
            ReleaseSafeTickCount = 2,
            ReleaseStepPercent = 3
        };
        var engine = new DynamicLimiterEngine(options);
        var state = DynamicLimiterState.Create(70) with
        {
            IsLimited = true,
            LastProgramWriteVolumePercent = 40
        };

        var first = engine.Tick(new DynamicLimiterTickInput(true, true, true, 40, 50, state));
        var second = engine.Tick(new DynamicLimiterTickInput(true, true, true, 40, 50, first.State));

        Assert.IsNull(first.VolumeToWritePercent);
        Assert.AreEqual(43, second.VolumeToWritePercent);
        Assert.AreEqual(DynamicLimiterStatus.Restoring, second.Status);
    }

    [TestMethod]
    public void Tick_WhenRestoringAndUserChangedVolume_DoesNotRestoreAboveNewUserTarget()
    {
        var options = DynamicLimiterOptions.Defaults with { ReleaseSafeTickCount = 1, ReleaseStepPercent = 10 };
        var engine = new DynamicLimiterEngine(options);
        var state = DynamicLimiterState.Create(70) with
        {
            IsLimited = true,
            LastProgramWriteVolumePercent = 40
        };

        var result = engine.Tick(new DynamicLimiterTickInput(true, true, true, 50, 50, state));

        Assert.IsNull(result.VolumeToWritePercent);
        Assert.AreEqual(50, result.State.UserTargetVolumePercent);
        Assert.IsFalse(result.State.IsLimited);
        Assert.AreEqual(DynamicLimiterStatus.Restored, result.Status);
    }

    [TestMethod]
    public void Tick_WhenRestoring_DoesNotExceedUserTarget()
    {
        var options = DynamicLimiterOptions.Defaults with { ReleaseSafeTickCount = 1, ReleaseStepPercent = 10 };
        var engine = new DynamicLimiterEngine(options);
        var state = DynamicLimiterState.Create(45) with
        {
            IsLimited = true,
            LastProgramWriteVolumePercent = 40
        };

        var result = engine.Tick(new DynamicLimiterTickInput(true, true, true, 40, 50, state));

        Assert.AreEqual(45, result.VolumeToWritePercent);
        Assert.AreEqual(DynamicLimiterStatus.Restored, result.Status);
        Assert.IsFalse(result.State.IsLimited);
    }

    [TestMethod]
    public void Tick_WhenDeviceOrPeakIsUnavailable_DoesNotWriteVolume()
    {
        var engine = new DynamicLimiterEngine(DynamicLimiterOptions.Defaults);
        var state = DynamicLimiterState.Create(50);

        var noDevice = engine.Tick(new DynamicLimiterTickInput(true, false, true, 50, 90, state));
        var noPeak = engine.Tick(new DynamicLimiterTickInput(true, true, false, 50, 90, state));

        Assert.IsNull(noDevice.VolumeToWritePercent);
        Assert.AreEqual(DynamicLimiterStatus.NoDevice, noDevice.Status);
        Assert.IsNull(noPeak.VolumeToWritePercent);
        Assert.AreEqual(DynamicLimiterStatus.LevelReadFailed, noPeak.Status);
    }

    [TestMethod]
    public void Tick_WhenProtectionIsPaused_DoesNotWriteAndUpdatesUserTarget()
    {
        var engine = new DynamicLimiterEngine(DynamicLimiterOptions.Defaults);
        var state = DynamicLimiterState.Create(40);

        var result = engine.Tick(new DynamicLimiterTickInput(false, true, true, 65, 90, state));

        Assert.IsNull(result.VolumeToWritePercent);
        Assert.AreEqual(65, result.State.UserTargetVolumePercent);
        Assert.AreEqual(DynamicLimiterStatus.Paused, result.Status);
    }

    [TestMethod]
    public void Normalize_WhenValuesAreOutOfRange_ClampsLimiterOptions()
    {
        var options = new DynamicLimiterOptions(
            PeakThresholdPercent: 150,
            ReleaseThresholdPercent: 120,
            MinimumVolumePercent: -10,
            AttackStepPercent: 0,
            ReleaseStepPercent: -5,
            ReleaseSafeTickCount: 0);

        var normalized = options.Normalize();

        Assert.AreEqual(100, normalized.PeakThresholdPercent);
        Assert.AreEqual(100, normalized.ReleaseThresholdPercent);
        Assert.AreEqual(0, normalized.MinimumVolumePercent);
        Assert.AreEqual(1, normalized.AttackStepPercent);
        Assert.AreEqual(1, normalized.ReleaseStepPercent);
        Assert.AreEqual(1, normalized.ReleaseSafeTickCount);
    }

    [TestMethod]
    public void Normalize_WhenReleaseThresholdExceedsClampedPeak_LowersReleaseThresholdToPeak()
    {
        var options = new DynamicLimiterOptions(
            PeakThresholdPercent: 60,
            ReleaseThresholdPercent: 90,
            MinimumVolumePercent: 10,
            AttackStepPercent: 10,
            ReleaseStepPercent: 3,
            ReleaseSafeTickCount: 3);

        var normalized = options.Normalize();

        Assert.AreEqual(60, normalized.PeakThresholdPercent);
        Assert.AreEqual(60, normalized.ReleaseThresholdPercent);
    }

    [TestMethod]
    public void Tick_WhenStateIsNull_ThrowsArgumentNullException()
    {
        var engine = new DynamicLimiterEngine(DynamicLimiterOptions.Defaults);
        var input = new DynamicLimiterTickInput(true, true, true, 50, 50, null!);

        Assert.ThrowsException<ArgumentNullException>(() => engine.Tick(input));
    }
}
