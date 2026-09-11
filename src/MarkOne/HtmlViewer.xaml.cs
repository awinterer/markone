using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MarkOne;

/// <summary>
/// HTML-Betrachter auf Basis der Edge-Engine (WebView2), die Windows 11
/// mitbringt. MarkOne liefert sie nicht mit und hält sie auch nicht vor:
/// Die Engine wird erst gestartet, wenn eine HTML-Datei zu sehen ist, und
/// beim Verlassen wieder beendet. Links nach draußen öffnen im Browser,
/// Downloads gibt es nicht, und eine Seite ohne eigenes Stylesheet bekommt
/// die Typografie von MarkOne.
/// </summary>
public partial class HtmlViewer : UserControl
{
    public string Status { get; private set; } = "";
    public event EventHandler? StatusChanged;

    private WebView2? _web;
    private Task? _ready;
    private string? _path;
    private long _size;
    private int _loadId;

    internal static readonly string UserDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MarkOne", "WebView2");

    public HtmlViewer()
    {
        InitializeComponent();
    }

    // ================================================================ Laden

    public async void Load(string path)
    {
        int id = ++_loadId;
        _path = path;
        ShowMessage(null);
        SetStatus("wird geladen …");

        try
        {
            _size = new FileInfo(path).Length;
            _ready ??= InitAsync();
            await _ready;
            if (id != _loadId || _web?.CoreWebView2 is null) return;

            _web.CoreWebView2.Navigate(new Uri(path).AbsoluteUri);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            if (id != _loadId) return;
            _ready = null;
            ShowMessage("Die WebView2-Laufzeit von Microsoft Edge fehlt auf diesem Rechner.\n\n"
                      + "Sie gehört zu Windows 11 und lässt sich bei Bedarf über microsoft.com nachinstallieren.");
            SetStatus("");
        }
        catch (Exception ex)
        {
            if (id != _loadId) return;
            _ready = null;
            ShowMessage("Die HTML-Datei konnte nicht angezeigt werden.\n\n" + ex.Message);
            SetStatus("");
        }
    }

    /// <summary>Engine beenden, Speicher freigeben.</summary>
    public void Clear()
    {
        _loadId++;
        _path = null;
        _ready = null;
        if (_web is not null)
        {
            Host.Child = null;
            _web.Dispose();
            _web = null;
        }
        SetStatus("");
    }

    private async Task InitAsync()
    {
        var environment = await CoreWebView2Environment.CreateAsync(null, UserDataFolder);

        var web = new WebView2
        {
            DefaultBackgroundColor = System.Drawing.Color.FromArgb(
                Theme.Paper.Color.R, Theme.Paper.Color.G, Theme.Paper.Color.B),
        };
        Host.Child = web;
        _web = web;
        await web.EnsureCoreWebView2Async(environment);

        var core = web.CoreWebView2;
        var settings = core.Settings;
        settings.AreDevToolsEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;   // F5, Strg+P und Co. bleiben bei MarkOne
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.AreHostObjectsAllowed = false;
        settings.IsZoomControlEnabled = true;

        core.NavigationStarting += OnNavigating;
        core.NewWindowRequested += (_, e) => { e.Handled = true; OpenOutside(e.Uri); };
        core.DownloadStarting += (_, e) => e.Cancel = true;
        core.NavigationCompleted += (_, _) => UpdateStatus();
        core.DocumentTitleChanged += (_, _) => UpdateStatus();
        web.ZoomFactorChanged += (_, _) => UpdateStatus();

        await core.AddScriptToExecuteOnDocumentCreatedAsync(DefaultStyleScript());
    }

    // ============================================================ Navigation

    /// <summary>
    /// Innerhalb lokaler HTML-, Text- und Bilddateien darf die Seite navigieren.
    /// Alles im Netz öffnet im Browser; andere lokale Dateien werden nicht angefasst,
    /// eine Seite soll nichts starten können.
    /// </summary>
    private void OnNavigating(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (e.Uri == "about:blank") return;
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri)) { e.Cancel = true; return; }

        if (uri.IsFile)
        {
            var kind = FileScanner.KindOf(uri.LocalPath);
            if (kind is FileKind.Html or FileKind.Markdown or FileKind.Image)
            {
                _path = uri.LocalPath;
                try { _size = new FileInfo(_path).Length; } catch { }
                return;
            }
            e.Cancel = true;
            return;
        }

        e.Cancel = true;
        OpenOutside(e.Uri);
    }

    private static void OpenOutside(string uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var u) && u.Scheme is "http" or "https" or "mailto")
            Shell.Open(uri);
    }

    // ================================================================= Zoom

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_web is null || (Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        switch (e.Key)
        {
            case Key.D0 or Key.NumPad0: _web.ZoomFactor = 1.0; break;
            case Key.OemPlus or Key.Add: _web.ZoomFactor = Math.Min(5.0, _web.ZoomFactor * 1.1); break;
            case Key.OemMinus or Key.Subtract: _web.ZoomFactor = Math.Max(0.25, _web.ZoomFactor / 1.1); break;
            default: return;
        }
        e.Handled = true;
    }

    // ============================================================ Gestaltung

    /// <summary>
    /// Läuft in jeder Seite an. Hat die Seite keinerlei eigene Stilangaben,
    /// bekommt sie die Typografie des Editors; sonst bleibt sie, wie sie ist.
    /// </summary>
    private static string DefaultStyleScript()
    {
        string css = HtmlStyle.Screen();
        return
            "document.addEventListener('DOMContentLoaded',function(){" +
            "if(document.querySelector('link[rel~=\"stylesheet\"],style')||(document.body&&document.body.getAttribute('style')))return;" +
            "var s=document.createElement('style');s.textContent=" + System.Text.Json.JsonSerializer.Serialize(css) + ";" +
            "document.head.appendChild(s);});";
    }

    // =============================================================== Status

    private void UpdateStatus()
    {
        if (_web?.CoreWebView2 is null || _path is null) return;
        string title = _web.CoreWebView2.DocumentTitle ?? "";
        string name = Path.GetFileName(_path);
        string head = title.Length > 0 && !string.Equals(title, name, StringComparison.OrdinalIgnoreCase)
            ? title + "  ·  " : "";
        SetStatus($"{head}{Shell.FormatSize(_size)}  ·  {_web.ZoomFactor:P0}");
    }

    private void SetStatus(string text)
    {
        Status = text;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ShowMessage(string? text)
    {
        Message.Text = text ?? "";
        Message.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
    }
}
