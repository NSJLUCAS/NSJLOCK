namespace NSJLock.App.ViewModels;

public sealed record OutputDeviceOption(
    string? DeviceId,
    string Name,
    bool IsDefault)
{
    public string DisplayName => IsDefault
        ? $"{Name}（系统默认）"
        : Name;

    public override string ToString()
    {
        return DisplayName;
    }
}
