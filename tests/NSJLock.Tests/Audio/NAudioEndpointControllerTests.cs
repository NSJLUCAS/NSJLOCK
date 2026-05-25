using System.Runtime.InteropServices;
using NSJLock.Audio;

namespace NSJLock.Tests.Audio;

[TestClass]
public sealed class NAudioEndpointControllerTests
{
    [TestMethod]
    public void GetBasicSnapshot_AfterDispose_ThrowsObjectDisposedException()
    {
        var controller = new NAudioEndpointController();
        controller.Dispose();

        Assert.ThrowsException<ObjectDisposedException>(() => controller.GetBasicSnapshot());
    }

    [TestMethod]
    public void SetMasterVolumePercent_AfterDispose_ThrowsObjectDisposedException()
    {
        var controller = new NAudioEndpointController();
        controller.Dispose();

        Assert.ThrowsException<ObjectDisposedException>(() => controller.SetMasterVolumePercent(50));
    }

    [TestMethod]
    public void GetBasicSnapshot_WhenProviderThrowsComException_ReturnsNoDeviceSnapshot()
    {
        var provider = new FakeEndpointProvider
        {
            GetEndpointException = new COMException("device unavailable")
        };
        var controller = new NAudioEndpointController(provider);

        var snapshot = controller.GetBasicSnapshot();

        Assert.AreEqual("未检测到默认输出设备", snapshot.DeviceName);
        Assert.AreEqual(0, snapshot.CurrentVolumePercent);
        Assert.IsFalse(snapshot.HasDefaultDevice);
        Assert.AreEqual("device unavailable", snapshot.ErrorMessage);
    }

    [TestMethod]
    public void GetBasicSnapshot_RoundsAndClampsVolumePercent()
    {
        var provider = new FakeEndpointProvider
        {
            Endpoint = new FakeEndpoint("Speakers", 1.2f)
        };
        var controller = new NAudioEndpointController(provider);

        var snapshot = controller.GetBasicSnapshot();

        Assert.AreEqual("Speakers", snapshot.DeviceName);
        Assert.AreEqual(100, snapshot.CurrentVolumePercent);
        Assert.IsTrue(snapshot.HasDefaultDevice);
    }

    [TestMethod]
    public void ReleaseAudioSampling_DoesNothingForExactVolumeLock()
    {
        var controller = new NAudioEndpointController(new FakeEndpointProvider());

        controller.ReleaseAudioSampling();

        var snapshot = controller.GetBasicSnapshot();
        Assert.IsTrue(snapshot.HasDefaultDevice);
    }

    [TestMethod]
    public void GetMeetingAudioDiagnostics_WhenZoomSessionExists_ReturnsZoomAppVolume()
    {
        var endpoint = new FakeEndpoint("Speakers", 0.4f)
        {
            AudioSessions =
            [
                new AudioSessionDiagnosticInfo("Music", "Spotify", 11, 80, false),
                new AudioSessionDiagnosticInfo("Zoom Meeting", "Zoom", 22, 25, false)
            ]
        };
        var controller = new NAudioEndpointController(new FakeEndpointProvider { Endpoint = endpoint });

        var snapshot = controller.GetMeetingAudioDiagnostics();

        Assert.IsTrue(snapshot.HasDefaultDevice);
        Assert.AreEqual("Speakers", snapshot.DeviceName);
        Assert.AreEqual(40, snapshot.SystemVolumePercent);
        Assert.IsTrue(snapshot.HasZoomSession);
        Assert.AreEqual(25, snapshot.ZoomVolumePercent);
        Assert.AreEqual(false, snapshot.IsZoomMuted);
    }

    [TestMethod]
    public void GetMeetingAudioDiagnostics_WhenZoomSessionIsMissing_ReturnsSystemVolumeOnly()
    {
        var endpoint = new FakeEndpoint("Speakers", 0.4f)
        {
            AudioSessions =
            [
                new AudioSessionDiagnosticInfo("Music", "Spotify", 11, 80, false)
            ]
        };
        var controller = new NAudioEndpointController(new FakeEndpointProvider { Endpoint = endpoint });

        var snapshot = controller.GetMeetingAudioDiagnostics();

        Assert.IsTrue(snapshot.HasDefaultDevice);
        Assert.AreEqual(40, snapshot.SystemVolumePercent);
        Assert.IsFalse(snapshot.HasZoomSession);
        Assert.IsNull(snapshot.ZoomVolumePercent);
        Assert.IsNull(snapshot.IsZoomMuted);
    }

    [TestMethod]
    public void GetMeetingAudioDiagnostics_WhenZoomUsesAnotherRenderDevice_ReturnsZoomDevice()
    {
        var lockedEndpoint = new FakeEndpoint("Speakers", 0.55f, "locked-device")
        {
            AudioSessions =
            [
                new AudioSessionDiagnosticInfo("Music", "Spotify", 11, 80, false)
            ]
        };
        var zoomEndpoint = new FakeEndpoint("Meeting Headset", 0.67f, "zoom-device")
        {
            AudioSessions =
            [
                new AudioSessionDiagnosticInfo("Zoom Meeting", "Zoom", 22, 25, false)
            ]
        };
        var controller = new NAudioEndpointController(new FakeEndpointProvider
        {
            Endpoint = lockedEndpoint,
            ActiveRenderEndpoints = [lockedEndpoint, zoomEndpoint]
        });

        var snapshot = controller.GetMeetingAudioDiagnostics();

        Assert.IsTrue(snapshot.HasZoomSession);
        Assert.AreEqual("Speakers", snapshot.DeviceName);
        Assert.AreEqual(55, snapshot.SystemVolumePercent);
        Assert.AreEqual("Meeting Headset", snapshot.ZoomDeviceName);
        Assert.AreEqual(67, snapshot.ZoomDeviceVolumePercent);
        Assert.IsFalse(snapshot.IsZoomOnLockedDevice);
    }

    [TestMethod]
    public void SetZoomSessionVolumePercent_WhenZoomSessionExists_UpdatesAllZoomSessions()
    {
        var endpoint = new FakeEndpoint("Speakers", 0.55f)
        {
            AudioSessions =
            [
                new AudioSessionDiagnosticInfo("Zoom Meeting", "Zoom", 22, 45, false),
                new AudioSessionDiagnosticInfo("Zoom Call", "Zoom", 23, 30, false),
                new AudioSessionDiagnosticInfo("Music", "Spotify", 11, 80, false)
            ]
        };
        var controller = new NAudioEndpointController(new FakeEndpointProvider { Endpoint = endpoint });

        controller.SetZoomSessionVolumePercent(100);

        Assert.AreEqual(100, endpoint.AudioSessions[0].VolumePercent);
        Assert.AreEqual(100, endpoint.AudioSessions[1].VolumePercent);
        Assert.AreEqual(80, endpoint.AudioSessions[2].VolumePercent);
    }

    [TestMethod]
    public void SetMasterVolumePercent_ClampsVolumeBeforeWriting()
    {
        var endpoint = new FakeEndpoint("Speakers", 0.5f);
        var provider = new FakeEndpointProvider { Endpoint = endpoint };
        var controller = new NAudioEndpointController(provider);

        controller.SetMasterVolumePercent(150);

        Assert.AreEqual(1f, endpoint.MasterVolumeScalar);
    }

    [TestMethod]
    public void SetMasterVolumePercent_WhenProviderThrowsComException_ThrowsInvalidOperationException()
    {
        var provider = new FakeEndpointProvider
        {
            GetEndpointException = new COMException("device broken")
        };
        var controller = new NAudioEndpointController(provider);

        var exception = Assert.ThrowsException<InvalidOperationException>(
            () => controller.SetMasterVolumePercent(40));

        Assert.AreEqual("device broken", exception.Message);
        Assert.IsInstanceOfType<COMException>(exception.InnerException);
    }

    [TestMethod]
    public void Dispose_DisposesProviderOnce()
    {
        var provider = new FakeEndpointProvider();
        var controller = new NAudioEndpointController(provider);

        controller.Dispose();
        controller.Dispose();

        Assert.AreEqual(1, provider.DisposeCallCount);
    }

    private sealed class FakeEndpointProvider : NAudioEndpointController.ICoreAudioEndpointProvider
    {
        public FakeEndpoint Endpoint { get; init; } = new("Speakers", 0.5f);

        public IReadOnlyList<NAudioEndpointController.ICoreAudioEndpoint>? ActiveRenderEndpoints { get; init; }

        public Exception? GetEndpointException { get; init; }

        public int DisposeCallCount { get; private set; }

        public NAudioEndpointController.ICoreAudioEndpoint GetDefaultRenderEndpoint()
        {
            if (GetEndpointException is not null)
            {
                throw GetEndpointException;
            }

            return Endpoint;
        }

        public NAudioEndpointController.ICoreAudioEndpoint GetRenderEndpoint(string? deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId)
                || string.Equals(deviceId, Endpoint.Id, StringComparison.OrdinalIgnoreCase))
            {
                return GetDefaultRenderEndpoint();
            }

            var endpoint = ActiveRenderEndpoints?.FirstOrDefault(endpoint =>
                string.Equals(endpoint.Id, deviceId, StringComparison.OrdinalIgnoreCase));
            return endpoint ?? throw new InvalidOperationException("device not found");
        }

        public IReadOnlyList<NAudioEndpointController.ICoreAudioEndpoint> GetActiveRenderEndpoints()
        {
            return ActiveRenderEndpoints ?? [Endpoint];
        }

        public void Dispose()
        {
            DisposeCallCount++;
        }
    }

    private sealed class FakeEndpoint(string friendlyName, float masterVolumeScalar, string id = "default-device")
        : NAudioEndpointController.ICoreAudioEndpoint
    {
        public string Id { get; } = id;

        public string FriendlyName { get; } = friendlyName;

        public float MasterVolumeScalar { get; set; } = masterVolumeScalar;

        public IReadOnlyList<AudioSessionDiagnosticInfo> AudioSessions { get; set; } = [];

        public IReadOnlyList<AudioSessionDiagnosticInfo> GetAudioSessions()
        {
            return AudioSessions;
        }

        public void SetZoomSessionVolume(float volumeScalar)
        {
            AudioSessions = AudioSessions
                .Select(session => IsZoomSession(session)
                    ? session with { VolumePercent = Math.Clamp((int)Math.Round(volumeScalar * 100), 0, 100) }
                    : session)
                .ToArray();
        }

        public void Dispose()
        {
        }

        private static bool IsZoomSession(AudioSessionDiagnosticInfo session)
        {
            return ContainsZoom(session.ProcessName)
                || ContainsZoom(session.DisplayName);
        }

        private static bool ContainsZoom(string? value)
        {
            return value?.Contains("zoom", StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
