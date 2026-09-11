using System.Windows;

namespace MarkOne;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Farben und Schriften aus Theme.cs für das XAML bereitstellen,
        // bevor das erste Fenster entsteht.
        Theme.Install(Resources);

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
}
