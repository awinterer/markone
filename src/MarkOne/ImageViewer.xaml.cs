using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MarkOne;

/// <summary>
/// Bildbetrachter auf Basis der Windows-Bilddecoder, die WPF mitbringt.
/// Zoom mit Strg+Mausrad um den Mauszeiger, Verschieben mit gedrückter
/// Maustaste, Doppelklick wechselt zwischen Einpassen und 100 %.
/// 100 % heißt: ein Bildpixel je Gerätepixel, auch auf skalierten Bildschirmen.
/// </summary>
public partial class ImageViewer : UserControl
{
    private const double MinZoom = 0.02;
    private const double MaxZoom = 32.0;
    private const double Step = 1.25;
    private const double Margin2 = 48;   // zweimal der Rand von 24 um das Bild

    /// <summary>Kurzbeschreibung für die Statuszeile: Maße, Dateigröße, Zoom.</summary>
    public string Status { get; private set; } = "";
    public event EventHandler? StatusChanged;

    private BitmapSource? _bitmap;
    private long _fileSize;
    private double _zoom = 1.0;
    private bool _fit = true;
    private int _loadId;

    private bool _dragging;
    private Point _dragOrigin;
    private double _dragH, _dragV;

    public ImageViewer()
    {
        InitializeComponent();
        SizeChanged += (_, _) => { if (_fit) ApplyFit(); };
    }

    // ================================================================ Laden

    public async void Load(string path)
    {
        int id = ++_loadId;
        _fit = true;
        Picture.Source = null;
        _bitmap = null;
        ShowMessage(null);

        try
        {
            _fileSize = new FileInfo(path).Length;
            var bitmap = await Task.Run(() => Decode(path));
            if (id != _loadId) return;   // inzwischen wurde eine andere Datei gewählt

            _bitmap = bitmap;
            Picture.Source = bitmap;
            ApplyFit();
            Scroller.ScrollToHome();
        }
        catch (Exception ex)
        {
            if (id != _loadId) return;
            ShowMessage("Das Bild konnte nicht gelesen werden.\n\n" + ex.Message);
            SetStatus("");
        }
    }

    /// <summary>Bild loslassen, damit der Speicher frei wird.</summary>
    public void Clear()
    {
        _loadId++;
        _bitmap = null;
        Picture.Source = null;
        ShowMessage(null);
        SetStatus("");
    }

    private static BitmapSource Decode(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        BitmapFrame frame = decoder.Frames[0];
        frame.Freeze();

        // Kameras speichern quer und vermerken die Drehung nur in den Metadaten.
        BitmapSource result = ReadOrientation(frame) switch
        {
            3 or 4 => new TransformedBitmap(frame, new RotateTransform(180)),
            6 or 5 => new TransformedBitmap(frame, new RotateTransform(90)),
            8 or 7 => new TransformedBitmap(frame, new RotateTransform(270)),
            _ => frame,
        };
        if (result.CanFreeze) result.Freeze();
        return result;
    }

    private static int ReadOrientation(BitmapFrame frame)
    {
        if (frame.Metadata is not BitmapMetadata metadata) return 1;
        foreach (var query in new[] { "System.Photo.Orientation", "/app1/ifd/{ushort=274}", "/ifd/{ushort=274}" })
        {
            try
            {
                if (metadata.ContainsQuery(query) && metadata.GetQuery(query) is { } value)
                    return Convert.ToInt32(value);
            }
            catch
            {
                // Nicht jedes Format kennt jede Abfrage; dann eben ungedreht.
            }
        }
        return 1;
    }

    // ================================================================= Zoom

    private double DpiScale => VisualTreeHelper.GetDpi(this).DpiScaleX;

    private void ApplyFit()
    {
        if (_bitmap is null) return;
        double availW = Scroller.ActualWidth - Margin2;
        double availH = Scroller.ActualHeight - Margin2;
        if (availW <= 0 || availH <= 0) return;

        double scale = DpiScale;
        double fit = Math.Min(availW / (_bitmap.PixelWidth / scale), availH / (_bitmap.PixelHeight / scale));
        _zoom = Math.Min(1.0, fit);   // kleine Bilder nicht aufblasen
        _fit = true;
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        if (_bitmap is null) return;
        double scale = DpiScale;
        Picture.Width = _bitmap.PixelWidth * _zoom / scale;
        Picture.Height = _bitmap.PixelHeight * _zoom / scale;
        SetStatus($"{_bitmap.PixelWidth} × {_bitmap.PixelHeight} Pixel  ·  {Shell.FormatSize(_fileSize)}  ·  {_zoom:P0}");
    }

    /// <summary>Zoomt so, dass der Bildpunkt unter <paramref name="anchor"/> (Scroller-Koordinaten) liegen bleibt.</summary>
    private void ZoomAt(double newZoom, Point anchor)
    {
        if (_bitmap is null) return;
        newZoom = Math.Clamp(newZoom, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - _zoom) < 1e-9) return;

        Point inImage = Scroller.TranslatePoint(anchor, Picture);
        double ratio = newZoom / _zoom;

        _zoom = newZoom;
        _fit = false;
        ApplyZoom();
        Scroller.UpdateLayout();

        Point now = Picture.TranslatePoint(new Point(inImage.X * ratio, inImage.Y * ratio), Scroller);
        Scroller.ScrollToHorizontalOffset(Scroller.HorizontalOffset + (now.X - anchor.X));
        Scroller.ScrollToVerticalOffset(Scroller.VerticalOffset + (now.Y - anchor.Y));
    }

    private Point Center => new(Scroller.ActualWidth / 2, Scroller.ActualHeight / 2);

    public void ZoomIn() => ZoomAt(_zoom * Step, Center);
    public void ZoomOut() => ZoomAt(_zoom / Step, Center);
    public void ZoomActual() => ZoomAt(1.0, Center);
    public void ZoomFit() => ApplyFit();

    // ============================================================== Eingabe

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 || _bitmap is null) return;
        e.Handled = true;
        ZoomAt(e.Delta > 0 ? _zoom * Step : _zoom / Step, e.GetPosition(Scroller));
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        switch (e.Key)
        {
            case Key.D0 or Key.NumPad0: ZoomFit(); break;
            case Key.D1 or Key.NumPad1: ZoomActual(); break;
            case Key.OemPlus or Key.Add: ZoomIn(); break;
            case Key.OemMinus or Key.Subtract: ZoomOut(); break;
            default: return;
        }
        e.Handled = true;
    }

    private void OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_bitmap is null) return;
        if (_fit && _zoom < 1.0) ZoomAt(1.0, e.GetPosition(Scroller));
        else ApplyFit();
    }

    private void OnDragStart(object sender, MouseButtonEventArgs e)
    {
        Focus();
        if (_bitmap is null) return;
        if (Scroller.ScrollableWidth <= 0 && Scroller.ScrollableHeight <= 0) return;

        _dragging = true;
        _dragOrigin = e.GetPosition(Scroller);
        _dragH = Scroller.HorizontalOffset;
        _dragV = Scroller.VerticalOffset;
        Scroller.Cursor = Cursors.SizeAll;
        Scroller.CaptureMouse();
    }

    private void OnDragMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var p = e.GetPosition(Scroller);
        Scroller.ScrollToHorizontalOffset(_dragH - (p.X - _dragOrigin.X));
        Scroller.ScrollToVerticalOffset(_dragV - (p.Y - _dragOrigin.Y));
    }

    private void OnDragEnd(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        Scroller.ReleaseMouseCapture();
        Scroller.Cursor = null;
    }

    private void OnLostCapture(object sender, MouseEventArgs e)
    {
        _dragging = false;
        Scroller.Cursor = null;
    }

    // =============================================================== Status

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
