using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MarkOne;

/// <summary>
/// Übergabe an Windows: Dateien im zugehörigen Programm öffnen, im Explorer
/// zeigen, und herausfinden, wie dieses Programm heißt.
/// </summary>
public static class Shell
{
    /// <summary>Öffnet die Datei so, wie es ein Doppelklick im Explorer täte.</summary>
    public static bool Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Explorer mit markierter Datei bzw. geöffnetem Ordner.</summary>
    public static void Reveal(string path)
    {
        try
        {
            string args = Directory.Exists(path) ? $"\"{path}\"" : $"/select,\"{path}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
        }
        catch
        {
            // Wenn nicht einmal der Explorer startet, hilft auch keine Meldung.
        }
    }

    // ------------------------------------------------- Zugeordnetes Programm

    private const int AssocStrFriendlyAppName = 4;

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint AssocQueryStringW(uint flags, int str, string assoc, string? extra,
        StringBuilder? outBuffer, ref uint outLength);

    private static readonly ConcurrentDictionary<string, string?> AppNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Anzeigename des Programms, das Windows für diese Dateiart startet, etwa „Word“.</summary>
    public static string? FriendlyAppName(string path)
    {
        string ext = Path.GetExtension(path);
        if (ext.Length == 0) return null;
        return AppNames.GetOrAdd(ext, QueryAppName);
    }

    private static string? QueryAppName(string ext)
    {
        try
        {
            uint length = 0;
            AssocQueryStringW(0, AssocStrFriendlyAppName, ext, null, null, ref length);
            if (length == 0) return null;

            var buffer = new StringBuilder((int)length);
            uint result = AssocQueryStringW(0, AssocStrFriendlyAppName, ext, null, buffer, ref length);
            if (result != 0) return null;

            string name = buffer.ToString().Trim();
            return name.Length == 0 ? null : name;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Dateigröße lesbar: 12 KB, 1,4 MB.</summary>
    public static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} Byte";
        double kb = bytes / 1024.0;
        if (kb < 1000) return $"{kb:N0} KB";
        double mb = kb / 1024.0;
        if (mb < 1000) return $"{mb:N1} MB";
        return $"{mb / 1024.0:N2} GB";
    }
}
