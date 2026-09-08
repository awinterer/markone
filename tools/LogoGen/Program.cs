using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LogoGen;

/// <summary>Farbwelt einer Variante des Zeichens.</summary>
sealed record Palette(string Suffix, string Tile, string Stroke, string Accent, string? TileBorder);

/// <summary>
/// Das Zeichen: eine Raute (#), das Zeichen, mit dem in Markdown jede
/// Überschrift beginnt, mit geneigten Aufrechten wie in einer
/// Serifenschrift. Die eine gefüllte Kammer in der Mitte ist das „One".
/// Entworfen in einem 256er-Raster; jede Ausgabegröße wird daraus gerendert.
/// </summary>
static class Program
{
    const double S = 256;             // Kachelkante
    const double Radius = 56;         // Eckenradius der Kachel
    const double W = 32;              // Strichstärke
    const double Slant = 0.2126;      // tan 12°: Neigung der Aufrechten
    const double Lo = 40, Hi = 216;   // Ausdehnung der Raute in der Kachel
    const double A = 96, B = 160;     // Mittellinien der Balken bzw. Aufrechten
    const double Mid = 128;

    // Tinte auf dunkler Kachel (Standard) und Tinte auf Papier.
    static readonly Palette Tinte  = new("",        "#2E2C28", "#F4EFE4", "#D89A4E", null);
    static readonly Palette Papier = new("-papier", "#FBF8F2", "#33322E", "#B8742F", "#D6CFC2");

    // Wortmarke
    const string WordFont = "Georgia";
    const double WordSize = 250;
    const double WordGap = 64;
    const string WordInk = "#33322E";
    const string WordAccent = "#B8742F";

    static readonly Encoding Utf8 = new UTF8Encoding(false);

    [STAThread]
    static int Main(string[] args)
    {
        string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
        string logoDir = Path.Combine(root, "assets", "logo");
        string iconDir = Path.Combine(root, "assets", "icon");
        string previewDir = args.Length > 1 ? Path.GetFullPath(args[1]) : logoDir;
        Directory.CreateDirectory(logoDir);
        Directory.CreateDirectory(iconDir);
        Directory.CreateDirectory(previewDir);

        foreach (var p in new[] { Tinte, Papier })
        {
            Write(Path.Combine(logoDir, $"markone-mark{p.Suffix}.svg"), MarkSvg(p));
            WriteIco(Path.Combine(iconDir, $"markone{p.Suffix}.ico"), p);
            SavePng(RenderMark(p, 512), Path.Combine(logoDir, $"markone-mark{p.Suffix}.png"));
            SavePng(RenderPreview(p), Path.Combine(previewDir, $"vorschau{p.Suffix}.png"));
        }

        Write(Path.Combine(logoDir, "markone-logo.svg"), LogoSvg(Tinte));
        SavePng(RenderLogo(Tinte, 2.0), Path.Combine(logoDir, "markone-logo.png"));

        Console.WriteLine($"Logo nach {logoDir}");
        Console.WriteLine($"Symbole nach {iconDir}");
        Console.WriteLine($"Vorschau nach {previewDir}");
        return 0;
    }

    // ============================================================ Geometrie

    static double SlantX(double y) => Slant * (Mid - y);

    /// <summary>Vier Parallelogramme, die zusammen die Raute ergeben.</summary>
    static IEnumerable<Point[]> HashParts()
    {
        double h = W / 2;
        yield return new[] { new Point(Lo, A - h), new Point(Hi, A - h), new Point(Hi, A + h), new Point(Lo, A + h) };
        yield return new[] { new Point(Lo, B - h), new Point(Hi, B - h), new Point(Hi, B + h), new Point(Lo, B + h) };
        foreach (double cx in new[] { A, B })
            yield return new[]
            {
                new Point(cx + SlantX(Lo) - h, Lo), new Point(cx + SlantX(Lo) + h, Lo),
                new Point(cx + SlantX(Hi) + h, Hi), new Point(cx + SlantX(Hi) - h, Hi),
            };
    }

    /// <summary>Die eine gefüllte Kammer. Leicht vergrößert, damit sie unter den Strichen liegt.</summary>
    static Point[] Cell()
    {
        double h = W / 2, o = 3;
        double top = A + h - o, bottom = B - h + o;
        return new[]
        {
            new Point(A + h + SlantX(top) - o, top),       new Point(B - h + SlantX(top) + o, top),
            new Point(B - h + SlantX(bottom) + o, bottom), new Point(A + h + SlantX(bottom) - o, bottom),
        };
    }

    static Geometry Poly(IEnumerable<Point[]> polys)
    {
        var g = new StreamGeometry { FillRule = FillRule.Nonzero };
        using (var c = g.Open())
        {
            foreach (var pts in polys)
            {
                c.BeginFigure(pts[0], true, true);
                c.PolyLineTo(pts[1..], true, true);
            }
        }
        g.Freeze();
        return g;
    }

    // ============================================================== Rendern

    static SolidColorBrush Brush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        b.Freeze();
        return b;
    }

    static void DrawMark(DrawingContext dc, Palette p)
    {
        if (p.TileBorder is null)
            dc.DrawRoundedRectangle(Brush(p.Tile), null, new Rect(0, 0, S, S), Radius, Radius);
        else
            dc.DrawRoundedRectangle(Brush(p.Tile), new Pen(Brush(p.TileBorder), 2),
                new Rect(1, 1, S - 2, S - 2), Radius - 1, Radius - 1);

        dc.DrawGeometry(Brush(p.Accent), null, Poly(new[] { Cell() }));
        dc.DrawGeometry(Brush(p.Stroke), null, Poly(HashParts()));
    }

    static RenderTargetBitmap RenderMark(Palette p, int px)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(px / S, px / S));
            DrawMark(dc, p);
            dc.Pop();
        }
        return Render(visual, px, px);
    }

    static RenderTargetBitmap Render(Visual visual, int w, int h)
    {
        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    // ------------------------------------------------------------ Wortmarke

    static FormattedText Word(string text, double size, string color, string family = WordFont) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily(family), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            size, Brush(color), 1.0);

    /// <summary>Layout der Wort-Bild-Marke im 256er-Raster: Kachel, Lücke, „Mark", „One".</summary>
    static (Geometry mark, Geometry one, double width) WordLayout()
    {
        double baseline = Hi;
        var mark = Word("Mark", WordSize, WordInk);
        var one = Word("One", WordSize, WordAccent);
        double x = S + WordGap;
        double xOne = x + mark.WidthIncludingTrailingWhitespace;
        var gMark = mark.BuildGeometry(new Point(x, baseline - mark.Baseline));
        var gOne = one.BuildGeometry(new Point(xOne, baseline - one.Baseline));
        double width = xOne + one.WidthIncludingTrailingWhitespace;
        return (gMark, gOne, width);
    }

    static RenderTargetBitmap RenderLogo(Palette p, double scale)
    {
        var (gMark, gOne, width) = WordLayout();
        double pad = 8;
        int w = (int)Math.Ceiling((width + pad) * scale);
        int h = (int)Math.Ceiling(S * scale);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(scale, scale));
            DrawMark(dc, p);
            dc.DrawGeometry(Brush(WordInk), null, gMark);
            dc.DrawGeometry(Brush(WordAccent), null, gOne);
            dc.Pop();
        }
        return Render(visual, w, h);
    }

    // -------------------------------------------------------------- Vorschau

    /// <summary>Zeigt, wie das Zeichen in den Größen wirkt, die Windows tatsächlich benutzt.</summary>
    static RenderTargetBitmap RenderPreview(Palette p)
    {
        int[] sizes = { 48, 32, 24, 20, 16 };
        int w = 760, h = 372;

        var visual = new DrawingVisual();
        TextOptions.SetTextRenderingMode(visual, TextRenderingMode.Grayscale);
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brush("#FFFFFF"), null, new Rect(0, 0, w, h));
            dc.DrawImage(RenderMark(p, 256), new Rect(24, 24, 256, 256));

            DrawRow(dc, p, sizes, "#F3F3F3", "#6B6B6B", 320, 24, "Taskleiste, helles Design");
            DrawRow(dc, p, sizes, "#202020", "#9A9A9A", 320, 154, "Taskleiste, dunkles Design");

            // Titelleiste von MarkOne nachgestellt: Symbol 16 px neben dem Fenstertitel.
            var bar = new Rect(320, 284, 416, 36);
            dc.DrawRectangle(Brush("#F1EEE8"), new Pen(Brush("#E3DED4"), 1), bar);
            dc.DrawImage(RenderMark(p, 16), new Rect(332, 294, 16, 16));
            dc.DrawText(Word("beispiel.md  —  MarkOne", 12, "#33322E", "Segoe UI"), new Point(358, 293));

            dc.DrawText(Word("Titelleiste", 11, "#8A857B", "Segoe UI"), new Point(320, 328));
            string name = p.Suffix.Length == 0 ? "Variante „Tinte“" : "Variante „Papier“";
            dc.DrawText(Word(name, 13, "#33322E", "Segoe UI"), new Point(24, 300));
        }
        return Render(visual, w, h);
    }

    static void DrawRow(DrawingContext dc, Palette p, int[] sizes, string bg, string fg, double x, double y, string label)
    {
        dc.DrawRectangle(Brush(bg), null, new Rect(x, y, 416, 100));
        double cx = x + 20;
        foreach (int s in sizes)
        {
            dc.DrawImage(RenderMark(p, s), new Rect(cx, y + 20 + (48 - s) / 2, s, s));
            dc.DrawText(Word(s.ToString(CultureInfo.InvariantCulture), 10, fg, "Segoe UI"),
                new Point(cx + (s - 12) / 2, y + 76));
            cx += s + 34;
        }
        dc.DrawText(Word(label, 11, fg, "Segoe UI"), new Point(x + 20, y + 4));
    }

    // ================================================================= SVG

    static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    static string P(Point p) => F(p.X) + " " + F(p.Y);

    static string PathData(IEnumerable<Point[]> polys)
    {
        var sb = new StringBuilder();
        foreach (var pts in polys)
        {
            sb.Append('M').Append(P(pts[0]));
            for (int i = 1; i < pts.Length; i++) sb.Append('L').Append(P(pts[i]));
            sb.Append('Z');
        }
        return sb.ToString();
    }

    static string TileSvg(Palette p) =>
        p.TileBorder is null
            ? $"  <rect width=\"256\" height=\"256\" rx=\"{F(Radius)}\" fill=\"{p.Tile}\"/>"
            : $"  <rect x=\"1\" y=\"1\" width=\"254\" height=\"254\" rx=\"{F(Radius - 1)}\" fill=\"{p.Tile}\" stroke=\"{p.TileBorder}\" stroke-width=\"2\"/>";

    static string MarkBody(Palette p) =>
        TileSvg(p) + "\n" +
        $"  <path fill=\"{p.Accent}\" d=\"{PathData(new[] { Cell() })}\"/>\n" +
        $"  <path fill=\"{p.Stroke}\" d=\"{PathData(HashParts())}\"/>";

    static string MarkSvg(Palette p) =>
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 256 256\" width=\"256\" height=\"256\">\n" +
        "  <title>MarkOne</title>\n" +
        MarkBody(p) + "\n</svg>\n";

    /// <summary>Wort-Bild-Marke. Die Buchstaben sind als Pfade eingebettet, damit die Datei ohne Schrift auskommt.</summary>
    static string LogoSvg(Palette p)
    {
        var (gMark, gOne, width) = WordLayout();
        double w = Math.Ceiling(width + 8);
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {F(w)} 256\" width=\"{F(w)}\" height=\"256\">\n");
        sb.Append("  <title>MarkOne</title>\n");
        sb.Append(MarkBody(p)).Append('\n');
        sb.Append($"  <path fill=\"{WordInk}\" d=\"").Append(GeometrySvg(gMark)).Append("\"/>\n");
        sb.Append($"  <path fill=\"{WordAccent}\" d=\"").Append(GeometrySvg(gOne)).Append("\"/>\n");
        sb.Append("</svg>\n");
        return sb.ToString();
    }

    static string GeometrySvg(Geometry g)
    {
        var sb = new StringBuilder();
        AppendGeometry(g, sb);
        return sb.ToString();
    }

    static void AppendGeometry(Geometry g, StringBuilder sb)
    {
        switch (g)
        {
            case GeometryGroup group:
                foreach (var child in group.Children) AppendGeometry(child, sb);
                break;
            case PathGeometry path:
                foreach (var figure in path.Figures) AppendFigure(figure, sb);
                break;
            default:
                foreach (var figure in PathGeometry.CreateFromGeometry(g).Figures) AppendFigure(figure, sb);
                break;
        }
    }

    static void AppendFigure(PathFigure figure, StringBuilder sb)
    {
        sb.Append('M').Append(P(figure.StartPoint));
        foreach (var segment in figure.Segments)
        {
            switch (segment)
            {
                case LineSegment l:
                    sb.Append('L').Append(P(l.Point));
                    break;
                case PolyLineSegment pl:
                    foreach (var pt in pl.Points) sb.Append('L').Append(P(pt));
                    break;
                case BezierSegment b:
                    sb.Append('C').Append(P(b.Point1)).Append(' ').Append(P(b.Point2)).Append(' ').Append(P(b.Point3));
                    break;
                case PolyBezierSegment pb:
                    for (int i = 0; i + 2 < pb.Points.Count; i += 3)
                        sb.Append('C').Append(P(pb.Points[i])).Append(' ').Append(P(pb.Points[i + 1])).Append(' ').Append(P(pb.Points[i + 2]));
                    break;
                case QuadraticBezierSegment q:
                    sb.Append('Q').Append(P(q.Point1)).Append(' ').Append(P(q.Point2));
                    break;
                case PolyQuadraticBezierSegment pq:
                    for (int i = 0; i + 1 < pq.Points.Count; i += 2)
                        sb.Append('Q').Append(P(pq.Points[i])).Append(' ').Append(P(pq.Points[i + 1]));
                    break;
                default:
                    throw new NotSupportedException($"Segmenttyp {segment.GetType().Name} wird nicht exportiert.");
            }
        }
        if (figure.IsClosed) sb.Append('Z');
    }

    // ================================================================= ICO

    /// <summary>
    /// Symboldatei mit den Größen, die Windows für Titelleiste, Taskleiste,
    /// Alt+Tab und Explorer heranzieht. Kleine Größen als unkomprimierte
    /// Bitmaps (das verstehen alle Windows-Bestandteile), große als PNG.
    /// </summary>
    static void WriteIco(string path, Palette p)
    {
        int[] plain = { 16, 20, 24, 32, 48 };
        int[] png = { 64, 256 };

        var images = new List<(int size, byte[] data)>();
        foreach (int s in plain) images.Add((s, Dib(RenderMark(p, s))));
        foreach (int s in png) images.Add((s, PngBytes(RenderMark(p, s))));

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)images.Count);
        int offset = 6 + 16 * images.Count;
        foreach (var (size, data) in images)
        {
            byte dim = (byte)(size >= 256 ? 0 : size);
            w.Write(dim); w.Write(dim); w.Write((byte)0); w.Write((byte)0);
            w.Write((ushort)1); w.Write((ushort)32);
            w.Write(data.Length); w.Write(offset);
            offset += data.Length;
        }
        foreach (var (_, data) in images) w.Write(data);
        w.Flush();
        File.WriteAllBytes(path, ms.ToArray());
    }

    static byte[] Dib(BitmapSource source)
    {
        var bmp = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int w = bmp.PixelWidth, h = bmp.PixelHeight, stride = w * 4;
        var pixels = new byte[stride * h];
        bmp.CopyPixels(pixels, stride, 0);

        int maskStride = ((w + 31) / 32) * 4;
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(40); bw.Write(w); bw.Write(h * 2); bw.Write((ushort)1); bw.Write((ushort)32);
        bw.Write(0); bw.Write(stride * h + maskStride * h); bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);

        for (int y = h - 1; y >= 0; y--) bw.Write(pixels, y * stride, stride);

        var maskRow = new byte[maskStride];
        for (int y = h - 1; y >= 0; y--)
        {
            Array.Clear(maskRow);
            for (int x = 0; x < w; x++)
                if (pixels[y * stride + x * 4 + 3] == 0) maskRow[x / 8] |= (byte)(0x80 >> (x % 8));
            bw.Write(maskRow);
        }
        bw.Flush();
        return ms.ToArray();
    }

    // ============================================================== Dateien

    static byte[] PngBytes(BitmapSource bmp)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    static void SavePng(BitmapSource bmp, string path) => File.WriteAllBytes(path, PngBytes(bmp));

    static void Write(string path, string text) => File.WriteAllText(path, text, Utf8);
}
