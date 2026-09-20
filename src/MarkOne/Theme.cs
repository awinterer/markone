using System.Windows;
using System.Windows.Media;

namespace MarkOne;

/// <summary>
/// Sämtliche Typografie- und Farbentscheidungen an einem Ort.
/// Wer das Aussehen ändern will, ändert nur diese Datei.
/// (Heißt bewusst nicht "Style" — das kollidiert mit Window.Style.)
///
/// Das XAML greift über <see cref="Install"/> auf dieselben Werte zu; dort
/// steht keine Farbe mehr als Zahl. Ein Dark Mode tauscht später nur die
/// Einträge in <see cref="Install"/> aus.
/// </summary>
public static class Theme
{
    // --- Schriften -------------------------------------------------------
    // WPF akzeptiert eine Fallback-Liste; das erste installierte gewinnt.
    public static readonly FontFamily Body = new("Georgia, Cambria, Segoe UI");
    public static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Courier New");
    public static readonly FontFamily Ui = new("Segoe UI");

    /// <summary>Einfarbige Symbole aus Windows selbst — keine Bilddateien nötig.</summary>
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    // --- Größen ----------------------------------------------------------
    public const double BaseSize = 16.5;
    public const double BaseLineHeight = 28.0;   // ~1.7 zeilig, das ist der halbe Trick
    public const double CodeSize = 14.5;

    public static double HeadingSize(int level) => level switch
    {
        1 => 30.0,
        2 => 24.0,
        3 => 20.0,
        4 => 17.5,
        _ => 16.5,
    };

    public static double HeadingLineHeight(int level) => HeadingSize(level) * 1.35;

    public static Thickness HeadingMargin(int level) => level switch
    {
        1 => new Thickness(0, 26, 0, 10),
        2 => new Thickness(0, 24, 0, 8),
        3 => new Thickness(0, 20, 0, 6),
        _ => new Thickness(0, 16, 0, 4),
    };

    public static readonly Thickness BodyMargin = new(0, 0, 0, 10);

    /// <summary>Maximale Textspaltenbreite. Alles darüber liest sich schlecht.</summary>
    public const double MaxColumnWidth = 760.0;

    // --- Farben: Text ----------------------------------------------------
    public static readonly SolidColorBrush Paper = Frozen("#FDFCFA");
    public static readonly SolidColorBrush Text = Frozen("#33322E");
    public static readonly SolidColorBrush Heading = Frozen("#1C1B18");
    public static readonly SolidColorBrush Marker = Frozen("#C3BEB3");   // die ## und ** — da, aber leise
    public static readonly SolidColorBrush Muted = Frozen("#8A857B");
    public static readonly SolidColorBrush Quote = Frozen("#6E6A62");
    public static readonly SolidColorBrush QuoteBar = Frozen("#DCD6C9");
    public static readonly SolidColorBrush Code = Frozen("#8A5A2B");
    public static readonly SolidColorBrush CodeBg = Frozen("#F2EEE6");
    public static readonly SolidColorBrush Link = Frozen("#3E6E9E");
    public static readonly SolidColorBrush Rule = Frozen("#D8D3C8");
    public static readonly SolidColorBrush Selection = Frozen("#CFE0F0");

    // --- Farben: Rahmen (Kopfzeile, Statuszeile, Dialoge) ----------------
    public static readonly SolidColorBrush Chrome = Frozen("#F1EEE8");
    public static readonly SolidColorBrush ChromeBorder = Frozen("#E3DED4");
    public static readonly SolidColorBrush ChromeText = Frozen("#3B382F");
    public static readonly SolidColorBrush Highlight = Frozen("#E4DCCB");        // Maus über Knopf, Auswahl im Baum
    public static readonly SolidColorBrush HighlightStrong = Frozen("#D8CDB6");  // Knopf gedrückt, Menü offen
    public static readonly SolidColorBrush Disabled = Frozen("#B5AFA3");
    public static readonly SolidColorBrush Success = Frozen("#5F7F4F");          // "Gespeichert" in der Statuszeile
    public static readonly SolidColorBrush ScrollThumb = Frozen("#CDC6B9");
    public static readonly SolidColorBrush ScrollThumbHover = Frozen("#A9A296");

    // --- Farben: Tabellen im Editor ---------------------------------------
    public static readonly SolidColorBrush TableHeaderBg = Frozen("#F4F0E8");
    public static readonly SolidColorBrush TableLine = Frozen("#E6E1D6");

    // --- Farben: Betrachter für Bilder und PDF ---------------------------
    public static readonly SolidColorBrush ViewerBg = Frozen("#E4DFD5");        // dunkler als der Rahmen, damit Seiten und Bilder tragen
    public static readonly SolidColorBrush PageBorder = Frozen("#CFC9BE");

    // --- Farben: Navigationsbaum -----------------------------------------
    public static readonly SolidColorBrush TreeBg = Frozen("#F7F4EE");
    public static readonly SolidColorBrush TreeFolder = Frozen("#4A473F");
    public static readonly SolidColorBrush TreeHeading = Frozen("#2B2A26");
    public static readonly SolidColorBrush TreeFileName = Frozen("#9A958B");
    public static readonly SolidColorBrush TreeNoHeading = Frozen("#B5AFA3");
    public static readonly SolidColorBrush TreeSelected = Frozen("#E4DCCB");
    public static readonly SolidColorBrush TreeHover = Frozen("#EDE8DE");

    // --- Ressourcen für XAML ---------------------------------------------

    /// <summary>
    /// Trägt Pinsel und Schriften unter festen Namen in ein Ressourcenwörterbuch
    /// ein. Das XAML verweist per DynamicResource darauf.
    /// </summary>
    public static void Install(ResourceDictionary r)
    {
        r["Font.Body"] = Body;
        r["Font.Mono"] = Mono;
        r["Font.Ui"] = Ui;
        r["Font.Icons"] = Icons;

        r["Brush.Paper"] = Paper;
        r["Brush.Text"] = Text;
        r["Brush.Muted"] = Muted;
        r["Brush.Rule"] = Rule;
        r["Brush.Selection"] = Selection;

        r["Brush.Chrome"] = Chrome;
        r["Brush.ChromeBorder"] = ChromeBorder;
        r["Brush.ChromeText"] = ChromeText;
        r["Brush.Highlight"] = Highlight;
        r["Brush.HighlightStrong"] = HighlightStrong;
        r["Brush.Disabled"] = Disabled;
        r["Brush.Success"] = Success;
        r["Brush.ScrollThumb"] = ScrollThumb;
        r["Brush.ScrollThumbHover"] = ScrollThumbHover;
        r["Brush.ViewerBg"] = ViewerBg;
        r["Brush.PageBorder"] = PageBorder;

        r["Brush.TreeBg"] = TreeBg;
        r["Brush.TreeFolder"] = TreeFolder;
        r["Brush.TreeHeading"] = TreeHeading;
        r["Brush.TreeFileName"] = TreeFileName;
        r["Brush.TreeNoHeading"] = TreeNoHeading;
        r["Brush.TreeSelected"] = TreeSelected;
        r["Brush.TreeHover"] = TreeHover;
    }

    private static SolidColorBrush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}
