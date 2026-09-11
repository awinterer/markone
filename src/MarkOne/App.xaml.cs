using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace MarkOne;

public partial class App : Application
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Farben und Schriften aus Theme.cs für das XAML bereitstellen,
        // bevor das erste Fenster entsteht.
        Theme.Install(Resources);

        // Kommandozeile: MarkOne.exe --pdf datei.md [ziel.pdf] bzw. --html datei.md [ziel.html]
        if (e.Args.Length >= 2 && e.Args[0] is "--pdf" or "--html")
        {
            _ = ExportAndExitAsync(e.Args);
            return;
        }

        var window = new MainWindow();

        // Erlaubt "Öffnen mit" aus dem Explorer heraus. Ein Ordner als
        // Parameter wird zum Basisverzeichnis, ohne gemerkt zu werden.
        if (e.Args.Length > 0)
        {
            if (System.IO.Directory.Exists(e.Args[0]))
                window.StartupFolder = e.Args[0];
            else
                window.LoadFile(e.Args[0]);
        }

        window.Show();
    }

    /// <summary>Export ohne Fenster; Meldungen gehen an die Konsole, aus der MarkOne gestartet wurde.</summary>
    private async Task ExportAndExitAsync(string[] args)
    {
        AttachConsole(-1);
        int code = 1;
        try
        {
            bool pdf = args[0] == "--pdf";
            string source = Path.GetFullPath(args[1]);
            string target = args.Length >= 3 ? Path.GetFullPath(args[2]) : Path.ChangeExtension(source, pdf ? ".pdf" : ".html");
            string markdown = File.ReadAllText(source, Encoding.UTF8);
            string title = MarkdownExport.Title(markdown, Path.GetFileNameWithoutExtension(source));

            if (pdf)
            {
                // Die Engine braucht ein Fensterhandle; das Fenster bleibt unsichtbar.
                var host = new Window { Width = 1, Height = 1, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
                var hwnd = new WindowInteropHelper(host).EnsureHandle();
                string? error = await MarkdownExport.ToPdfAsync(markdown, Path.GetDirectoryName(source), title, target, hwnd);
                if (error is null) code = 0;
                else Console.Error.WriteLine(error);
            }
            else
            {
                MarkdownExport.SaveHtml(markdown, title, target);
                code = 0;
            }

            if (code == 0) Console.WriteLine(target);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
        }
        Shutdown(code);
    }
}
