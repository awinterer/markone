using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MarkOne;

/// <summary>
/// Färbt die Titelleiste unter Windows 11 in der Farbe der Kopfzeile, damit
/// das Fenster aus einem Guss wirkt statt Windows-Weiß über Creme. Ältere
/// Windows-Versionen kennen die Attribute nicht; dort passiert nichts.
/// </summary>
public static class TitleBar
{
    private const int BorderColor = 34;    // DWMWA_BORDER_COLOR
    private const int CaptionColor = 35;   // DWMWA_CAPTION_COLOR
    private const int TextColor = 36;      // DWMWA_TEXT_COLOR

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint value, int size);

    /// <summary>Im Konstruktor eines Fensters aufrufen; wirkt, sobald das Fenster ein Handle hat.</summary>
    public static void Attach(Window window) =>
        window.SourceInitialized += (_, _) => Apply(window);

    private static void Apply(Window window)
    {
        if (Environment.OSVersion.Version.Build < 22000) return;   // Windows 11

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        Set(hwnd, CaptionColor, Theme.Chrome.Color);
        Set(hwnd, TextColor, Theme.Text.Color);
        Set(hwnd, BorderColor, Theme.Rule.Color);
    }

    private static void Set(IntPtr hwnd, int attribute, Color color)
    {
        uint colorref = (uint)(color.R | (color.G << 8) | (color.B << 16));   // COLORREF ist 0x00BBGGRR
        try
        {
            DwmSetWindowAttribute(hwnd, attribute, ref colorref, sizeof(uint));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Kein DWM, keine Farbe — kein Drama.
        }
    }
}
