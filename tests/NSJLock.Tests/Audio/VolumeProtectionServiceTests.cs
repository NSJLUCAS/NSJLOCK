using NSJLock.Audio;

namespace NSJLock.Tests.Audio;

[TestClass]
public sealed class VolumeProtectionServiceTests
{
    [TestMethod]
    public void CheckOnce_WhenProtectionDisabled_DoesNotWriteVolume()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 80, true, null));
        var service = new VolumeProtectionService(controller);

        var result = service.CheckOnce(false, 40, new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero));

        Assert.AreEqual("Speakers", result.DeviceName);
        Assert.AreEqual(80, result.CurrentVolumePercent);
        Assert.IsFalse(result.WasLimited);
        Assert.AreEqual(0, controller.SetCalls.Count);
    }

    [TestMethod]
    public void CheckOnce_WhenProtectionDisabled_UsesBasicSnapshotAndReleasesAudioSampling()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 80, true, null));
        var service = new VolumeProtectionService(controller);

        var result = service.CheckOnce(false, 40, new DateTimeOffset(2026, 5, 24, 10, 0, 0, TimeSpan.Zero));

        Assert.AreEqual("Speakers", result.DeviceName);
        Assert.AreEqual(80, result.CurrentVolumePercent);
        Assert.AreEqual(1, controller.BasicSnapshotCalls);
        Assert.AreEqual(0, controller.FullSnapshotCalls);
        Assert.AreEqual(1, controller.ReleaseAudioSamplingCalls);
    }

    [TestMethod]
    public void CheckOnce_WhenVolumeIsAboveTarget_WritesTargetVolume()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Headset", 100, true, null));
        var service = new VolumeProtectionService(controller);

        var result = service.CheckOnce(true, 40, new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero));

        Assert.IsTrue(result.WasLimited);
        Assert.AreEqual(1, controller.SetCalls.Count);
        Assert.AreEqual(40, controller.SetCalls[0]);
        Assert.AreEqual(40, result.CurrentVolumePercent);
    }

    [TestMethod]
    public void CheckOnce_WhenVolumeIsBelowTarget_WritesTargetVolume()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 35, true, null));
        var service = new VolumeProtectionService(controller);

        var result = service.CheckOnce(true, 40, new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero));

        Assert.IsTrue(result.WasLimited);
        Assert.AreEqual(1, controller.SetCalls.Count);
        Assert.AreEqual(40, controller.SetCalls[0]);
        Assert.AreEqual(40, result.CurrentVolumePercent);
    }

    [TestMethod]
    public void CheckOnce_WhenNoDefaultDevice_ReturnsNoDeviceStatus()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot(null, 0, false, null));
        var service = new VolumeProtectionService(controller);

        var result = service.CheckOnce(true, 40, new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero));

        Assert.AreEqual("未检测到默认输出设备", result.DeviceName);
        Assert.IsFalse(result.WasLimited);
        Assert.AreEqual(0, controller.SetCalls.Count);
    }

    [TestMethod]
    public void CheckOnce_WhenGetSnapshotThrows_ReturnsAudioReadFailureStatus()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 60, true, null))
        {
            SnapshotException = new InvalidOperationException("设备读取失败")
        };
        var service = new VolumeProtectionService(controller);

        var result = service.CheckOnce(true, 40, new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero));

        Assert.AreEqual("默认输出设备", result.DeviceName);
        Assert.IsFalse(result.WasLimited);
        Assert.AreEqual(0, controller.SetCalls.Count);
    }

    [TestMethod]
    public void CheckOnce_WhenSetVolumeThrowsInvalidOperationException_ReturnsFailureStatus()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 100, true, null))
        {
            SetException = new InvalidOperationException("测试设置失败")
        };
        var service = new VolumeProtectionService(controller);

        var result = service.CheckOnce(true, 40, new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero));

        Assert.AreEqual("Speakers", result.DeviceName);
        Assert.IsFalse(result.WasLimited);
        Assert.AreEqual(1, controller.SetCalls.Count);
        Assert.AreEqual(ProtectionTickStatus.VolumeWriteFailed, result.StatusCode);
        Assert.AreEqual("测试设置失败", result.StatusDetail);
    }

    [TestMethod]
    public void CheckOnce_WhenProtectionTurnsBackOn_LocksConfiguredTarget()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Headset", 100, true, null));
        var service = new VolumeProtectionService(controller);
        var first = service.CheckOnce(true, 40, new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero));
        controller.Snapshot = new AudioEndpointSnapshot("Headset", 45, true, null);

        var second = service.CheckOnce(false, 40, new DateTimeOffset(2026, 5, 22, 10, 0, 1, TimeSpan.Zero));
        var third = service.CheckOnce(true, 40, new DateTimeOffset(2026, 5, 22, 10, 0, 2, TimeSpan.Zero));

        Assert.IsTrue(first.WasLimited);
        Assert.AreEqual(45, second.CurrentVolumePercent);
        Assert.IsFalse(second.WasLimited);
        Assert.IsTrue(third.WasLimited);
        Assert.AreEqual(40, third.CurrentVolumePercent);
    }

    [TestMethod]
    public void CheckOnce_WhenFixedLockMode_WritesExactTargetVolume()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 68, true, null));
        var service = new VolumeProtectionService(controller);

        var result = service.CheckOnce(
            true,
            40,
            new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero),
            protectionMode: ProtectionMode.FixedLock);

        Assert.IsTrue(result.WasLimited);
        Assert.AreEqual(1, controller.SetCalls.Count);
        Assert.AreEqual(40, controller.SetCalls[0]);
        Assert.AreEqual(40, result.CurrentVolumePercent);
        Assert.AreEqual(ProtectionTickStatus.VolumeAdjusted, result.StatusCode);
        Assert.AreEqual(ProtectionMode.FixedLock, result.ProtectionMode);
        Assert.IsNull(result.CurrentPeakPercent);
        Assert.IsNull(result.UserTargetVolumePercent);
        Assert.AreEqual(1, controller.BasicSnapshotCalls);
        Assert.AreEqual(0, controller.LimiterSnapshotCalls);
    }

    [TestMethod]
    public void CheckOnce_WhenDynamicLimiterPeakIsSafe_DoesNotWriteVolume()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 70, true, null))
        {
            LimiterSnapshot = new LimiterAudioSnapshot("Speakers", 70, 40, true, true, null)
        };
        var service = new VolumeProtectionService(controller);

        var result = service.CheckOnce(
            true,
            80,
            new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);

        Assert.AreEqual("Speakers", result.DeviceName);
        Assert.AreEqual(70, result.CurrentVolumePercent);
        Assert.AreEqual(40, result.CurrentPeakPercent);
        Assert.AreEqual(70, result.UserTargetVolumePercent);
        Assert.IsTrue(result.HasDefaultDevice);
        Assert.IsFalse(result.WasLimited);
        Assert.AreEqual(ProtectionMode.DynamicLimiter, result.ProtectionMode);
        Assert.AreEqual(ProtectionTickStatus.DynamicMonitoring, result.StatusCode);
        Assert.AreEqual(0, controller.SetCalls.Count);
        Assert.AreEqual(0, controller.BasicSnapshotCalls);
        Assert.AreEqual(1, controller.LimiterSnapshotCalls);
    }

    [TestMethod]
    public void CheckOnce_WhenDynamicLimiterPeakExceedsSliderThreshold_WritesLimitedVolume()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 70, true, null))
        {
            LimiterSnapshot = new LimiterAudioSnapshot("Speakers", 70, 60, true, true, null)
        };
        var service = new VolumeProtectionService(controller);

        var result = service.CheckOnce(
            true,
            50,
            new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);

        Assert.AreEqual(1, controller.SetCalls.Count);
        Assert.AreEqual(60, controller.SetCalls[0]);
        Assert.AreEqual(60, result.CurrentVolumePercent);
        Assert.AreEqual(60, result.CurrentPeakPercent);
        Assert.IsTrue(result.WasLimited);
        Assert.AreEqual(ProtectionTickStatus.DynamicLimited, result.StatusCode);
    }

    [TestMethod]
    public void CheckOnce_WhenDynamicLimiterPeakIsBelowSliderThreshold_DoesNotWriteVolume()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 70, true, null))
        {
            LimiterSnapshot = new LimiterAudioSnapshot("Speakers", 70, 49, true, true, null)
        };
        var service = new VolumeProtectionService(controller);

        var result = service.CheckOnce(
            true,
            50,
            new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);

        Assert.AreEqual(0, controller.SetCalls.Count);
        Assert.AreEqual(70, result.CurrentVolumePercent);
        Assert.AreEqual(49, result.CurrentPeakPercent);
        Assert.IsFalse(result.WasLimited);
        Assert.AreEqual(ProtectionTickStatus.DynamicMonitoring, result.StatusCode);
    }

    [TestMethod]
    public void CheckOnce_WhenDynamicLimiterPeakIsHigh_WritesEngineRequestedVolume()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 70, true, null))
        {
            LimiterSnapshot = new LimiterAudioSnapshot("Speakers", 70, 90, true, true, null)
        };
        var service = new VolumeProtectionService(controller);

        var result = service.CheckOnce(
            true,
            80,
            new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);

        Assert.AreEqual(1, controller.SetCalls.Count);
        Assert.AreEqual(60, controller.SetCalls[0]);
        Assert.AreEqual(60, result.CurrentVolumePercent);
        Assert.AreEqual(90, result.CurrentPeakPercent);
        Assert.AreEqual(70, result.UserTargetVolumePercent);
        Assert.IsTrue(result.WasLimited);
        Assert.AreEqual(ProtectionTickStatus.DynamicLimited, result.StatusCode);
        Assert.AreEqual(ProtectionMode.DynamicLimiter, result.ProtectionMode);
    }

    [TestMethod]
    public void CheckOnce_WhenDynamicLimiterPeakIsUnavailable_DoesNotWriteVolumeAndReturnsLevelReadFailure()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 70, true, null))
        {
            LimiterSnapshot = new LimiterAudioSnapshot("Speakers", 70, 0, true, false, "peak read failed")
        };
        var service = new VolumeProtectionService(controller);

        var result = service.CheckOnce(
            true,
            80,
            new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);

        Assert.AreEqual("Speakers", result.DeviceName);
        Assert.AreEqual(70, result.CurrentVolumePercent);
        Assert.AreEqual(0, result.CurrentPeakPercent);
        Assert.AreEqual(70, result.UserTargetVolumePercent);
        Assert.IsTrue(result.HasDefaultDevice);
        Assert.IsFalse(result.WasLimited);
        Assert.AreEqual(ProtectionTickStatus.LevelReadFailed, result.StatusCode);
        Assert.AreEqual("peak read failed", result.StatusDetail);
        Assert.AreEqual(0, controller.SetCalls.Count);
    }

    [TestMethod]
    public void CheckOnce_WhenDynamicLimiterHasNoDevice_DoesNotWriteVolume()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot(null, 0, false, null))
        {
            LimiterSnapshot = new LimiterAudioSnapshot(null, 0, 0, false, false, null)
        };
        var service = new VolumeProtectionService(controller);

        var result = service.CheckOnce(
            true,
            40,
            new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);

        Assert.IsFalse(result.HasDefaultDevice);
        Assert.IsFalse(result.WasLimited);
        Assert.AreEqual(ProtectionTickStatus.NoDefaultDevice, result.StatusCode);
        Assert.AreEqual(0, controller.SetCalls.Count);
    }

    [TestMethod]
    public void CheckOnce_WhenDynamicLimiterWriteFails_ReturnsVolumeWriteFailed()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 70, true, null))
        {
            LimiterSnapshot = new LimiterAudioSnapshot("Speakers", 70, 90, true, true, null),
            SetException = new InvalidOperationException("dynamic write failed")
        };
        var service = new VolumeProtectionService(controller);

        var result = service.CheckOnce(
            true,
            40,
            new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);

        Assert.AreEqual(1, controller.SetCalls.Count);
        Assert.AreEqual(60, controller.SetCalls[0]);
        Assert.AreEqual(70, result.CurrentVolumePercent);
        Assert.IsFalse(result.WasLimited);
        Assert.AreEqual(ProtectionTickStatus.VolumeWriteFailed, result.StatusCode);
        Assert.AreEqual("dynamic write failed", result.StatusDetail);
        Assert.AreEqual(ProtectionMode.DynamicLimiter, result.ProtectionMode);
        Assert.AreEqual(90, result.CurrentPeakPercent);
        Assert.AreEqual(70, result.UserTargetVolumePercent);
    }

    [TestMethod]
    public void CheckOnce_WhenReturningToDynamicLimiterAfterFixedLock_ResetsDynamicState()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 60, true, null))
        {
            LimiterSnapshot = new LimiterAudioSnapshot("Speakers", 70, 90, true, true, null)
        };
        var service = new VolumeProtectionService(controller);
        service.CheckOnce(
            true,
            80,
            new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);

        controller.LimiterSnapshot = new LimiterAudioSnapshot("Speakers", 60, 40, true, true, null);

        service.CheckOnce(
            true,
            60,
            new DateTimeOffset(2026, 5, 28, 10, 0, 1, TimeSpan.Zero),
            protectionMode: ProtectionMode.FixedLock);
        var result = service.CheckOnce(
            true,
            80,
            new DateTimeOffset(2026, 5, 28, 10, 0, 2, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);

        Assert.AreEqual(60, result.CurrentVolumePercent);
        Assert.AreEqual(60, result.UserTargetVolumePercent);
        Assert.IsFalse(result.WasLimited);
        Assert.AreEqual(ProtectionTickStatus.DynamicMonitoring, result.StatusCode);
        CollectionAssert.AreEqual(new[] { 60 }, controller.SetCalls);
    }

    [TestMethod]
    public void CheckOnce_WhenDynamicLimiterLockedDeviceChanges_ResetsDynamicState()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 60, true, null))
        {
            LimiterSnapshot = new LimiterAudioSnapshot("Speakers", 70, 90, true, true, null)
        };
        var service = new VolumeProtectionService(controller);
        service.CheckOnce(
            true,
            80,
            new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero),
            "device-a",
            ProtectionMode.DynamicLimiter);
        controller.LimiterSnapshot = new LimiterAudioSnapshot("Speakers", 60, 40, true, true, null);

        var result = service.CheckOnce(
            true,
            80,
            new DateTimeOffset(2026, 5, 28, 10, 0, 1, TimeSpan.Zero),
            "device-b",
            ProtectionMode.DynamicLimiter);

        Assert.AreEqual(60, result.CurrentVolumePercent);
        Assert.AreEqual(60, result.UserTargetVolumePercent);
        Assert.IsFalse(result.WasLimited);
        Assert.AreEqual(ProtectionTickStatus.DynamicMonitoring, result.StatusCode);
        CollectionAssert.AreEqual(new[] { 60 }, controller.SetCalls);
    }

    [TestMethod]
    public void CheckOnce_WhenDefaultDeviceModeUsesDifferentDeviceIdsWithSameName_ResetsDynamicState()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 60, true, null))
        {
            LimiterSnapshot = new LimiterAudioSnapshot("Speakers", 70, 90, true, true, null, "device-a")
        };
        var service = new VolumeProtectionService(controller);
        service.CheckOnce(
            true,
            80,
            new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);
        controller.LimiterSnapshot = new LimiterAudioSnapshot("Speakers", 60, 40, true, true, null, "device-b");

        var result = service.CheckOnce(
            true,
            80,
            new DateTimeOffset(2026, 5, 28, 10, 0, 1, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);

        Assert.AreEqual(60, result.CurrentVolumePercent);
        Assert.AreEqual(60, result.UserTargetVolumePercent);
        Assert.IsFalse(result.WasLimited);
        Assert.AreEqual(ProtectionTickStatus.DynamicMonitoring, result.StatusCode);
        CollectionAssert.AreEqual(new[] { 60 }, controller.SetCalls);
    }

    [TestMethod]
    public void CheckOnce_WhenDynamicLimiterThresholdChanges_ResetsDynamicState()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 60, true, null))
        {
            LimiterSnapshot = new LimiterAudioSnapshot("Speakers", 70, 90, true, true, null, "device-a")
        };
        var service = new VolumeProtectionService(controller);
        service.CheckOnce(
            true,
            80,
            new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);
        controller.LimiterSnapshot = new LimiterAudioSnapshot("Speakers", 60, 40, true, true, null, "device-a");

        var result = service.CheckOnce(
            true,
            50,
            new DateTimeOffset(2026, 5, 28, 10, 0, 1, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);

        Assert.AreEqual(60, result.CurrentVolumePercent);
        Assert.AreEqual(60, result.UserTargetVolumePercent);
        Assert.IsFalse(result.WasLimited);
        Assert.AreEqual(ProtectionTickStatus.DynamicMonitoring, result.StatusCode);
        CollectionAssert.AreEqual(new[] { 60 }, controller.SetCalls);
    }

    [TestMethod]
    public void CheckOnce_WhenDynamicLimiterWriteFails_DoesNotCommitFailedLimiterState()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 70, true, null))
        {
            LimiterSnapshot = new LimiterAudioSnapshot("Speakers", 70, 90, true, true, null),
            SetException = new InvalidOperationException("dynamic write failed")
        };
        var service = new VolumeProtectionService(controller);
        service.CheckOnce(
            true,
            80,
            new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);

        controller.SetException = null;
        controller.LimiterSnapshot = new LimiterAudioSnapshot("Speakers", 70, 40, true, true, null);
        var firstSafe = service.CheckOnce(
            true,
            80,
            new DateTimeOffset(2026, 5, 28, 10, 0, 1, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);
        service.CheckOnce(
            true,
            80,
            new DateTimeOffset(2026, 5, 28, 10, 0, 2, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);
        var thirdSafe = service.CheckOnce(
            true,
            80,
            new DateTimeOffset(2026, 5, 28, 10, 0, 3, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);

        Assert.AreEqual(70, firstSafe.UserTargetVolumePercent);
        Assert.AreEqual(ProtectionTickStatus.DynamicMonitoring, firstSafe.StatusCode);
        Assert.AreEqual(70, thirdSafe.UserTargetVolumePercent);
        Assert.AreEqual(ProtectionTickStatus.DynamicMonitoring, thirdSafe.StatusCode);
        CollectionAssert.AreEqual(new[] { 60 }, controller.SetCalls);
    }

    [TestMethod]
    public void CheckOnce_WhenDynamicLimiterRestoresVolumeWrite_WasLimitedIsTrue()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("Speakers", 70, true, null))
        {
            LimiterSnapshot = new LimiterAudioSnapshot("Speakers", 70, 90, true, true, null)
        };
        var service = new VolumeProtectionService(controller);
        service.CheckOnce(
            true,
            80,
            new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);

        controller.LimiterSnapshot = new LimiterAudioSnapshot("Speakers", 60, 40, true, true, null);
        service.CheckOnce(
            true,
            80,
            new DateTimeOffset(2026, 5, 28, 10, 0, 1, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);
        service.CheckOnce(
            true,
            80,
            new DateTimeOffset(2026, 5, 28, 10, 0, 2, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);
        var result = service.CheckOnce(
            true,
            80,
            new DateTimeOffset(2026, 5, 28, 10, 0, 3, TimeSpan.Zero),
            protectionMode: ProtectionMode.DynamicLimiter);

        Assert.AreEqual(2, controller.SetCalls.Count);
        Assert.AreEqual(63, controller.SetCalls[1]);
        Assert.AreEqual(63, result.CurrentVolumePercent);
        Assert.IsTrue(result.WasLimited);
        Assert.AreEqual(ProtectionTickStatus.DynamicRestoring, result.StatusCode);
    }

    private sealed class FakeAudioEndpointController(AudioEndpointSnapshot snapshot) : IAudioEndpointController
    {
        public AudioEndpointSnapshot Snapshot { get; set; } = snapshot;

        public LimiterAudioSnapshot LimiterSnapshot { get; set; } = CreateLimiterSnapshot(snapshot);

        public List<int> SetCalls { get; } = [];

        public int BasicSnapshotCalls { get; private set; }

        public int FullSnapshotCalls { get; private set; }

        public int LimiterSnapshotCalls { get; private set; }

        public int ReleaseAudioSamplingCalls { get; private set; }

        public Exception? SetException { get; set; }

        public Exception? SnapshotException { get; init; }

        public IReadOnlyList<AudioOutputDevice> GetActiveOutputDevices()
        {
            return [new("default-device", Snapshot.DeviceName ?? "Speakers", true)];
        }

        public AudioEndpointSnapshot GetBasicSnapshot(string? deviceId = null)
        {
            BasicSnapshotCalls++;

            if (SnapshotException is not null)
            {
                throw SnapshotException;
            }

            return Snapshot;
        }

        public LimiterAudioSnapshot GetLimiterSnapshot(string? deviceId = null)
        {
            LimiterSnapshotCalls++;

            return LimiterSnapshot;
        }

        public MeetingAudioDiagnosticSnapshot GetMeetingAudioDiagnostics(string? lockedDeviceId = null)
        {
            return new MeetingAudioDiagnosticSnapshot(
                Snapshot.DeviceName ?? "Speakers",
                Snapshot.CurrentVolumePercent,
                Snapshot.HasDefaultDevice,
                false,
                null,
                null,
                null,
                null,
                false,
                Snapshot.ErrorMessage,
                Snapshot.DeviceName ?? "Speakers");
        }

        public void SetZoomSessionVolumePercent(int volumePercent)
        {
        }

        public void SetMasterVolumePercent(int volumePercent, string? deviceId = null)
        {
            SetCalls.Add(volumePercent);

            if (SetException is not null)
            {
                throw SetException;
            }
        }

        public void ReleaseAudioSampling()
        {
            ReleaseAudioSamplingCalls++;
        }

        private static LimiterAudioSnapshot CreateLimiterSnapshot(AudioEndpointSnapshot snapshot)
        {
            return new LimiterAudioSnapshot(
                snapshot.DeviceName,
                snapshot.CurrentVolumePercent,
                0,
                snapshot.HasDefaultDevice,
                snapshot.HasDefaultDevice,
                snapshot.ErrorMessage);
        }
    }
}
