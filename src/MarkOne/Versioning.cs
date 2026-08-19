using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MarkOne;

/// <summary>
/// Bewusste Zwischenstände. Landen als fortlaufend nummerierte Kopie im
/// Unterordner .versions neben der Datei, damit das Arbeitsverzeichnis
/// aufgeräumt bleibt.
/// </summary>
public static class Versioning
{
    public const string FolderName = ".versions";

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    public static string Save(string filePath, string content)
    {
        var folder = Path.Combine(Path.GetDirectoryName(filePath)!, FolderName);
        var info = Directory.CreateDirectory(folder);

        try
        {
            // Unter Windows macht der führende Punkt einen Ordner nicht unsichtbar.
            if ((info.Attributes & FileAttributes.Hidden) == 0)
                info.Attributes |= FileAttributes.Hidden;
        }
        catch { }

        var stem = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        var pattern = new Regex("^" + Regex.Escape(stem) + @"_v(\d+)" + Regex.Escape(extension) + "$",
            RegexOptions.IgnoreCase);

        int next = 1;
        foreach (var existing in Directory.EnumerateFiles(folder))
        {
            var match = pattern.Match(Path.GetFileName(existing));
            if (match.Success && int.TryParse(match.Groups[1].Value, out int number) && number >= next)
                next = number + 1;
        }

        var target = Path.Combine(folder, $"{stem}_v{next:D3}{extension}");
        File.WriteAllText(target, content, Utf8NoBom);
        return target;
    }

    public static int CountFor(string filePath)
    {
        try
        {
            var folder = Path.Combine(Path.GetDirectoryName(filePath)!, FolderName);
            if (!Directory.Exists(folder)) return 0;
            var stem = Path.GetFileNameWithoutExtension(filePath);
            return Directory.EnumerateFiles(folder, stem + "_v*").Count();
        }
        catch { return 0; }
    }
}
