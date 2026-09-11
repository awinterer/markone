using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MarkOne;

/// <summary>
/// PDF-Betrachter: alle Seiten untereinander, fortlaufend gescrollt.
/// Gezeichnet wird nur, was im Blick ist (plus eine Bildschirmhöhe
/// davor und danach); was weit weg scrollt, gibt seinen Speicher wieder frei.
/// Zoom mit Strg+Mausrad, Strg+0 passt die Seitenbreite ein, Strg+1 ist 100 %.
/// </summary>
public partial class PdfViewer : UserControl
{
    private const double PtToDip = 96.0 / 72.0;    // 100 % = ein Punkt ist 1/72 Zoll
    private const double Gap = 18;                 // Abstand zwischen Seiten
    private const double MaxFitWidth = 1100;       // eingepasst wird höchstens auf diese Breite
    private const double MinZoom = 0.1, MaxZoom = 6.0, Step = 1.2;

    public string Status { get; private set; } = "";
    public event EventHandler? StatusChanged;

    private PdfDocument? _doc;
    private Border[] _pages = Array.Empty<Border>();
    private int[] _renderedAt = Array.Empty<int>();   // Zoom-Generation, in der die Seite gezeichnet wurde
    private bool[] _pending = Array.Empty<bool>();
    private double[] _tops = Array.Empty<double>();   // Oberkante jeder Seite im Inhalt (DIP)

    private double _zoom = 1.0;
    private bool _fit = true;
    private int _generation;
    private int _loadId;
    private CancellationTokenSource _cts = new();
    private readonly DispatcherTimer _relayout;

    public PdfViewer()
    {
        InitializeComponent();
        _relayout = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _relayout.Tick += (_, _) => { _relayout.Stop(); if (_fit) ApplyFit(); };
        SizeChanged += (_, _) => { if (_fit && _doc is not null) { _relayout.Stop(); _relayout.Start(); } };
    }

    // ================================================================ Laden

    public async void Load(string path)
    {
        Clear();                 // zählt _loadId hoch, deshalb vor dem Merken der Kennung
        int id = ++_loadId;
        ShowMessage(null);
        SetStatus("wird gelesen …");

        try
        {
            var doc = await PdfDocument.OpenAsync(path);
            if (id != _loadId) { doc.Dispose(); return; }

            _doc = doc;
            BuildPages();
            _fit = true;
            ApplyFit();
            Scroller.ScrollToHome();
        }
        catch (Exception ex)
        {
            if (id != _loadId) return;
            ShowMessage("Das PDF konnte nicht geöffnet werden.\n\n" + ex.Message);
            SetStatus("");
        }
    }

    public void Clear()
    {
        _loadId++;
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        Pages.Children.Clear();
        _pages = Array.Empty<Border>();
        _renderedAt = Array.Empty<int>();
        _pending = Array.Empty<bool>();
        _tops = Array.Empty<double>();
        _doc?.Dispose();
        _doc = null;
        SetStatus("");
    }

    private void BuildPages()
    {
        if (_doc is null) return;
        int n = _doc.PageCount;
        _pages = new Border[n];
        _renderedAt = new int[n];
        _pending = new bool[n];
        _tops = new double[n];

        for (int i = 0; i < n; i++)
        {
            var image = new Image { Stretch = Stretch.Fill, SnapsToDevicePixels = true };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = Theme.PageBorder,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, Gap),
                SnapsToDevicePixels = true,
                Child = image,
            };
            _pages[i] = border;
            _renderedAt[i] = -1;
            Pages.Children.Add(border);
        }
    }

    // ================================================================= Zoom

    private double DpiScale => VisualTreeHelper.GetDpi(this).DpiScaleX;

    /// <summary>Breiteste Seite füllt die Fläche, aber nicht über <see cref="MaxFitWidth"/> hinaus.</summary>
    private void ApplyFit()
    {
        if (_doc is null || _doc.PageCount == 0) return;
        double avail = Math.Min(Scroller.ActualWidth - 48 - 12, MaxFitWidth);   // Rand und Bildlaufleiste
        if (avail <= 0) return;

        double widest = 0;
        foreach (var s in _doc.PageSizes) widest = Math.Max(widest, s.Width);
        _zoom = Math.Clamp(avail / (widest * PtToDip), MinZoom, MaxZoom);
        _fit = true;
        Layout();
    }

    /// <summary>Seitenmaße setzen und neu zeichnen lassen. Alte Bilder bleiben skaliert stehen, bis die scharfen da sind.</summary>
    private void Layout()
    {
        if (_doc is null) return;
        _generation++;
        _cts.Cancel();
        _cts = new CancellationTokenSource();

        double y = 20;   // oberer Rand des StackPanels
        for (int i = 0; i < _pages.Length; i++)
        {
            var size = _doc.PageSizes[i];
            double w = Math.Round(size.Width * PtToDip * _zoom);
            double h = Math.Round(size.Height * PtToDip * _zoom);
            _pages[i].Width = w + 2;
            _pages[i].Height = h + 2;
            _tops[i] = y;
            y += h + 2 + Gap;
        }

        Scroller.UpdateLayout();
        UpdateVisiblePages();
    }

    private void ZoomTo(double newZoom, Point anchor)
    {
        if (_doc is null) return;
        newZoom = Math.Clamp(newZoom, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - _zoom) < 1e-9) return;

        // Der Inhalt unter dem Anker soll nach dem Zoom an derselben Stelle liegen.
        double ratioY = (Scroller.VerticalOffset + anchor.Y) / Math.Max(1, Scroller.ExtentHeight);
        double ratioX = (Scroller.HorizontalOffset + anchor.X) / Math.Max(1, Scroller.ExtentWidth);

        _zoom = newZoom;
        _fit = false;
        Layout();

        Scroller.ScrollToVerticalOffset(ratioY * Scroller.ExtentHeight - anchor.Y);
        Scroller.ScrollToHorizontalOffset(ratioX * Scroller.ExtentWidth - anchor.X);
    }

    private Point Center => new(Scroller.ViewportWidth / 2, Scroller.ViewportHeight / 2);

    public void ZoomIn() => ZoomTo(_zoom * Step, Center);
    public void ZoomOut() => ZoomTo(_zoom / Step, Center);
    public void ZoomActual() => ZoomTo(1.0, Center);
    public void ZoomFit() => ApplyFit();

    // ============================================================= Zeichnen

    private void UpdateVisiblePages()
    {
        if (_doc is null || _pages.Length == 0) return;

        double top = Scroller.VerticalOffset;
        double viewport = Scroller.ViewportHeight;
        double bottom = top + viewport;
        int current = -1;   // die erste Seite, die noch ins obere Drittel reicht

        for (int i = 0; i < _pages.Length; i++)
        {
            double pageTop = _tops[i], pageBottom = pageTop + _pages[i].Height;
            if (current < 0 && pageBottom > top + viewport / 3) current = i;

            bool near = pageBottom >= top - viewport && pageTop <= bottom + viewport;
            bool far = pageBottom < top - 2 * viewport || pageTop > bottom + 2 * viewport;

            if (near)
            {
                if (_renderedAt[i] != _generation && !_pending[i]) RequestRender(i, _generation);
            }
            else if (far && _renderedAt[i] != -1)
            {
                ((Image)_pages[i].Child).Source = null;
                _renderedAt[i] = -1;
            }
        }

        SetStatus($"Seite {Math.Max(0, current) + 1} von {_doc.PageCount}  ·  {Shell.FormatSize(_doc.FileSize)}  ·  {_zoom:P0}");
    }

    private async void RequestRender(int index, int generation)
    {
        if (_doc is null) return;
        var doc = _doc;
        var image = (Image)_pages[index].Child;
        double scale = DpiScale;
        int width = Math.Max(1, (int)Math.Round((_pages[index].Width - 2) * scale));
        int height = Math.Max(1, (int)Math.Round((_pages[index].Height - 2) * scale));

        _pending[index] = true;
        try
        {
            var bitmap = await doc.RenderAsync(index, width, height, _cts.Token);
            if (generation != _generation || !ReferenceEquals(doc, _doc)) return;   // inzwischen überholt
            image.Source = bitmap;
            _renderedAt[index] = generation;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            // Die Seite bleibt weiß; besser als ein Dialog mitten im Blättern.
        }
        finally
        {
            if (index < _pending.Length) _pending[index] = false;
            if (generation != _generation && ReferenceEquals(doc, _doc)) UpdateVisiblePages();
        }
    }

    // ============================================================== Eingabe

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange != 0 || e.ViewportHeightChange != 0) UpdateVisiblePages();
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 || _doc is null) return;
        e.Handled = true;
        ZoomTo(e.Delta > 0 ? _zoom * Step : _zoom / Step, e.GetPosition(Scroller));
    }

    private void OnClick(object sender, MouseButtonEventArgs e) => Focus();

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        switch (e.Key)
        {
            case Key.D0 or Key.NumPad0 when ctrl: ZoomFit(); break;
            case Key.D1 or Key.NumPad1 when ctrl: ZoomActual(); break;
            case Key.OemPlus or Key.Add when ctrl: ZoomIn(); break;
            case Key.OemMinus or Key.Subtract when ctrl: ZoomOut(); break;
            case Key.PageDown or Key.Space: Scroller.PageDown(); break;
            case Key.PageUp: Scroller.PageUp(); break;
            case Key.Home when ctrl: Scroller.ScrollToHome(); break;
            case Key.End when ctrl: Scroller.ScrollToEnd(); break;
            case Key.Down: Scroller.LineDown(); break;
            case Key.Up: Scroller.LineUp(); break;
            default: return;
        }
        e.Handled = true;
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
