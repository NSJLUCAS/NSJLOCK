using NSJLock.Audio;

namespace NSJLock.Tests.Audio;

[TestClass]
public sealed class VolumeLockServiceTests
{
    [TestMethod]
    public void CheckOnce_WhenVolumeDiffersFromTarget_WritesTargetVolume()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot("扬声器", 100, true, null));
        var service = new VolumeProtectionService(controller);

        var result = service.CheckOnce(true, 40, new DateTimeOffset(2026, 5, 23, 10, 0, 0, TimeSpan.Zero));

        Assert.IsTrue(result.WasLimited);
        Assert.AreEqual(1, controller.SetCalls.Count);
        Assert.AreEqual(40, controller.SetCalls[0]);
    }

    [TestMethod]
    public void CheckOnce_WhenNoDefaultDevice_DoesNotWrite()
    {
        var controller = new FakeAudioEndpointController(new AudioEndpointSnapshot(null, 0, false, null));
        var service = new VolumeProtectionService(controller);

        var result = service.CheckOnce(true, 40, new DateTimeOffset(2026, 5, 23, 10, 0, 0, TimeSpan.Zero));

        Assert.IsFalse(result.WasLimited);
        Assert.AreEqual(0, controller.SetCalls.Count);
    }

    private sealed class FakeAudioEndpointController(AudioEndpointSnapshot snapshot) : IAudioEndpointController
    {
        public AudioEndpointSnapshot Snapshot { get; set; } = snapshot;

        public LimiterAudioSnapshot LimiterSnapshot { get; set; } = CreateLimiterSnapshot(snapshot);

        public List<int> SetCalls { get; } = [];

        public IReadOnlyList<AudioOutputDevice> GetActiveOutputDevices()
        {
            return [new("default-device", Snapshot.DeviceName ?? "Speakers", true)];
        }

        public AudioEndpointSnapshot GetBasicSnapshot(string? deviceId = null)
        {
            return Snapshot;
        }

        public LimiterAudioSnapshot GetLimiterSnapshot(string? deviceId = null)
        {
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

        public void ReleaseAudioSampling()
        {
        }

        public void SetMasterVolumePercent(int volumePercent, string? deviceId = null)
        {
            SetCalls.Add(volumePercent);
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
