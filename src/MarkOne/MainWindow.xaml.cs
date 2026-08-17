using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

namespace MarkOne;

public partial class MainWindow : Window
{
    private static readonly Regex RxWord = new(@"[\p{L}\p{N}'’\-]+", RegexOptions.Compiled);
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    private string? _path;
    private bool _dirty;
    private bool _suppress;          // verhindert Rekursion beim Umformatieren
    private FindReplaceWindow? _finder;
    private readonly DispatcherTimer _countTimer;

    public MainWindow()
    {
        InitializeComponent();

        DataObject.AddPastingHandler(Editor, OnPaste);

        _countTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _countTimer.Tick += (_, _) => { _countTimer.Stop(); UpdateWordCount(); };

        SetDocumentText(string.Empty);
        UpdateTitle();
        Loaded += (_, _) => Editor.Focus();
        Closing += OnClosing;
    }

    // ---------------------------------------------------------------- Text

    private void SetDocumentText(string text)
    {
        _suppress = true;
        try
        {
            var doc = new FlowDocument
            {
                FontFamily = Theme.Body,
                FontSize = Theme.BaseSize,
                Foreground = Theme.Text,
                PagePadding = new Thickness(40, 34, 40, 60),
            };

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (var line in lines)
                doc.Blocks.Add(new Paragraph(new Run(line)));

            if (doc.Blocks.Count == 0)
                doc.Blocks.Add(new Paragraph(new Run(string.Empty)));

            Editor.Document = doc;
            ApplyColumnPadding();
            RestyleAll();
            Editor.CaretPosition = Editor.Document.ContentStart;
        }
        finally
        {
            _suppress = false;
        }

        UpdateWordCount();
    }

    private string GetDocumentText() =>
        string.Join(Environment.NewLine,
            Editor.Document.Blocks.OfType<Paragraph>().Select(MarkdownStyler.GetText));

    // ----------------------------------------------------------- Formatieren

    private static bool IsFence(string text)
    {
        var t = text.TrimStart();
        return t.StartsWith("```", StringComparison.Ordinal) || t.StartsWith("~~~", StringComparison.Ordinal);
    }

    private void RestyleAll()
    {
        var paragraphs = Editor.Document.Blocks.OfType<Paragraph>().ToList();
        if (paragraphs.Count == 0) return;

        // Cursorposition merken, damit sie das Umformatieren übersteht.
        int caretIndex = -1, caretOffset = 0;
        if (Editor.CaretPosition?.Paragraph is { } cp)
        {
            caretIndex = paragraphs.IndexOf(cp);
            caretOffset = OffsetInParagraph(cp, Editor.CaretPosition);
        }

        bool inCode = false;
        foreach (var p in paragraphs)
        {
            string text = MarkdownStyler.GetText(p);
            bool fence = IsFence(text);
            MarkdownStyler.Apply(p, inCode);
            if (fence) inCode = !inCode;
        }

        if (caretIndex >= 0 && caretIndex < paragraphs.Count)
            Editor.CaretPosition = PointerAtOffset(paragraphs[caretIndex], caretOffset);
    }

    private void RestyleCaretParagraph()
    {
        if (Editor.CaretPosition?.Paragraph is not { } p)
        {
            RestyleAll();
            return;
        }

        string text = MarkdownStyler.GetText(p);
        var info = p.Tag as LineInfo;

        // Eine ```-Zeile verschiebt die Blockgrenzen — dann muss alles neu.
        if (IsFence(text) || (info?.IsFence ?? false))
        {
            RestyleAll();
            return;
        }

        int offset = OffsetInParagraph(p, Editor.CaretPosition);
        MarkdownStyler.Apply(p, info?.InCodeBlock ?? false);
        Editor.CaretPosition = PointerAtOffset(p, offset);
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;

        _dirty = true;
        UpdateTitle();

        bool bulk = e.Changes.Any(c => c.AddedLength > 2 || c.RemovedLength > 2);

        _suppress = true;
        try
        {
            if (bulk) RestyleAll();
            else RestyleCaretParagraph();
        }
        finally
        {
            _suppress = false;
        }

        _countTimer.Stop();
        _countTimer.Start();
    }

    // ------------------------------------------------------------- Positionen

    private static int OffsetInParagraph(Paragraph p, TextPointer pointer) =>
        new TextRange(p.ContentStart, pointer).Text.Length;

    private static TextPointer PointerAtOffset(Paragraph p, int offset)
    {
        int acc = 0;
        foreach (var inline in p.Inlines)
        {
            if (inline is not Run run) continue;
            int len = run.Text.Length;
            if (offset <= acc + len)
                return run.ContentStart.GetPositionAtOffset(offset - acc) ?? p.ContentEnd;
            acc += len;
        }
        return p.ContentEnd;
    }

    // ---------------------------------------------------------------- Layout

    private void ApplyColumnPadding()
    {
        double side = Math.Max(32, (Editor.ActualWidth - Theme.MaxColumnWidth) / 2);
        Editor.Document.PagePadding = new Thickness(side, 34, side, 60);
    }

    private void OnEditorSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) ApplyColumnPadding();
    }

    // ---------------------------------------------------------------- Eingabe

    private void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Umschalt+Enter soll ebenfalls einen echten Absatz erzeugen,
        // sonst bricht das "eine Zeile = ein Absatz"-Modell.
        if (e.Key == Key.Return && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            e.Handled = true;
            EditingCommands.EnterParagraphBreak.Execute(null, Editor);
            return;
        }

        // Strg+B/I/U würden echte Rich-Text-Formatierung einfügen — hier unerwünscht.
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key is Key.B or Key.I or Key.U)
            e.Handled = true;
    }

    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            var text = (string)e.DataObject.GetData(DataFormats.UnicodeText)!;
            var plain = new DataObject();
            plain.SetData(DataFormats.UnicodeText, text);
            e.DataObject = plain;
            e.FormatToApply = DataFormats.UnicodeText;
        }
        else
        {
            e.CancelCommand();
        }
    }

    // ------------------------------------------------------------ Statuszeile

    private void UpdateWordCount()
    {
        int words = RxWord.Matches(GetDocumentText()).Count;
        StatusCount.Text = words == 1 ? "1 Wort" : $"{words:N0} Wörter";
    }

    private void UpdateTitle()
    {
        string name = _path is null ? "Unbenannt" : Path.GetFileName(_path);
        Title = (_dirty ? "• " : "") + name + "  —  MarkOne";
        StatusFile.Text = _path ?? "Unbenannt";
    }

    // ---------------------------------------------------------------- Dateien

    public void LoadFile(string path)
    {
        try
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            SetDocumentText(text);
            _path = path;
            _dirty = false;
            UpdateTitle();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Datei konnte nicht geöffnet werden:\n\n{ex.Message}",
                "MarkOne", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool SaveTo(string path)
    {
        try
        {
            File.WriteAllText(path, GetDocumentText(), Utf8NoBom);
            _path = path;
            _dirty = false;
            UpdateTitle();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Datei konnte nicht gespeichert werden:\n\n{ex.Message}",
                "MarkOne", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    /// <summary>Fragt bei ungesicherten Änderungen nach. false = Vorgang abbrechen.</summary>
    private bool ConfirmDiscard()
    {
        if (!_dirty) return true;

        var answer = MessageBox.Show(this,
            "Die Änderungen wurden noch nicht gespeichert. Jetzt speichern?",
            "MarkOne", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        return answer switch
        {
            MessageBoxResult.Yes => Save(),
            MessageBoxResult.No => true,
            _ => false,
        };
    }

    private bool Save() => _path is not null ? SaveTo(_path) : SaveAs();

    private bool SaveAs()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Markdown (*.md;*.markdown;*.txt)|*.md;*.markdown;*.txt|Alle Dateien (*.*)|*.*",
            DefaultExt = ".md",
            FileName = _path is null ? "Unbenannt.md" : Path.GetFileName(_path),
        };
        return dialog.ShowDialog(this) == true && SaveTo(dialog.FileName);
    }

    private void OnNew(object sender, ExecutedRoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        SetDocumentText(string.Empty);
        _path = null;
        _dirty = false;
        UpdateTitle();
    }

    private void OnOpen(object sender, ExecutedRoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Markdown (*.md;*.markdown;*.txt)|*.md;*.markdown;*.txt|Alle Dateien (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) == true) LoadFile(dialog.FileName);
    }

    private void OnSave(object sender, ExecutedRoutedEventArgs e) => Save();

    private void OnSaveAs(object sender, ExecutedRoutedEventArgs e) => SaveAs();

    private void OnExit(object sender, RoutedEventArgs e) => Close();

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!ConfirmDiscard()) e.Cancel = true;
    }

    // ------------------------------------------------------- Suchen/Ersetzen

    private void OnFind(object sender, ExecutedRoutedEventArgs e) => ShowFinder(replaceMode: false);

    private void OnReplace(object sender, ExecutedRoutedEventArgs e) => ShowFinder(replaceMode: true);

    private void ShowFinder(bool replaceMode)
    {
        _finder ??= new FindReplaceWindow(this) { Owner = this };
        _finder.Closed += (_, _) => _finder = null;
        _finder.Show();
        _finder.Activate();
        _finder.FocusInput(replaceMode);
    }

    public bool FindNext(string term, bool matchCase)
    {
        if (string.IsNullOrEmpty(term)) return false;

        var paragraphs = Editor.Document.Blocks.OfType<Paragraph>().ToList();
        if (paragraphs.Count == 0) return false;

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var from = Editor.Selection.IsEmpty ? Editor.CaretPosition : Editor.Selection.End;

        int startIndex = 0, startOffset = 0;
        if (from?.Paragraph is { } sp)
        {
            startIndex = Math.Max(0, paragraphs.IndexOf(sp));
            startOffset = OffsetInParagraph(sp, from);
        }

        // Ein Durchlauf plus eine Runde extra, damit die Suche am Ende umbricht.
        for (int step = 0; step <= paragraphs.Count; step++)
        {
            int index = (startIndex + step) % paragraphs.Count;
            string text = MarkdownStyler.GetText(paragraphs[index]);
            int begin = step == 0 ? startOffset : 0;
            if (begin > text.Length) continue;

            int hit = text.IndexOf(term, begin, comparison);
            if (hit < 0) continue;

            SelectRange(paragraphs[index], hit, term.Length);
            return true;
        }

        return false;
    }

    public bool ReplaceOne(string term, string replacement, bool matchCase)
    {
        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var selection = Editor.Selection;

        if (!selection.IsEmpty
            && selection.Text.Equals(term, comparison)
            && selection.Start.Paragraph is { } p)
        {
            int offset = OffsetInParagraph(p, selection.Start);
            string text = MarkdownStyler.GetText(p);
            if (offset + term.Length <= text.Length)
            {
                ReplaceParagraphText(p, text.Remove(offset, term.Length).Insert(offset, replacement));
                Editor.CaretPosition = PointerAtOffset(p, offset + replacement.Length);
            }
        }

        return FindNext(term, matchCase);
    }

    public int ReplaceAll(string term, string replacement, bool matchCase)
    {
        if (string.IsNullOrEmpty(term)) return 0;

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int total = 0;

        foreach (var p in Editor.Document.Blocks.OfType<Paragraph>().ToList())
        {
            string text = MarkdownStyler.GetText(p);
            int count = 0, index = 0;
            while ((index = text.IndexOf(term, index, comparison)) >= 0)
            {
                text = text.Remove(index, term.Length).Insert(index, replacement);
                index += replacement.Length;
                count++;
            }

            if (count > 0)
            {
                ReplaceParagraphText(p, text);
                total += count;
            }
        }

        if (total > 0)
        {
            _suppress = true;
            try { RestyleAll(); } finally { _suppress = false; }
            UpdateWordCount();
        }

        return total;
    }

    private void ReplaceParagraphText(Paragraph p, string text)
    {
        _suppress = true;
        try
        {
            bool inCode = (p.Tag as LineInfo)?.InCodeBlock ?? false;
            p.Inlines.Clear();
            p.Inlines.Add(new Run(text));
            MarkdownStyler.Apply(p, inCode);
            _dirty = true;
            UpdateTitle();
        }
        finally
        {
            _suppress = false;
        }
    }

    private void SelectRange(Paragraph p, int offset, int length)
    {
        var start = PointerAtOffset(p, offset);
        var end = PointerAtOffset(p, offset + length);
        Editor.Selection.Select(start, end);

        var rect = start.GetCharacterRect(LogicalDirection.Forward);
        if (rect != Rect.Empty && Editor.ActualHeight > 0)
        {
            double target = Editor.VerticalOffset + rect.Top - Editor.ActualHeight / 2;
            Editor.ScrollToVerticalOffset(Math.Max(0, target));
        }
    }
}
