using System.Globalization;

namespace NSJLock.Audio;

public sealed class VolumeProtectionService
{
    private const string DefaultOutputDeviceText = "默认输出设备";
    private const string NoDefaultDeviceText = "未检测到默认输出设备";
    private readonly IAudioEndpointController audioEndpointController;
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
        string? lockedDeviceId = null)
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

    private static int ClampPercent(int value)
    {
        return Math.Clamp(value, 0, 100);
    }
}
