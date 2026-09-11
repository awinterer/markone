using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MarkOne;

public abstract class TreeNode : INotifyPropertyChanged
{
    public string FullPath { get; init; } = "";
    public string Name { get; init; } = "";
    public ObservableCollection<TreeNode> Children { get; } = new();

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            Notify();
            if (value) OnExpanded();
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; Notify(); }
    }

    protected virtual void OnExpanded() { }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Platzhalter, damit der Aufklapppfeil erscheint, bevor eingelesen wurde.</summary>
public sealed class LoadingNode : TreeNode
{
    public LoadingNode() { Name = "wird gelesen …"; }
}

public sealed class FolderNode : TreeNode
{
    private bool _loaded;
    private bool _loading;

    public FolderNode(string path)
    {
        FullPath = path;
        Name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(Name)) Name = path;   // Laufwerkswurzel
        Children.Add(new LoadingNode());
    }

    protected override void OnExpanded() => _ = LoadAsync();

    /// <summary>Ob der Inhalt schon einmal eingelesen wurde.</summary>
    public bool IsLoaded => _loaded;

    public async Task LoadAsync(bool force = false)
    {
        if (_loading || (_loaded && !force)) return;
        _loading = true;
        try
        {
            var items = await Task.Run(() => FileScanner.Scan(FullPath));
            Children.Clear();
            foreach (var item in items)
            {
                if (item.IsFolder) Children.Add(new FolderNode(item.Path));
                else Children.Add(new FileNode(item.Path) { Heading = item.Heading });
            }
            _loaded = true;
        }
        catch
        {
            Children.Clear();
        }
        finally
        {
            _loading = false;
        }
    }
}

/// <summary>Was MarkOne mit einer Datei anfangen kann.</summary>
public enum FileKind
{
    /// <summary>Markdown oder Text: wird im Editor bearbeitet.</summary>
    Markdown,
    /// <summary>Bild: wird im eingebauten Betrachter gezeigt.</summary>
    Image,
    /// <summary>PDF: wird im eingebauten Betrachter gezeigt.</summary>
    Pdf,
    /// <summary>HTML: wird mit der Edge-Engine von Windows gezeigt.</summary>
    Html,
    /// <summary>Alles andere: öffnet per Doppelklick im zugehörigen Programm.</summary>
    Other,
}

public sealed class FileNode : TreeNode
{
    public FileNode(string path)
    {
        FullPath = path;
        Name = Path.GetFileName(path);
        Kind = FileScanner.KindOf(path);
        TypeLabel = FileScanner.TypeLabel(path);
    }

    public FileKind Kind { get; }

    /// <summary>Steuert im Baum die zweizeilige Darstellung mit Überschrift.</summary>
    public bool IsMarkdown => Kind == FileKind.Markdown;

    /// <summary>Kurzes Typkürzel wie PDF oder DOCX für Dateien, die kein Markdown sind.</summary>
    public string TypeLabel { get; }

    private string? _heading;
    public string? Heading
    {
        get => _heading;
        set { _heading = value; Notify(); Notify(nameof(Display)); }
    }

    /// <summary>Erste Zeile im Baum: die Überschrift, sonst ein dezenter Hinweis.</summary>
    public string Display => string.IsNullOrWhiteSpace(_heading) ? "(ohne Überschrift)" : _heading!;
}

public sealed record ScanItem(bool IsFolder, string Path, string Name, string? Heading);

public static class FileScanner
{
    public static readonly string[] MarkdownExtensions = { ".md", ".markdown", ".txt" };

    // Was die Windows-Bilddecoder von Haus aus lesen; WebP, HEIC und AVIF nur mit
    // den zugehörigen Erweiterungen aus dem Store.
    public static readonly string[] ImageExtensions =
    {
        ".png", ".jpg", ".jpeg", ".jfif", ".gif", ".bmp", ".tif", ".tiff",
        ".ico", ".webp", ".heic", ".heif", ".avif",
    };

    public static readonly string[] PdfExtensions = { ".pdf" };
    public static readonly string[] HtmlExtensions = { ".html", ".htm", ".xhtml" };

    /// <summary>Zeigt der Baum alle Dateien oder nur die, die MarkOne selbst öffnet?</summary>
    public static bool ShowAllFiles { get; set; } = true;

    // Ordner, in denen niemand seine Notizen sucht.
    private static readonly HashSet<string> Skip = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", ".svn", ".hg", ".vs", ".idea", "bin", "obj",
        "dist", "build", "packages", "__pycache__", ".venv", "venv",
        Versioning.FolderName,
    };

    // Dateien, die Windows und Office nebenbei ablegen.
    private static readonly HashSet<string> SkipFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "desktop.ini", "thumbs.db", ".ds_store",
    };

    private static readonly Regex RxHeading = new(@"^\s{0,3}(#{1,6})\s+(.+?)\s*#*\s*$", RegexOptions.Compiled);
    private static readonly Regex RxStripInline = new(@"(\*\*|__|~~|[*_`])", RegexOptions.Compiled);
    private static readonly Regex RxStripLink = new(@"\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled);

    // Überschriften ändern sich selten — merken, solange die Datei unverändert ist.
    private static readonly ConcurrentDictionary<string, (DateTime Stamp, string? Heading)> Cache = new();

    public static FileKind KindOf(string path)
    {
        string ext = Path.GetExtension(path);
        if (Has(MarkdownExtensions, ext)) return FileKind.Markdown;
        if (Has(ImageExtensions, ext)) return FileKind.Image;
        if (Has(PdfExtensions, ext)) return FileKind.Pdf;
        if (Has(HtmlExtensions, ext)) return FileKind.Html;
        return FileKind.Other;
    }

    public static bool IsMarkdown(string path) => KindOf(path) == FileKind.Markdown;

    /// <summary>Kürzel für den Baum: Erweiterung in Großbuchstaben, ohne Punkt.</summary>
    public static string TypeLabel(string path)
    {
        string ext = Path.GetExtension(path).TrimStart('.');
        return ext.Length == 0 ? "—" : ext.ToUpperInvariant();
    }

    private static bool Has(string[] list, string ext) =>
        list.Contains(ext, StringComparer.OrdinalIgnoreCase);

    /// <summary>Gehört die Datei nach den aktuellen Einstellungen in den Baum?</summary>
    private static bool IsShown(string path)
    {
        if (SkipFiles.Contains(Path.GetFileName(path))) return false;
        return ShowAllFiles || IsMarkdown(path);
    }

    public static List<ScanItem> Scan(string directory)
    {
        var folders = new List<ScanItem>();
        var files = new List<ScanItem>();

        try
        {
            foreach (var sub in Directory.EnumerateDirectories(directory))
            {
                if (ShouldSkip(sub)) continue;
                if (!HasContentBelow(sub, 0)) continue;
                folders.Add(new ScanItem(true, sub, Path.GetFileName(sub), null));
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if (!IsShown(file)) continue;
                if (IsHidden(file)) continue;
                string? heading = IsMarkdown(file) ? ReadHeading(file) : null;
                files.Add(new ScanItem(false, file, Path.GetFileName(file), heading));
            }
        }
        catch
        {
            // Kein Zugriff: liefern, was bis hierher zusammenkam.
        }

        var comparer = StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true);
        folders.Sort((a, b) => comparer.Compare(a.Name, b.Name));
        files.Sort((a, b) => comparer.Compare(a.Name, b.Name));

        folders.AddRange(files);
        return folders;
    }

    private static bool ShouldSkip(string directory)
    {
        var name = Path.GetFileName(directory);
        if (Skip.Contains(name)) return true;
        if (name.Length > 0 && name[0] == '.') return true;
        return IsHidden(directory);
    }

    private static bool IsHidden(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0;
        }
        catch { return false; }
    }

    /// <summary>Liegt irgendwo unterhalb eine Datei, die uns interessiert?</summary>
    private static bool HasContentBelow(string directory, int depth)
    {
        if (depth > 8) return true;   // im Zweifel lieber anzeigen als verschlucken
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory))
                if (IsShown(file) && !IsHidden(file)) return true;

            foreach (var sub in Directory.EnumerateDirectories(directory))
            {
                if (ShouldSkip(sub)) continue;
                if (HasContentBelow(sub, depth + 1)) return true;
            }
        }
        catch
        {
            return false;   // nicht lesbar heißt: für dich ohnehin nicht nutzbar
        }
        return false;
    }

    public static string? ReadHeading(string path)
    {
        try
        {
            var stamp = File.GetLastWriteTimeUtc(path);
            if (Cache.TryGetValue(path, out var cached) && cached.Stamp == stamp)
                return cached.Heading;

            string? heading = ExtractHeading(path);
            Cache[path] = (stamp, heading);
            return heading;
        }
        catch { return null; }
    }

    private static string? ExtractHeading(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        bool inFrontMatter = false;
        for (int lineNumber = 0; lineNumber < 60; lineNumber++)
        {
            string? line = reader.ReadLine();
            if (line is null) break;

            var trimmed = line.Trim();

            // YAML-Kopf am Dateianfang überspringen.
            if (lineNumber == 0 && trimmed == "---") { inFrontMatter = true; continue; }
            if (inFrontMatter)
            {
                if (trimmed is "---" or "...") inFrontMatter = false;
                continue;
            }

            var match = RxHeading.Match(line);
            if (match.Success) return Clean(match.Groups[2].Value);
        }
        return null;
    }

    /// <summary>Auszeichnung aus der Überschrift entfernen — im Baum stört sie nur.</summary>
    private static string Clean(string text)
    {
        text = RxStripLink.Replace(text, "$1");
        text = RxStripInline.Replace(text, "");
        return text.Trim();
    }

    /// <summary>Vergisst gemerkte Überschriften einer Datei (nach dem Speichern).</summary>
    public static void Forget(string path) => Cache.TryRemove(path, out _);
}
