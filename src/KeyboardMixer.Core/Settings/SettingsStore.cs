using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyboardMixer.Core.Settings;

/// <summary>Loads and saves <see cref="AppSettings"/> as JSON.</summary>
public sealed class SettingsStore
{
    public static string DefaultDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyboardMixer");

    public SettingsStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(DefaultDirectory, "settings.json");
    }

    public string FilePath { get; }

    /// <summary>
    /// Reads the settings file. A missing file gives defaults; an unreadable or corrupt file is
    /// moved aside to "settings.json.bad" and defaults are used, so the app always starts.
    /// </summary>
    public AppSettings Load()
    {
        if (!File.Exists(FilePath)) return AppSettings.CreateDefault();

        try
        {
            using var stream = File.OpenRead(FilePath);
            var settings = JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.AppSettings);
            if (settings is null) return AppSettings.CreateDefault();
            settings.Normalize();
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            TryQuarantine();
            return AppSettings.CreateDefault();
        }
    }

    /// <summary>Writes atomically: to a temp file first, then swaps it into place.</summary>
    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);

        var temp = FilePath + ".tmp";
        using (var stream = File.Create(temp))
        {
            JsonSerializer.Serialize(stream, settings, SettingsJsonContext.Default.AppSettings);
        }

        if (File.Exists(FilePath))
            File.Replace(temp, FilePath, destinationBackupFileName: null);
        else
            File.Move(temp, FilePath);
    }

    private void TryQuarantine()
    {
        try
        {
            File.Move(FilePath, FilePath + ".bad", overwrite: true);
        }
        catch (Exception)
        {
            // Best effort; the next successful save overwrites it anyway.
        }
    }
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
