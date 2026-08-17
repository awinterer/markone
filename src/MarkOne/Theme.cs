using System.Windows;
using System.Windows.Media;

namespace MarkOne;

/// <summary>
/// Sämtliche Typografie- und Farbentscheidungen an einem Ort.
/// Wer das Aussehen ändern will, ändert nur diese Datei.
/// (Heißt bewusst nicht "Style" — das kollidiert mit Window.Style.)
/// </summary>
public static class Theme
{
    // --- Schriften -------------------------------------------------------
    // WPF akzeptiert eine Fallback-Liste; das erste installierte gewinnt.
    public static readonly FontFamily Body = new("Georgia, Cambria, Segoe UI");
    public static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Courier New");

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

    // --- Farben ----------------------------------------------------------
    public static readonly SolidColorBrush Paper = Frozen("#FDFCFA");
    public static readonly SolidColorBrush Chrome = Frozen("#F1EEE8");
    public static readonly SolidColorBrush ChromeBorder = Frozen("#E3DED4");
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

    private static SolidColorBrush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}
