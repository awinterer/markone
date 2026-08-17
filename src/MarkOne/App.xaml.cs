using System.Windows;

namespace MarkOne;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();

        // Erlaubt "Öffnen mit" aus dem Explorer heraus.
        if (e.Args.Length > 0)
        {
            window.LoadFile(e.Args[0]);
        }

        window.Show();
    }
}
