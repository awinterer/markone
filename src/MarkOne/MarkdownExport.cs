using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Markdig;
using Microsoft.Web.WebView2.Core;

namespace MarkOne;

/// <summary>
/// Markdown nach HTML und PDF. Die Umwandlung übernimmt Markdig (CommonMark
/// plus Tabellen, Fußnoten, Aufgabenlisten). Für das PDF baut die Edge-Engine
/// die Seite unsichtbar auf und druckt sie in eine Datei; danach wird sie
/// wieder beendet.
/// </summary>
public static class MarkdownExport
{
    // Jede Zeile bleibt eine Zeile: So sieht es im Editor aus, und so soll es aufs Papier.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseSoftlineBreakAsHardlineBreak()
        .Build();

    private static readonly Regex RxTitle = new(@"^\s{0,3}#\s+(.+?)\s*#*\s*$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex RxStrip = new(@"(\*\*|__|~~|[*_`])", RegexOptions.Compiled);

    // [Text](ein Pfad mit Leerzeichen.png): CommonMark verlangt dafür spitze Klammern,
    // Windows-Nutzer schreiben es ohne. Wir setzen sie nachträglich.
    private static readonly Regex RxSpacedLink = new(@"(!?\[[^\]\n]*\])\(([^()<>\n]*?[ ][^()<>\n]*?)\)", RegexOptions.Compiled);
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    private const string ExportHost = "markone.export";   // die erzeugte Seite
    private const string AssetHost = "markone.assets";    // der Ordner der Markdown-Datei, für Bilder

    private static readonly string TempFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MarkOne", "export");

    /// <summary>Die erste Überschrift erster Ordnung, sonst der Vorschlag.</summary>
    public static string Title(string markdown, string fallback)
    {
        var match = RxTitle.Match(markdown);
        if (!match.Success) return fallback;
        string title = RxStrip.Replace(match.Groups[1].Value, "").Trim();
        return title.Length == 0 ? fallback : title;
    }

    /// <summary>Pfade mit Leerzeichen in Links und Bildern in spitze Klammern setzen, außer in Codeblöcken.</summary>
    private static string Lenient(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        bool fence = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                fence = !fence;
                continue;
            }
            if (!fence) lines[i] = RxSpacedLink.Replace(lines[i], "$1(<$2>)");
        }
        return string.Join('\n', lines);
    }

    /// <summary>Vollständige HTML-Seite mit eingebettetem Stylesheet.</summary>
    public static string ToHtml(string markdown, string title, string css, string? baseHref = null)
    {
        string body = Markdown.ToHtml(Lenient(markdown), Pipeline);
        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n<html lang=\"de\">\n<head>\n<meta charset=\"utf-8\">\n");
        sb.Append("<meta name=\"generator\" content=\"MarkOne\">\n");
        sb.Append("<title>").Append(WebUtility.HtmlEncode(title)).Append("</title>\n");
        if (baseHref is not null) sb.Append("<base href=\"").Append(baseHref).Append("\">\n");
        sb.Append("<style>\n").Append(css).Append("\n</style>\n</head>\n<body>\n");
        sb.Append(body);
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>Schreibt eine eigenständige HTML-Datei; relative Bildpfade bleiben, wie sie sind.</summary>
    public static void SaveHtml(string markdown, string title, string htmlPath) =>
        File.WriteAllText(htmlPath, ToHtml(markdown, title, HtmlStyle.Screen()), Utf8NoBom);

    /// <summary>
    /// Erzeugt das PDF. <paramref name="sourceFolder"/> ist der Ordner der Markdown-Datei,
    /// gegen den relative Bildpfade aufgelöst werden. Liefert null bei Erfolg, sonst den Fehler in Worten.
    /// </summary>
    public static async Task<string?> ToPdfAsync(string markdown, string? sourceFolder, string title, string pdfPath, IntPtr ownerHwnd)
    {
        Directory.CreateDirectory(TempFolder);

        // Bilder liegen gern auch eine Ebene höher ("../Bilder/x.png"). Deshalb wird das
        // ganze Laufwerk eingeblendet und die Seite auf den Ordner der Datei verankert.
        string assetRoot = TempFolder;
        string baseHref = $"https://{AssetHost}/";
        if (sourceFolder is not null && Path.GetPathRoot(sourceFolder) is { Length: > 0 } root)
        {
            assetRoot = root;
            string relative = Path.GetRelativePath(root, sourceFolder).Replace('\\', '/');
            if (relative != ".")
                baseHref += string.Join("/", relative.Split('/').Select(Uri.EscapeDataString)) + "/";
        }

        string html = ToHtml(markdown, title, HtmlStyle.Print(), baseHref);
        File.WriteAllText(Path.Combine(TempFolder, "print.html"), html, Utf8NoBom);

        CoreWebView2Environment environment;
        try
        {
            environment = await CoreWebView2Environment.CreateAsync(null, HtmlViewer.UserDataFolder);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            return "Die WebView2-Laufzeit von Microsoft Edge fehlt auf diesem Rechner; ohne sie gibt es keinen PDF-Export.";
        }

        var controller = await environment.CreateCoreWebView2ControllerAsync(ownerHwnd);
        try
        {
            controller.IsVisible = false;
            controller.Bounds = new System.Drawing.Rectangle(0, 0, 1200, 1600);

            var core = controller.CoreWebView2;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.SetVirtualHostNameToFolderMapping(ExportHost, TempFolder, CoreWebView2HostResourceAccessKind.Allow);
            core.SetVirtualHostNameToFolderMapping(AssetHost, assetRoot, CoreWebView2HostResourceAccessKind.Allow);

            var loaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            core.NavigationCompleted += (_, e) => loaded.TrySetResult(e.IsSuccess);
            core.Navigate($"https://{ExportHost}/print.html");

            var finished = await Task.WhenAny(loaded.Task, Task.Delay(TimeSpan.FromSeconds(30)));
            if (finished != loaded.Task) return "Die Seite wurde nach 30 Sekunden nicht fertig.";
            if (!loaded.Task.Result) return "Die Seite ließ sich nicht aufbauen.";

            var settings = environment.CreatePrintSettings();
            settings.Orientation = CoreWebView2PrintOrientation.Portrait;
            settings.PageWidth = 8.27;                       // A4 in Zoll
            settings.PageHeight = 11.69;
            settings.MarginTop = settings.MarginBottom = 0.79;   // 20 mm
            settings.MarginLeft = settings.MarginRight = 0.79;
            settings.ShouldPrintBackgrounds = true;
            settings.ShouldPrintHeaderAndFooter = true;
            settings.HeaderTitle = title;
            settings.FooterUri = "";

            bool ok = await core.PrintToPdfAsync(pdfPath, settings);
            return ok ? null : "Die Engine hat kein PDF geschrieben.";
        }
        finally
        {
            controller.Close();
        }
    }
}
