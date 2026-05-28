using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace NSJLock.Audio;

public sealed class NAudioEndpointController : IAudioEndpointController, IDisposable
{
    private const string NoDefaultDeviceText = "未检测到默认输出设备";

    private readonly object syncRoot = new();
    private readonly ICoreAudioEndpointProvider endpointProvider;
    private bool isDisposed;

    public NAudioEndpointController()
        : this(new NAudioEndpointProvider())
    {
    }

    internal NAudioEndpointController(ICoreAudioEndpointProvider endpointProvider)
    {
        this.endpointProvider = endpointProvider
            ?? throw new ArgumentNullException(nameof(endpointProvider));
    }

    public IReadOnlyList<AudioOutputDevice> GetActiveOutputDevices()
    {
        lock (syncRoot)
        {
            ThrowIfDisposed();

            try
            {
                using var defaultEndpoint = endpointProvider.GetDefaultRenderEndpoint();
                var devices = new List<AudioOutputDevice>();
                foreach (var endpoint in endpointProvider.GetActiveRenderEndpoints())
                {
                    using (endpoint)
                    {
                        devices.Add(new AudioOutputDevice(
                            endpoint.Id,
                            endpoint.FriendlyName,
                            string.Equals(endpoint.Id, defaultEndpoint.Id, StringComparison.OrdinalIgnoreCase)));
                    }
                }

                return devices;
            }
            catch (Exception exception) when (IsExpectedEndpointException(exception))
            {
                return [];
            }
        }
    }

    public AudioEndpointSnapshot GetBasicSnapshot(string? deviceId = null)
    {
        lock (syncRoot)
        {
            ThrowIfDisposed();

            try
            {
                using var endpoint = endpointProvider.GetRenderEndpoint(deviceId);

                return new AudioEndpointSnapshot(
                    endpoint.FriendlyName,
                    PercentFromScalar(endpoint.MasterVolumeScalar),
                    true,
                    null);
            }
            catch (Exception exception) when (IsExpectedEndpointException(exception))
            {
                return new AudioEndpointSnapshot(
                    NoDefaultDeviceText,
                    0,
                    false,
                    exception.Message);
            }
        }
    }

    public LimiterAudioSnapshot GetLimiterSnapshot(string? deviceId = null)
    {
        lock (syncRoot)
        {
            ThrowIfDisposed();

            try
            {
                using var endpoint = endpointProvider.GetRenderEndpoint(deviceId);
                var endpointId = endpoint.Id;
                var deviceName = endpoint.FriendlyName;
                var currentVolumePercent = PercentFromScalar(endpoint.MasterVolumeScalar);

                try
                {
                    return new LimiterAudioSnapshot(
                        deviceName,
                        currentVolumePercent,
                        PercentFromScalar(endpoint.MasterPeakValue),
                        true,
                        true,
                        null,
                        endpointId);
                }
                catch (Exception exception) when (IsExpectedEndpointException(exception))
                {
                    return new LimiterAudioSnapshot(
                        deviceName,
                        currentVolumePercent,
                        0,
                        true,
                        false,
                        exception.Message,
                        endpointId);
                }
            }
            catch (Exception exception) when (IsExpectedEndpointException(exception))
            {
                return new LimiterAudioSnapshot(
                    NoDefaultDeviceText,
                    0,
                    0,
                    false,
                    false,
                    exception.Message);
            }
        }
    }

    public MeetingAudioDiagnosticSnapshot GetMeetingAudioDiagnostics(string? lockedDeviceId = null)
    {
        lock (syncRoot)
        {
            ThrowIfDisposed();

            try
            {
                using var lockedEndpoint = endpointProvider.GetRenderEndpoint(lockedDeviceId);
                using var defaultEndpoint = endpointProvider.GetDefaultRenderEndpoint();
                var systemDefaultDeviceName = defaultEndpoint.FriendlyName;
                var systemVolumePercent = PercentFromScalar(lockedEndpoint.MasterVolumeScalar);
                var zoomSession = FindZoomSession(lockedEndpoint);
                if (zoomSession is not null)
                {
                    return new MeetingAudioDiagnosticSnapshot(
                        lockedEndpoint.FriendlyName,
                        systemVolumePercent,
                        true,
                        true,
                        zoomSession.VolumePercent,
                        zoomSession.IsMuted,
                        lockedEndpoint.FriendlyName,
                        systemVolumePercent,
                        true,
                        null,
                        systemDefaultDeviceName);
                }

                foreach (var endpoint in endpointProvider.GetActiveRenderEndpoints())
                {
                    using (endpoint)
                    {
                        if (string.Equals(endpoint.Id, lockedEndpoint.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        zoomSession = FindZoomSession(endpoint);
                        if (zoomSession is null)
                        {
                            continue;
                        }

                        return new MeetingAudioDiagnosticSnapshot(
                            lockedEndpoint.FriendlyName,
                            systemVolumePercent,
                            true,
                            true,
                            zoomSession.VolumePercent,
                            zoomSession.IsMuted,
                            endpoint.FriendlyName,
                            PercentFromScalar(endpoint.MasterVolumeScalar),
                            false,
                            null,
                            systemDefaultDeviceName);
                    }
                }

                return new MeetingAudioDiagnosticSnapshot(
                    lockedEndpoint.FriendlyName,
                    systemVolumePercent,
                    true,
                    false,
                    null,
                    null,
                    null,
                    null,
                    false,
                    null,
                    systemDefaultDeviceName);
            }
            catch (Exception exception) when (IsExpectedEndpointException(exception))
            {
                return new MeetingAudioDiagnosticSnapshot(
                    NoDefaultDeviceText,
                    0,
                    false,
                    false,
                    null,
                    null,
                    null,
                    null,
                    false,
                    exception.Message);
            }
        }
    }

    public void ReleaseAudioSampling()
    {
    }

    public void SetZoomSessionVolumePercent(int volumePercent)
    {
        lock (syncRoot)
        {
            ThrowIfDisposed();

            try
            {
                var targetScalar = Math.Clamp(volumePercent, 0, 100) / 100f;
                foreach (var endpoint in endpointProvider.GetActiveRenderEndpoints())
                {
                    using (endpoint)
                    {
                        endpoint.SetZoomSessionVolume(targetScalar);
                    }
                }
            }
            catch (Exception exception) when (IsExpectedEndpointException(exception))
            {
                throw new InvalidOperationException(exception.Message, exception);
            }
        }
    }

    public void SetMasterVolumePercent(int volumePercent, string? deviceId = null)
    {
        lock (syncRoot)
        {
            ThrowIfDisposed();

            try
            {
                using var endpoint = endpointProvider.GetRenderEndpoint(deviceId);
                endpoint.MasterVolumeScalar = Math.Clamp(volumePercent, 0, 100) / 100f;
            }
            catch (Exception exception) when (IsExpectedEndpointException(exception))
            {
                throw new InvalidOperationException(exception.Message, exception);
            }
        }
    }

    public void Dispose()
    {
        lock (syncRoot)
        {
            if (isDisposed)
            {
                return;
            }

            endpointProvider.Dispose();
            isDisposed = true;
        }
    }

    private static bool IsExpectedEndpointException(Exception exception)
    {
        return exception is COMException or InvalidOperationException;
    }

    private static int PercentFromScalar(float value)
    {
        var volumePercent = (int)Math.Round(value * 100);
        return Math.Clamp(volumePercent, 0, 100);
    }

    private static bool IsZoomSession(AudioSessionDiagnosticInfo session)
    {
        return ContainsZoom(session.ProcessName)
            || ContainsZoom(session.DisplayName);
    }

    private static AudioSessionDiagnosticInfo? FindZoomSession(ICoreAudioEndpoint endpoint)
    {
        return endpoint.GetAudioSessions()
            .FirstOrDefault(IsZoomSession);
    }

    private static bool ContainsZoom(string? value)
    {
        return value?.Contains("zoom", StringComparison.OrdinalIgnoreCase) == true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
    }

    internal interface ICoreAudioEndpointProvider : IDisposable
    {
        ICoreAudioEndpoint GetDefaultRenderEndpoint();

        ICoreAudioEndpoint GetRenderEndpoint(string? deviceId);

        IReadOnlyList<ICoreAudioEndpoint> GetActiveRenderEndpoints();
    }

    internal interface ICoreAudioEndpoint : IDisposable
    {
        string Id { get; }

        string FriendlyName { get; }

        float MasterVolumeScalar { get; set; }

        float MasterPeakValue { get; }

        IReadOnlyList<AudioSessionDiagnosticInfo> GetAudioSessions();

        void SetZoomSessionVolume(float volumeScalar);
    }

    private sealed class NAudioEndpointProvider : ICoreAudioEndpointProvider
    {
        private readonly MMDeviceEnumerator deviceEnumerator = new();

        public ICoreAudioEndpoint GetDefaultRenderEndpoint()
        {
            return new NAudioEndpoint(
                deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia));
        }

        public ICoreAudioEndpoint GetRenderEndpoint(string? deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return GetDefaultRenderEndpoint();
            }

            return new NAudioEndpoint(deviceEnumerator.GetDevice(deviceId));
        }

        public IReadOnlyList<ICoreAudioEndpoint> GetActiveRenderEndpoints()
        {
            var devices = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            var endpoints = new List<ICoreAudioEndpoint>();
            for (var index = 0; index < devices.Count; index++)
            {
                endpoints.Add(new NAudioEndpoint(devices[index]));
            }

            return endpoints;
        }

        public void Dispose()
        {
            deviceEnumerator.Dispose();
        }
    }

    private sealed class NAudioEndpoint(MMDevice device) : ICoreAudioEndpoint
    {
        public string Id => device.ID;

        public string FriendlyName => device.FriendlyName;

        public float MasterVolumeScalar
        {
            get => device.AudioEndpointVolume.MasterVolumeLevelScalar;
            set => device.AudioEndpointVolume.MasterVolumeLevelScalar = value;
        }

        public float MasterPeakValue => device.AudioMeterInformation.MasterPeakValue;

        public IReadOnlyList<AudioSessionDiagnosticInfo> GetAudioSessions()
        {
            var sessions = device.AudioSessionManager.Sessions;
            var result = new List<AudioSessionDiagnosticInfo>();

            for (var index = 0; index < sessions.Count; index++)
            {
                try
                {
                    using var session = sessions[index];
                    using var simpleVolume = session.SimpleAudioVolume;
                    var processId = (int)session.GetProcessID;

                    result.Add(new AudioSessionDiagnosticInfo(
                        session.DisplayName ?? string.Empty,
                        TryGetProcessName(processId),
                        processId,
                        PercentFromScalar(simpleVolume.Volume),
                        simpleVolume.Mute));
                }
                catch (Exception exception) when (IsExpectedEndpointException(exception))
                {
                }
            }

            return result;
        }

        public void SetZoomSessionVolume(float volumeScalar)
        {
            var sessions = device.AudioSessionManager.Sessions;
            for (var index = 0; index < sessions.Count; index++)
            {
                try
                {
                    using var session = sessions[index];
                    if (!IsZoomSession(session))
                    {
                        continue;
                    }

                    using var simpleVolume = session.SimpleAudioVolume;
                    simpleVolume.Volume = Math.Clamp(volumeScalar, 0f, 1f);
                }
                catch (Exception exception) when (IsExpectedEndpointException(exception))
                {
                }
            }
        }

        public void Dispose()
        {
            device.Dispose();
        }

        private static bool IsZoomSession(AudioSessionControl session)
        {
            var processId = (int)session.GetProcessID;
            return ContainsZoom(session.DisplayName)
                || ContainsZoom(TryGetProcessName(processId))
                || ContainsZoom(TryGetSessionIdentifier(session));
        }

        private static string? TryGetSessionIdentifier(AudioSessionControl session)
        {
            try
            {
                return session.GetSessionIdentifier;
            }
            catch (Exception exception) when (IsExpectedEndpointException(exception))
            {
                return null;
            }
        }

        private static string? TryGetProcessName(int processId)
        {
            if (processId <= 0)
            {
                return null;
            }

            try
            {
                using var process = Process.GetProcessById(processId);
                return process.ProcessName;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                return null;
            }
        }
    }
}
