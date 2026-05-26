using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSJLock.Config;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _settingsPath;

    public JsonSettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NSJ Lock"))
    {
    }

    public JsonSettingsStore(string settingsDirectory)
    {
        _settingsPath = Path.Combine(settingsDirectory, "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsPath))
        {
            return AppSettings.Defaults;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<SettingsFile>(
                stream,
                JsonOptions,
                cancellationToken);

            return settings?.ToAppSettings() ?? AppSettings.Defaults;
        }
        catch (JsonException)
        {
            return AppSettings.Defaults;
        }
        catch (IOException)
        {
            return AppSettings.Defaults;
        }
        catch (UnauthorizedAccessException)
        {
            return AppSettings.Defaults;
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = Path.Combine(
            directory ?? Path.GetTempPath(),
            $"{Path.GetFileName(_settingsPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    settings.Normalize(),
                    JsonOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(_settingsPath))
            {
                File.Copy(tempPath, _settingsPath, overwrite: true);
                File.Delete(tempPath);
            }
            else
            {
                File.Move(tempPath, _settingsPath);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private sealed class SettingsFile
    {
        public bool? IsProtectionEnabled { get; init; }

        public int? MaxVolumePercent { get; init; }

        public string? ThemeMode { get; init; }

        public string? Language { get; init; }

        public string? LockedDeviceId { get; init; }

        public AppSettings ToAppSettings()
        {
            return new AppSettings(
                IsProtectionEnabled ?? AppSettings.Defaults.IsProtectionEnabled,
                MaxVolumePercent ?? AppSettings.Defaults.MaxVolumePercent,
                ParseThemeMode(ThemeMode),
                ParseLanguage(Language),
                LockedDeviceId).Normalize();
        }

        private static AppThemeMode ParseThemeMode(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return AppSettings.Defaults.ThemeMode;
            }

            return Enum.TryParse<AppThemeMode>(value, ignoreCase: true, out var themeMode)
                ? themeMode
                : AppSettings.Defaults.ThemeMode;
        }

        private static AppLanguage ParseLanguage(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return AppSettings.Defaults.Language;
            }

            return Enum.TryParse<AppLanguage>(value, ignoreCase: true, out var language)
                ? language
                : AppSettings.Defaults.Language;
        }
    }
}
