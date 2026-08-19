using System;
using System.IO;
using System.Text.Json;

namespace MarkOne;

/// <summary>
/// Was MarkOne sich zwischen zwei Starts merkt. Liegt in
/// %APPDATA%\MarkOne\settings.json.
/// </summary>
public sealed class Settings
{
    public string? BaseDirectory { get; set; }
    public bool AutoSave { get; set; } = true;
    public double TreeWidth { get; set; } = 320;
    public double WindowWidth { get; set; } = 1240;
    public double WindowHeight { get; set; } = 880;

    public static string AppDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MarkOne");

    private static string FilePath => Path.Combine(AppDataDir, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch
        {
            // Kaputte Einstellungsdatei darf den Start nicht verhindern.
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Einstellungen sind Komfort, kein Grund für eine Fehlermeldung.
        }
    }
}
