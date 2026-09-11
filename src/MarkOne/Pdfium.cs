using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MarkOne;

/// <summary>
/// Die Handvoll PDFium-Funktionen, die MarkOne braucht. Die Bibliothek
/// (pdfium.dll, derselbe PDF-Kern wie in Chrome) liegt neben der EXE und
/// kommt aus dem NuGet-Paket bblanchon.PDFium.Win32.
/// </summary>
internal static class Pdfium
{
    private const string Lib = "pdfium";

    public const int FormatBgr = 2;             // FPDFBitmap_BGR: 3 Byte je Pixel, ohne Alpha (Gray=1, BGR=2, BGRx=3, BGRA=4)
    public const int FlagAnnotations = 0x01;    // FPDF_ANNOT: Kommentare und Markierungen mitzeichnen

    [StructLayout(LayoutKind.Sequential)]
    public struct SizeF { public float Width, Height; }

    [DllImport(Lib)] public static extern void FPDF_InitLibrary();
    [DllImport(Lib)] public static extern IntPtr FPDF_LoadMemDocument(IntPtr data, int size, IntPtr password);
    [DllImport(Lib)] public static extern void FPDF_CloseDocument(IntPtr document);
    [DllImport(Lib)] public static extern uint FPDF_GetLastError();
    [DllImport(Lib)] public static extern int FPDF_GetPageCount(IntPtr document);
    [DllImport(Lib)] public static extern int FPDF_GetPageSizeByIndexF(IntPtr document, int index, out SizeF size);
    [DllImport(Lib)] public static extern IntPtr FPDF_LoadPage(IntPtr document, int index);
    [DllImport(Lib)] public static extern void FPDF_ClosePage(IntPtr page);
    [DllImport(Lib)] public static extern IntPtr FPDFBitmap_CreateEx(int width, int height, int format, IntPtr firstScan, int stride);
    [DllImport(Lib)] public static extern void FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);
    [DllImport(Lib)] public static extern void FPDFBitmap_Destroy(IntPtr bitmap);
    [DllImport(Lib)] public static extern void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

    public static string DescribeError(uint code) => code switch
    {
        2 => "Die Datei ließ sich nicht lesen.",
        3 => "Die Datei ist kein gültiges PDF oder beschädigt.",
        4 => "Das PDF ist mit einem Passwort geschützt.",
        5 => "Das PDF verwendet ein nicht unterstütztes Sicherheitsverfahren.",
        6 => "Die Seite wurde nicht gefunden.",
        _ => "Unbekannter Fehler beim Lesen des PDF.",
    };
}

/// <summary>Fehler aus PDFium, in Worten.</summary>
public sealed class PdfException : Exception
{
    public PdfException(string message) : base(message) { }
}

/// <summary>
/// PDFium ist nicht threadsicher. Deshalb laufen sämtliche Aufrufe auf
/// diesem einen Hintergrundthread, nacheinander; die Oberfläche wartet
/// asynchron auf die Ergebnisse und bleibt bedienbar.
/// </summary>
internal static class PdfWorker
{
    private static readonly BlockingCollection<Action> Queue = new();
    private static Exception? _initError;

    static PdfWorker()
    {
        var thread = new Thread(Loop) { IsBackground = true, Name = "PDFium" };
        thread.Start();
    }

    private static void Loop()
    {
        try
        {
            Pdfium.FPDF_InitLibrary();
        }
        catch (Exception ex)
        {
            _initError = ex;   // pdfium.dll fehlt; jede Anfrage meldet das dann sauber
        }

        foreach (var work in Queue.GetConsumingEnumerable())
        {
            try { work(); } catch { /* Fehler sind in den Tasks der Aufrufer gelandet */ }
        }
    }

    public static Task<T> Run<T>(Func<T> work, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Queue.Add(() =>
        {
            if (_initError is not null)
            {
                tcs.TrySetException(new PdfException(
                    "Die PDF-Bibliothek (pdfium.dll) wurde nicht gefunden. Sie gehört neben MarkOne.exe.\n" + _initError.Message));
                return;
            }
            if (ct.IsCancellationRequested) { tcs.TrySetCanceled(ct); return; }
            try { tcs.TrySetResult(work()); }
            catch (OperationCanceledException) { tcs.TrySetCanceled(); }
            catch (Exception ex) { tcs.TrySetException(ex); }
        });
        return tcs.Task;
    }

    public static void Post(Action work) => Queue.Add(work);
}
