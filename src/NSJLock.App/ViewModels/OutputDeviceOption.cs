namespace NSJLock.App.ViewModels;

public sealed record OutputDeviceOption(
    string? DeviceId,
    string Name,
    bool IsDefault,
    string DefaultSuffix = "（系统默认）")
{
    public string DisplayName => IsDefault
        ? $"{Name}{DefaultSuffix}"
        : Name;

    public override string ToString()
    {
        return DisplayName;
    }
}
