using NSJLock.Audio;
using NSJLock.Config;
using ConfigProtectionMode = NSJLock.Config.ProtectionMode;

namespace NSJLock.App.ViewModels;

internal static class MainWindowText
{
    public static string SelectedLanguageLabel(AppLanguage language)
    {
        return language == AppLanguage.English ? "EN" : "中";
    }

    public static string Subtitle(AppLanguage language)
    {
        return language == AppLanguage.English ? "System sound protection" : "系统声音保护";
    }

    public static string OpenMiniWindow(AppLanguage language)
    {
        return language == AppLanguage.English ? "Mini window" : "迷你窗口";
    }

    public static string ReturnToMainWindow(AppLanguage language)
    {
        return language == AppLanguage.English ? "Full window" : "回到主窗口";
    }

    public static string Theme(AppLanguage language, AppThemeMode themeMode)
    {
        return (language, themeMode) switch
        {
            (AppLanguage.English, AppThemeMode.System) => "System",
            (AppLanguage.English, AppThemeMode.Light) => "Light",
            (AppLanguage.English, AppThemeMode.Dark) => "Dark",
            (_, AppThemeMode.System) => "跟随系统",
            (_, AppThemeMode.Light) => "浅色",
            _ => "深色"
        };
    }

    public static string ProtectionButton(AppLanguage language, bool enabled)
    {
        return language == AppLanguage.English
            ? enabled ? "Turn protection off" : "Turn protection on"
            : enabled ? "关闭保护" : "开启保护";
    }

    public static string ProtectionState(AppLanguage language, bool enabled)
    {
        return language == AppLanguage.English
            ? enabled ? "Protected" : "Paused"
            : enabled ? "保护中" : "暂停中";
    }

    public static string SoundLockTitle(AppLanguage language)
    {
        return language == AppLanguage.English ? "Sound lock" : "声音锁";
    }

    public static string SoundLockStrength(AppLanguage language, ConfigProtectionMode mode)
    {
        if (mode == ConfigProtectionMode.DynamicLimiter)
        {
            return language == AppLanguage.English ? "Peak limit" : "峰值上限";
        }

        return language == AppLanguage.English ? "Volume lock" : "锁定音量";
    }

    public static string SoundLockDescription(AppLanguage language, int percent, ConfigProtectionMode mode)
    {
        if (mode == ConfigProtectionMode.DynamicLimiter)
        {
            return language == AppLanguage.English
                ? $"You can adjust system volume. NSJ Lock temporarily lowers it when output peak reaches {percent}%."
                : $"你可以继续调系统音量。输出峰值达到 {percent}% 时，NSJ Lock 才会临时压低。";
        }

        return language == AppLanguage.English
            ? $"System volume will be pulled back to {percent}%, whether it goes above or below the target."
            : $"系统音量会被拉回 {percent}%，高了会降下去，低了会拉上来。";
    }

    public static string CurrentVolume(AppLanguage language)
    {
        return language == AppLanguage.English ? "Current volume" : "当前声音";
    }

    public static string SystemMasterVolume(AppLanguage language)
    {
        return language == AppLanguage.English ? "Master volume" : "系统主音量";
    }

    public static string LockedTarget(AppLanguage language, ConfigProtectionMode mode)
    {
        if (mode == ConfigProtectionMode.DynamicLimiter)
        {
            return language == AppLanguage.English ? "Protection target" : "保护目标";
        }

        return language == AppLanguage.English ? "Locked target" : "锁定目标";
    }

    public static string FixedLockMode(AppLanguage language)
    {
        return language == AppLanguage.English ? "Fixed lock" : "固定锁定";
    }

    public static string DynamicLimiterMode(AppLanguage language)
    {
        return language == AppLanguage.English ? "Dynamic adjust" : "动态调整";
    }

    public static string OutputPeak(AppLanguage language)
    {
        return language == AppLanguage.English ? "Output peak" : "输出峰值";
    }

    public static string Target(AppLanguage language)
    {
        return language == AppLanguage.English ? "Target" : "目标";
    }

    public static string SystemOutput(AppLanguage language)
    {
        return language == AppLanguage.English ? "System output" : "系统输出";
    }

    public static string SystemDefaultSuffix(AppLanguage language)
    {
        return language == AppLanguage.English ? " (system default)" : "（系统默认）";
    }

    public static string Zoom(AppLanguage language)
    {
        return "Zoom";
    }

    public static string FollowSystemDefaultOutput(AppLanguage language)
    {
        return language == AppLanguage.English ? "Follow system default output" : "跟随系统默认输出";
    }

    public static string AdjustLockValue(AppLanguage language, ConfigProtectionMode mode)
    {
        if (mode == ConfigProtectionMode.DynamicLimiter)
        {
            return language == AppLanguage.English ? "Adjust peak limit" : "调节峰值上限";
        }

        return language == AppLanguage.English ? "Adjust lock value" : "调节锁定值";
    }

    public static string AdjustLockDescription(AppLanguage language, ConfigProtectionMode mode)
    {
        if (mode == ConfigProtectionMode.DynamicLimiter)
        {
            return language == AppLanguage.English
                ? "Drag the slider to set the output peak that triggers temporary lowering."
                : "拖动滑杆设置触发临时压低的输出峰值。";
        }

        return language == AppLanguage.English
            ? "Drag the slider to set the system volume target."
            : "拖动滑杆设置系统主音量会被拉回的位置。";
    }

    public static string Quieter(AppLanguage language)
    {
        return language == AppLanguage.English ? "Quieter" : "更安静";
    }

    public static string Louder(AppLanguage language)
    {
        return language == AppLanguage.English ? "Louder" : "更响亮";
    }

    public static string LoadingDevice(AppLanguage language)
    {
        return language == AppLanguage.English ? "Checking..." : "正在检测...";
    }

    public static string StartingProtection(AppLanguage language)
    {
        return language == AppLanguage.English ? "Starting protection..." : "正在启动保护...";
    }

    public static string WaitingForZoom(AppLanguage language)
    {
        return language == AppLanguage.English
            ? "Meeting diagnostics: waiting to detect Zoom audio sessions"
            : "会议诊断：等待检测 Zoom 音频会话";
    }

    public static string LockedTargetChecking(AppLanguage language)
    {
        return language == AppLanguage.English ? "Locked target: checking..." : "锁定目标：正在检测...";
    }

    public static string LockedTargetSwitching(AppLanguage language)
    {
        return language == AppLanguage.English ? "Locked target: switching..." : "锁定目标：正在切换...";
    }

    public static string SystemDefaultChecking(AppLanguage language)
    {
        return language == AppLanguage.English ? "System default output: checking..." : "系统默认输出：正在检测...";
    }

    public static string ZoomWaiting(AppLanguage language)
    {
        return language == AppLanguage.English ? "Zoom output: waiting to detect audio sessions" : "Zoom 输出：等待检测音频会话";
    }

    public static string ZoomRefreshWaiting(AppLanguage language)
    {
        return language == AppLanguage.English ? "Zoom output: waiting to refresh" : "Zoom 输出：等待刷新";
    }

    public static string MissingPreviousOutputDevice(AppLanguage language)
    {
        return language == AppLanguage.English
            ? "Tip: the previously selected output device is no longer available, so NSJ Lock now follows the system default output."
            : "提示：之前选择的输出设备已不可用，已改为跟随系统默认输出。";
    }

    public static string SaveFailed(AppLanguage language, string message)
    {
        return language == AppLanguage.English ? $"Failed to save settings: {message}" : $"保存设置失败：{message}";
    }

    public static string AudioReadFailed(AppLanguage language, string message)
    {
        return language == AppLanguage.English ? $"Failed to read audio: {message}" : $"读取音频失败：{message}";
    }

    public static string ProtectionStatus(AppLanguage language, ProtectionTickResult result)
    {
        return result.StatusCode switch
        {
            ProtectionTickStatus.AudioReadFailed => AudioReadFailed(language, result.StatusDetail ?? string.Empty),
            ProtectionTickStatus.NoDefaultDevice => language == AppLanguage.English ? "No default output device detected" : "未检测到默认输出设备",
            ProtectionTickStatus.ProtectionPaused => language == AppLanguage.English ? "Protection is paused" : "保护已暂停",
            ProtectionTickStatus.LevelReadFailed => language == AppLanguage.English ? "Failed to read output level" : "无法读取输出峰值",
            ProtectionTickStatus.DynamicMonitoring => language == AppLanguage.English ? "Dynamic protection active" : "动态保护中",
            ProtectionTickStatus.DynamicLimited => language == AppLanguage.English ? "High peak limited" : "检测到高峰值，已临时压低",
            ProtectionTickStatus.DynamicRestoring => language == AppLanguage.English ? "Restoring user volume" : "正在恢复到用户音量",
            ProtectionTickStatus.DynamicRestored => language == AppLanguage.English ? "User volume restored" : "已恢复到用户音量",
            ProtectionTickStatus.BaselineUpdated => language == AppLanguage.English ? "Baseline volume updated" : "已更新基准音量",
            ProtectionTickStatus.Protecting => language == AppLanguage.English ? "Protected" : "保护中",
            ProtectionTickStatus.VolumeWriteFailed => language == AppLanguage.English ? $"Failed to set volume: {result.StatusDetail}" : $"设置音量失败：{result.StatusDetail}",
            ProtectionTickStatus.VolumeAdjusted => language == AppLanguage.English ? "Protected" : "保护中",
            _ => language == AppLanguage.English ? "Protected" : "保护中"
        };
    }

    public static string MeetingAudioStatus(AppLanguage language, MeetingAudioDiagnosticSnapshot diagnostics)
    {
        if (!diagnostics.HasDefaultDevice)
        {
            return diagnostics.ErrorMessage is null
                ? language == AppLanguage.English
                    ? "Meeting diagnostics: no default output device detected"
                    : "会议诊断：未检测到默认输出设备"
                : language == AppLanguage.English
                    ? $"Meeting diagnostics: read failed: {diagnostics.ErrorMessage}"
                    : $"会议诊断：读取失败：{diagnostics.ErrorMessage}";
        }

        if (!diagnostics.HasZoomSession)
        {
            return language == AppLanguage.English
                ? $"Output diagnostics: locked target {diagnostics.DeviceName} {diagnostics.SystemVolumePercent}%; system default output {diagnostics.DeviceName}; Zoom output no audio session detected"
                : $"输出诊断：锁定目标 {diagnostics.DeviceName} {diagnostics.SystemVolumePercent}%；系统默认输出 {diagnostics.DeviceName}；Zoom 输出 未检测到音频会话";
        }

        if (diagnostics.IsZoomMuted == true)
        {
            return language == AppLanguage.English
                ? $"Output diagnostics: locked target {diagnostics.DeviceName} {diagnostics.SystemVolumePercent}%; system default output {diagnostics.DeviceName}; Zoom output {diagnostics.ZoomDeviceName ?? diagnostics.DeviceName}; Zoom is muted"
                : $"输出诊断：锁定目标 {diagnostics.DeviceName} {diagnostics.SystemVolumePercent}%；系统默认输出 {diagnostics.DeviceName}；Zoom 输出 {diagnostics.ZoomDeviceName ?? diagnostics.DeviceName}；Zoom 已静音";
        }

        if (!diagnostics.IsZoomOnLockedDevice)
        {
            return language == AppLanguage.English
                ? $"Output diagnostics: locked target {diagnostics.DeviceName} {diagnostics.SystemVolumePercent}%; system default output {diagnostics.DeviceName}; Zoom output device {diagnostics.ZoomDeviceName}; device volume {diagnostics.ZoomDeviceVolumePercent}%; Zoom app volume {diagnostics.ZoomVolumePercent}%; tip: Zoom output device is not currently locked"
                : $"输出诊断：锁定目标 {diagnostics.DeviceName} {diagnostics.SystemVolumePercent}%；系统默认输出 {diagnostics.DeviceName}；Zoom 输出设备 {diagnostics.ZoomDeviceName}，设备音量 {diagnostics.ZoomDeviceVolumePercent}%；Zoom 应用音量 {diagnostics.ZoomVolumePercent}%；提示：当前未锁 Zoom 输出设备";
        }

        return language == AppLanguage.English
            ? $"Output diagnostics: locked target {diagnostics.DeviceName} {diagnostics.SystemVolumePercent}%; system default output {diagnostics.DeviceName}; Zoom output device {diagnostics.ZoomDeviceName ?? diagnostics.DeviceName}; Zoom app volume {diagnostics.ZoomVolumePercent}%"
            : $"输出诊断：锁定目标 {diagnostics.DeviceName} {diagnostics.SystemVolumePercent}%；系统默认输出 {diagnostics.DeviceName}；Zoom 输出设备 {diagnostics.ZoomDeviceName ?? diagnostics.DeviceName}；Zoom 应用音量 {diagnostics.ZoomVolumePercent}%";
    }

    public static string LockedTargetDetail(AppLanguage language, MeetingAudioDiagnosticSnapshot diagnostics)
    {
        if (!diagnostics.HasDefaultDevice)
        {
            return language == AppLanguage.English
                ? "Locked target: no available output device detected"
                : "锁定目标：未检测到可用输出设备";
        }

        return language == AppLanguage.English
            ? $"Locked target: {diagnostics.DeviceName} {diagnostics.SystemVolumePercent}%"
            : $"锁定目标：{diagnostics.DeviceName} {diagnostics.SystemVolumePercent}%";
    }

    public static string SystemDefaultOutput(AppLanguage language, MeetingAudioDiagnosticSnapshot diagnostics)
    {
        if (diagnostics.SystemDefaultDeviceName is null)
        {
            return language == AppLanguage.English ? "System default output: not detected" : "系统默认输出：未检测到";
        }

        return language == AppLanguage.English
            ? $"System default output: {diagnostics.SystemDefaultDeviceName}"
            : $"系统默认输出：{diagnostics.SystemDefaultDeviceName}";
    }

    public static string ZoomOutput(AppLanguage language, MeetingAudioDiagnosticSnapshot diagnostics)
    {
        if (!diagnostics.HasDefaultDevice)
        {
            return language == AppLanguage.English ? "Zoom output: not detected" : "Zoom 输出：未检测到";
        }

        if (!diagnostics.HasZoomSession)
        {
            return language == AppLanguage.English
                ? "Zoom output: no audio session detected"
                : "Zoom 输出：未检测到音频会话";
        }

        if (diagnostics.IsZoomMuted == true)
        {
            return language == AppLanguage.English
                ? $"Zoom output: {diagnostics.ZoomDeviceName ?? diagnostics.DeviceName}, muted"
                : $"Zoom 输出：{diagnostics.ZoomDeviceName ?? diagnostics.DeviceName}，已静音";
        }

        var zoomDevice = diagnostics.ZoomDeviceName ?? diagnostics.DeviceName;
        var zoomDeviceVolume = diagnostics.ZoomDeviceVolumePercent is null
            ? string.Empty
            : language == AppLanguage.English
                ? $" (device volume {diagnostics.ZoomDeviceVolumePercent}%)"
                : $"（设备音量 {diagnostics.ZoomDeviceVolumePercent}%）";

        return language == AppLanguage.English
            ? $"Zoom output device: {zoomDevice}{zoomDeviceVolume}; Zoom app volume: {diagnostics.ZoomVolumePercent}%"
            : $"Zoom 输出设备：{zoomDevice}{zoomDeviceVolume}；Zoom 应用音量：{diagnostics.ZoomVolumePercent}%";
    }

    public static string OutputWarning(AppLanguage language, MeetingAudioDiagnosticSnapshot diagnostics)
    {
        if (!diagnostics.HasDefaultDevice || !diagnostics.HasZoomSession || diagnostics.IsZoomMuted == true)
        {
            return string.Empty;
        }

        if (diagnostics.IsZoomOnLockedDevice)
        {
            return string.Empty;
        }

        return language == AppLanguage.English
            ? "Tip: the locked target is not the device Zoom is currently using."
            : "提示：当前锁定目标不是 Zoom 正在输出的设备。";
    }
}
