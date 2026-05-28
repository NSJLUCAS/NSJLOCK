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
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

            return SettingsFile.FromJsonElement(document.RootElement).ToAppSettings();
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
        public bool? IsProtectionEnabled { get; private init; }

        public int? MaxVolumePercent { get; private init; }

        public string? ThemeMode { get; private init; }

        public string? Language { get; private init; }

        public string? LockedDeviceId { get; private init; }

        public string? ProtectionMode { get; private init; }

        public int? LimiterPeakThresholdPercent { get; private init; }

        public int? LimiterReleaseThresholdPercent { get; private init; }

        public int? LimiterMinimumVolumePercent { get; private init; }

        public static SettingsFile FromJsonElement(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return new SettingsFile();
            }

            return new SettingsFile
            {
                IsProtectionEnabled = TryGetBoolean(element, "isProtectionEnabled"),
                MaxVolumePercent = TryGetInt32(element, "maxVolumePercent"),
                ThemeMode = TryGetString(element, "themeMode"),
                Language = TryGetString(element, "language"),
                LockedDeviceId = TryGetString(element, "lockedDeviceId"),
                ProtectionMode = TryGetString(element, "protectionMode"),
                LimiterPeakThresholdPercent = TryGetInt32(element, "limiterPeakThresholdPercent"),
                LimiterReleaseThresholdPercent = TryGetInt32(element, "limiterReleaseThresholdPercent"),
                LimiterMinimumVolumePercent = TryGetInt32(element, "limiterMinimumVolumePercent")
            };
        }

        public AppSettings ToAppSettings()
        {
            return new AppSettings(
                IsProtectionEnabled ?? AppSettings.Defaults.IsProtectionEnabled,
                MaxVolumePercent ?? AppSettings.Defaults.MaxVolumePercent,
                ParseThemeMode(ThemeMode),
                ParseLanguage(Language),
                LockedDeviceId,
                ParseProtectionMode(ProtectionMode),
                LimiterPeakThresholdPercent ?? AppSettings.Defaults.LimiterPeakThresholdPercent,
                LimiterReleaseThresholdPercent ?? AppSettings.Defaults.LimiterReleaseThresholdPercent,
                LimiterMinimumVolumePercent ?? AppSettings.Defaults.LimiterMinimumVolumePercent).Normalize();
        }

        private static bool? TryGetBoolean(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        private static int? TryGetInt32(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property) ||
                property.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            return property.TryGetInt32(out var value) ? value : null;
        }

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property) ||
                property.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return property.GetString();
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

        private static ProtectionMode ParseProtectionMode(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return AppSettings.Defaults.ProtectionMode;
            }

            return !int.TryParse(value, out _) &&
                   Enum.TryParse<ProtectionMode>(value, ignoreCase: true, out var protectionMode) &&
                   Enum.IsDefined(protectionMode)
                ? protectionMode
                : AppSettings.Defaults.ProtectionMode;
        }
    }
}
