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

    private sealed class FakeAudioEndpointController(AudioEndpointSnapshot snapshot) : IAudioEndpointController
    {
        public AudioEndpointSnapshot Snapshot { get; set; } = snapshot;

        public List<int> SetCalls { get; } = [];

        public int BasicSnapshotCalls { get; private set; }

        public int FullSnapshotCalls { get; private set; }

        public int ReleaseAudioSamplingCalls { get; private set; }

        public Exception? SetException { get; init; }

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
    }
}
