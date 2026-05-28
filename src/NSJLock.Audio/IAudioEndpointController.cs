namespace NSJLock.Audio;

public interface IAudioEndpointController
{
    IReadOnlyList<AudioOutputDevice> GetActiveOutputDevices();

    AudioEndpointSnapshot GetBasicSnapshot(string? deviceId = null);

    LimiterAudioSnapshot GetLimiterSnapshot(string? deviceId = null);

    MeetingAudioDiagnosticSnapshot GetMeetingAudioDiagnostics(string? lockedDeviceId = null);

    void SetZoomSessionVolumePercent(int volumePercent);

    void ReleaseAudioSampling();

    void SetMasterVolumePercent(int volumePercent, string? deviceId = null);
}
