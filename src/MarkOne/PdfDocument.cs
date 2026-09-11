using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MarkOne;

/// <summary>
/// Ein geöffnetes PDF. Die Datei wird komplett in den Speicher gelesen,
/// damit sie auf der Platte nicht gesperrt bleibt — wer ein PDF aus Word
/// neu exportiert, soll es in MarkOne einfach neu anklicken können.
/// </summary>
public sealed class PdfDocument : IDisposable
{
    private readonly byte[] _bytes;
    private GCHandle _pin;
    private IntPtr _doc;

    public int PageCount { get; }

    /// <summary>Seitengrößen in Punkt (1/72 Zoll).</summary>
    public IReadOnlyList<Size> PageSizes { get; }

    public long FileSize => _bytes.LongLength;

    public static async Task<PdfDocument> OpenAsync(string path)
    {
        byte[] bytes = await File.ReadAllBytesAsync(path);
        return await PdfWorker.Run(() => new PdfDocument(bytes));
    }

    // Läuft auf dem PDF-Thread.
    private PdfDocument(byte[] bytes)
    {
        _bytes = bytes;
        _pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        _doc = Pdfium.FPDF_LoadMemDocument(_pin.AddrOfPinnedObject(), bytes.Length, IntPtr.Zero);
        if (_doc == IntPtr.Zero)
        {
            uint error = Pdfium.FPDF_GetLastError();
            _pin.Free();
            throw new PdfException(Pdfium.DescribeError(error));
        }

        PageCount = Pdfium.FPDF_GetPageCount(_doc);
        var sizes = new Size[PageCount];
        for (int i = 0; i < PageCount; i++)
        {
            sizes[i] = Pdfium.FPDF_GetPageSizeByIndexF(_doc, i, out var s) != 0 && s.Width > 0 && s.Height > 0
                ? new Size(s.Width, s.Height)
                : new Size(595, 842);   // A4, falls die Seite keine Größe verrät
        }
        PageSizes = sizes;
    }

    /// <summary>Zeichnet eine Seite in der gewünschten Pixelgröße, weiß hinterlegt.</summary>
    public Task<BitmapSource> RenderAsync(int index, int width, int height, CancellationToken ct) =>
        PdfWorker.Run(() => Render(index, width, height, ct), ct);

    private BitmapSource Render(int index, int width, int height, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_doc == IntPtr.Zero) throw new ObjectDisposedException(nameof(PdfDocument));

        IntPtr page = Pdfium.FPDF_LoadPage(_doc, index);
        if (page == IntPtr.Zero) throw new PdfException(Pdfium.DescribeError(6));

        try
        {
            int stride = (width * 3 + 3) & ~3;
            var buffer = new byte[stride * height];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                IntPtr bitmap = Pdfium.FPDFBitmap_CreateEx(width, height, Pdfium.FormatBgr, handle.AddrOfPinnedObject(), stride);
                if (bitmap == IntPtr.Zero) throw new PdfException("Kein Speicher für die Seitendarstellung.");
                try
                {
                    Pdfium.FPDFBitmap_FillRect(bitmap, 0, 0, width, height, 0xFFFFFFFF);
                    Pdfium.FPDF_RenderPageBitmap(bitmap, page, 0, 0, width, height, 0, Pdfium.FlagAnnotations);
                }
                finally
                {
                    Pdfium.FPDFBitmap_Destroy(bitmap);
                }
            }
            finally
            {
                handle.Free();
            }

            var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr24, null, buffer, stride);
            source.Freeze();
            return source;
        }
        finally
        {
            Pdfium.FPDF_ClosePage(page);
        }
    }

    public void Dispose()
    {
        PdfWorker.Post(() =>
        {
            if (_doc != IntPtr.Zero)
            {
                Pdfium.FPDF_CloseDocument(_doc);
                _doc = IntPtr.Zero;
            }
            if (_pin.IsAllocated) _pin.Free();
        });
    }
}
