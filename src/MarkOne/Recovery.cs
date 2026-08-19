using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MarkOne;

/// <summary>Eine liegengebliebene, ungesicherte Fassung.</summary>
public sealed class RecoveryEntry
{
    public string Id { get; set; } = "";
    public string? OriginalPath { get; set; }
    public DateTime SavedAt { get; set; }
    public string Content { get; set; } = "";

    public string DisplayName =>
        OriginalPath is null ? "Unbenanntes Dokument" : Path.GetFileName(OriginalPath);

    public string DisplayDetail =>
        (OriginalPath ?? "nie gespeichert") + "   ·   " + SavedAt.ToString("dd.MM.yyyy HH:mm");
}

/// <summary>
/// Absturzsicherung. Schreibt eine Arbeitskopie nach
/// %APPDATA%\MarkOne\recovery\ — die Originaldatei bleibt unberührt,
/// bis du bewusst speicherst.
/// </summary>
public static class Recovery
{
    private static string Dir => Path.Combine(Settings.AppDataDir, "recovery");

    private static string IdFor(string? path)
    {
        if (path is null) return "unbenannt";
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(path.ToLowerInvariant()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static void Write(string? originalPath, string content)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var entry = new RecoveryEntry
            {
                Id = IdFor(originalPath),
                OriginalPath = originalPath,
                SavedAt = DateTime.Now,
                Content = content,
            };
            // Erst daneben schreiben, dann umbenennen: ein Absturz mitten im
            // Schreiben darf die vorherige Sicherung nicht zerstören.
            var target = Path.Combine(Dir, entry.Id + ".json");
            var temp = target + ".writing";
            File.WriteAllText(temp, JsonSerializer.Serialize(entry));
            File.Move(temp, target, overwrite: true);
        }
        catch
        {
            // Eine fehlgeschlagene Sicherung darf das Tippen nicht stören.
        }
    }

    public static void Clear(string? originalPath)
    {
        try
        {
            var file = Path.Combine(Dir, IdFor(originalPath) + ".json");
            if (File.Exists(file)) File.Delete(file);
        }
        catch { }
    }

    public static List<RecoveryEntry> List()
    {
        var result = new List<RecoveryEntry>();
        try
        {
            if (!Directory.Exists(Dir)) return result;
            foreach (var file in Directory.EnumerateFiles(Dir, "*.json"))
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<RecoveryEntry>(File.ReadAllText(file));
                    if (entry is not null) result.Add(entry);
                }
                catch { }
            }
        }
        catch { }
        return result.OrderByDescending(e => e.SavedAt).ToList();
    }

    public static void Delete(RecoveryEntry entry)
    {
        try
        {
            var file = Path.Combine(Dir, entry.Id + ".json");
            if (File.Exists(file)) File.Delete(file);
        }
        catch { }
    }
}
