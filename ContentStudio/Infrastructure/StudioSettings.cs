using System.Text.Json;

namespace ContentStudio.Infrastructure;

/// <summary>
/// Per-user tool settings, persisted outside the game repository (the game must never know
/// Content Studio exists). Lives at %APPDATA%/ContentStudio/settings.json.
/// </summary>
public sealed class StudioSettings
{
    public string? ProjectRoot { get; set; }

    /// <summary>How many timestamped backups to keep per content file before pruning the oldest.</summary>
    public int BackupVersionsPerFile { get; set; } = 30;

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ContentStudio");

    public static string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

    /// <summary>Backups live under %LOCALAPPDATA% so they never pollute the game repo or OneDrive-sync the project.</summary>
    public static string BackupRootDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ContentStudio", "backups");

    public static StudioSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
                return JsonSerializer.Deserialize<StudioSettings>(File.ReadAllText(SettingsFilePath)) ?? new StudioSettings();
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            // A corrupt settings file should never stop the tool from starting; fall back to defaults.
        }
        return new StudioSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(this, WriteOptions));
    }
}
