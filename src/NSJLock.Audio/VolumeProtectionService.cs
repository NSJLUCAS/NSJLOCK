using System.Globalization;

namespace NSJLock.Audio;

public sealed class VolumeProtectionService
{
    private const string DefaultOutputDeviceText = "默认输出设备";
    private const string NoDefaultDeviceText = "未检测到默认输出设备";
    private readonly IAudioEndpointController audioEndpointController;
    private DynamicLimiterState? dynamicLimiterState;
    private string? dynamicLimiterStateKey;
    private DateTimeOffset? lastLimitedAt;

    public VolumeProtectionService(IAudioEndpointController audioEndpointController)
    {
        this.audioEndpointController = audioEndpointController
            ?? throw new ArgumentNullException(nameof(audioEndpointController));
    }

    public ProtectionTickResult CheckOnce(
        bool isProtectionEnabled,
        int maxVolumePercent,
        DateTimeOffset now,
        string? lockedDeviceId = null,
        ProtectionMode protectionMode = ProtectionMode.FixedLock)
    {
        if (protectionMode == ProtectionMode.DynamicLimiter)
        {
            return CheckDynamicLimiter(isProtectionEnabled, maxVolumePercent, now, lockedDeviceId);
        }

        ResetDynamicLimiterState();
        return CheckFixedLock(isProtectionEnabled, maxVolumePercent, now, lockedDeviceId);
    }

    public IReadOnlyList<AudioOutputDevice> GetActiveOutputDevices()
    {
        return audioEndpointController.GetActiveOutputDevices();
    }

    public MeetingAudioDiagnosticSnapshot GetMeetingAudioDiagnostics(string? lockedDeviceId = null)
    {
        return audioEndpointController.GetMeetingAudioDiagnostics(lockedDeviceId);
    }

    public void SetZoomSessionVolumePercent(int volumePercent)
    {
        audioEndpointController.SetZoomSessionVolumePercent(ClampPercent(volumePercent));
    }

    private ProtectionTickResult CheckFixedLock(
        bool isProtectionEnabled,
        int maxVolumePercent,
        DateTimeOffset now,
        string? lockedDeviceId)
    {
        var targetVolumePercent = ClampPercent(maxVolumePercent);
        AudioEndpointSnapshot snapshot;
        try
        {
            audioEndpointController.ReleaseAudioSampling();
            snapshot = audioEndpointController.GetBasicSnapshot(lockedDeviceId);
        }
        catch (Exception exception)
        {
            return new ProtectionTickResult(
                DefaultOutputDeviceText,
                0,
                false,
                false,
                lastLimitedAt,
                ProtectionTickStatus.AudioReadFailed,
                exception.Message);
        }

        if (!snapshot.HasDefaultDevice)
        {
            return new ProtectionTickResult(
                NoDefaultDeviceText,
                snapshot.CurrentVolumePercent,
                false,
                false,
                lastLimitedAt,
                ProtectionTickStatus.NoDefaultDevice,
                null);
        }

        var deviceName = string.IsNullOrWhiteSpace(snapshot.DeviceName)
            ? DefaultOutputDeviceText
            : snapshot.DeviceName;

        if (!isProtectionEnabled)
        {
            return new ProtectionTickResult(
                deviceName,
                snapshot.CurrentVolumePercent,
                true,
                false,
                lastLimitedAt,
                ProtectionTickStatus.ProtectionPaused,
                null);
        }

        if (snapshot.CurrentVolumePercent == targetVolumePercent)
        {
            return new ProtectionTickResult(
                deviceName,
                snapshot.CurrentVolumePercent,
                true,
                false,
                lastLimitedAt,
                ProtectionTickStatus.Protecting,
                null);
        }

        try
        {
            audioEndpointController.SetMasterVolumePercent(targetVolumePercent, lockedDeviceId);
            lastLimitedAt = now;
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException)
        {
            return new ProtectionTickResult(
                deviceName,
                snapshot.CurrentVolumePercent,
                true,
                false,
                lastLimitedAt,
                ProtectionTickStatus.VolumeWriteFailed,
                exception.Message);
        }

        return new ProtectionTickResult(
            deviceName,
            targetVolumePercent,
            true,
            true,
            lastLimitedAt,
            ProtectionTickStatus.VolumeAdjusted,
            now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
    }

    private ProtectionTickResult CheckDynamicLimiter(
        bool isProtectionEnabled,
        int maxVolumePercent,
        DateTimeOffset now,
        string? lockedDeviceId)
    {
        var peakThresholdPercent = ClampPercent(maxVolumePercent);
        var limiterEngine = CreateDynamicLimiterEngine(peakThresholdPercent);
        LimiterAudioSnapshot snapshot;
        try
        {
            snapshot = audioEndpointController.GetLimiterSnapshot(lockedDeviceId);
        }
        catch (Exception exception)
        {
            return new ProtectionTickResult(
                DefaultOutputDeviceText,
                0,
                false,
                false,
                lastLimitedAt,
                ProtectionTickStatus.AudioReadFailed,
                exception.Message,
                ProtectionMode.DynamicLimiter);
        }

        var deviceName = GetDeviceName(snapshot.DeviceName, snapshot.HasDefaultDevice);
        if (!snapshot.HasDefaultDevice)
        {
            ResetDynamicLimiterState();
            return new ProtectionTickResult(
                deviceName,
                snapshot.CurrentVolumePercent,
                false,
                false,
                lastLimitedAt,
                ProtectionTickStatus.NoDefaultDevice,
                snapshot.ErrorMessage,
                ProtectionMode.DynamicLimiter,
                snapshot.PeakPercent,
                DynamicLimiterState.Create(snapshot.CurrentVolumePercent).UserTargetVolumePercent);
        }

        var stateKey = CreateDynamicLimiterStateKey(lockedDeviceId, snapshot, peakThresholdPercent);
        var state = GetDynamicLimiterState(stateKey, snapshot.CurrentVolumePercent);
        var limiterResult = limiterEngine.Tick(new DynamicLimiterTickInput(
            isProtectionEnabled,
            snapshot.HasDefaultDevice,
            snapshot.IsPeakAvailable,
            snapshot.CurrentVolumePercent,
            snapshot.PeakPercent,
            state));
        var status = MapDynamicStatus(limiterResult.Status);

        if (limiterResult.Status == DynamicLimiterStatus.LevelReadFailed)
        {
            CommitDynamicLimiterState(stateKey, limiterResult.State);

            return new ProtectionTickResult(
                deviceName,
                snapshot.CurrentVolumePercent,
                true,
                false,
                lastLimitedAt,
                status,
                snapshot.ErrorMessage,
                ProtectionMode.DynamicLimiter,
                snapshot.PeakPercent,
                limiterResult.State.UserTargetVolumePercent);
        }

        if (limiterResult.VolumeToWritePercent is null)
        {
            CommitDynamicLimiterState(stateKey, limiterResult.State);

            return new ProtectionTickResult(
                deviceName,
                snapshot.CurrentVolumePercent,
                true,
                false,
                lastLimitedAt,
                status,
                null,
                ProtectionMode.DynamicLimiter,
                snapshot.PeakPercent,
                limiterResult.State.UserTargetVolumePercent);
        }

        try
        {
            audioEndpointController.SetMasterVolumePercent(limiterResult.VolumeToWritePercent.Value, lockedDeviceId);
            lastLimitedAt = now;
            CommitDynamicLimiterState(stateKey, limiterResult.State);
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException)
        {
            return new ProtectionTickResult(
                deviceName,
                snapshot.CurrentVolumePercent,
                true,
                false,
                lastLimitedAt,
                ProtectionTickStatus.VolumeWriteFailed,
                exception.Message,
                ProtectionMode.DynamicLimiter,
                snapshot.PeakPercent,
                limiterResult.State.UserTargetVolumePercent);
        }

        return new ProtectionTickResult(
            deviceName,
            limiterResult.VolumeToWritePercent.Value,
            true,
            true,
            lastLimitedAt,
            status,
            now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            ProtectionMode.DynamicLimiter,
            snapshot.PeakPercent,
            limiterResult.State.UserTargetVolumePercent);
    }

    private static int ClampPercent(int value)
    {
        return Math.Clamp(value, 0, 100);
    }

    private static string GetDeviceName(string? deviceName, bool hasDefaultDevice)
    {
        if (!hasDefaultDevice)
        {
            return NoDefaultDeviceText;
        }

        return string.IsNullOrWhiteSpace(deviceName)
            ? DefaultOutputDeviceText
            : deviceName;
    }

    private static ProtectionTickStatus MapDynamicStatus(DynamicLimiterStatus status)
    {
        return status switch
        {
            DynamicLimiterStatus.Paused => ProtectionTickStatus.ProtectionPaused,
            DynamicLimiterStatus.NoDevice => ProtectionTickStatus.NoDefaultDevice,
            DynamicLimiterStatus.LevelReadFailed => ProtectionTickStatus.LevelReadFailed,
            DynamicLimiterStatus.Limiting => ProtectionTickStatus.DynamicLimited,
            DynamicLimiterStatus.Restoring => ProtectionTickStatus.DynamicRestoring,
            DynamicLimiterStatus.Restored => ProtectionTickStatus.DynamicRestored,
            _ => ProtectionTickStatus.DynamicMonitoring
        };
    }

    private DynamicLimiterState GetDynamicLimiterState(string stateKey, int currentVolumePercent)
    {
        if (dynamicLimiterState is null ||
            !string.Equals(dynamicLimiterStateKey, stateKey, StringComparison.Ordinal))
        {
            ResetDynamicLimiterState();
            return DynamicLimiterState.Create(currentVolumePercent);
        }

        return dynamicLimiterState;
    }

    private void CommitDynamicLimiterState(string stateKey, DynamicLimiterState state)
    {
        dynamicLimiterStateKey = stateKey;
        dynamicLimiterState = state;
    }

    private void ResetDynamicLimiterState()
    {
        dynamicLimiterStateKey = null;
        dynamicLimiterState = null;
    }

    private static string CreateDynamicLimiterStateKey(
        string? lockedDeviceId,
        LimiterAudioSnapshot snapshot,
        int peakThresholdPercent)
    {
        var keyPrefix = $"peak:{ClampPercent(peakThresholdPercent)}:";
        if (!string.IsNullOrWhiteSpace(lockedDeviceId))
        {
            return keyPrefix + lockedDeviceId;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.DeviceId))
        {
            return keyPrefix + snapshot.DeviceId;
        }

        return keyPrefix + (snapshot.DeviceName ?? string.Empty);
    }

    private static DynamicLimiterEngine CreateDynamicLimiterEngine(int peakThresholdPercent)
    {
        var options = DynamicLimiterOptions.Defaults with
        {
            PeakThresholdPercent = ClampPercent(peakThresholdPercent)
        };

        return new DynamicLimiterEngine(options);
    }
}
