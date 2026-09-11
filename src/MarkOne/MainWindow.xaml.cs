using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MarkOne;

public partial class MainWindow : Window
{
    public static readonly RoutedUICommand OpenFolderCommand =
        new("Basisverzeichnis öffnen", nameof(OpenFolderCommand), typeof(MainWindow));
    public static readonly RoutedUICommand SaveVersionCommand =
        new("Als neue Version sichern", nameof(SaveVersionCommand), typeof(MainWindow));
    public static readonly RoutedUICommand RefreshTreeCommand =
        new("Navigation aktualisieren", nameof(RefreshTreeCommand), typeof(MainWindow));

    private static readonly Regex RxWord = new(@"[\p{L}\p{N}'’\-]+", RegexOptions.Compiled);
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    private readonly Settings _settings;
    private readonly ObservableCollection<TreeNode> _roots = new();

    /// <summary>Basisverzeichnis von der Kommandozeile; gilt nur für diesen Start.</summary>
    public string? StartupFolder { get; set; }

    private string? _path;
    private bool _dirty;
    private bool _suppress;               // verhindert Rekursion beim Umformatieren
    private bool _suppressTreeSelection;  // verhindert Rückkopplung beim Zurücknehmen der Auswahl
    private FileNode? _currentNode;           // Knoten des Dokuments im Editor
    private Pane _pane = Pane.Editor;         // was rechts zu sehen ist
    private string? _viewPath;                // im Betrachter gezeigte Datei
    private TreeNode? _contextNode;           // Knoten unter dem Kontextmenü
    private FindReplaceWindow? _finder;

    private readonly DispatcherTimer _countTimer;
    private readonly DispatcherTimer _autoSaveTimer;
    private readonly DispatcherTimer _messageTimer;

    public MainWindow()
    {
        InitializeComponent();
        TitleBar.Attach(this);

        _settings = Settings.Load();
        Width = _settings.WindowWidth;
        Height = _settings.WindowHeight;
        TreeColumn.Width = new GridLength(_settings.TreeWidth);
        MenuAutoSave.IsChecked = _settings.AutoSave;
        MenuAllFiles.IsChecked = _settings.ShowAllFiles;
        FileScanner.ShowAllFiles = _settings.ShowAllFiles;

        Tree.ItemsSource = _roots;
        Images.StatusChanged += (_, _) => { if (_pane == Pane.Image) StatusCount.Text = Images.Status; };
        Pdfs.StatusChanged += (_, _) => { if (_pane == Pane.Pdf) StatusCount.Text = Pdfs.Status; };

        DataObject.AddPastingHandler(Editor, OnPaste);

        _countTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _countTimer.Tick += (_, _) => { _countTimer.Stop(); UpdateWordCount(); };

        // Absturzsicherung: kurz nach der letzten Eingabe, nicht bei jedem Zeichen.
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _autoSaveTimer.Tick += (_, _) => { _autoSaveTimer.Stop(); WriteRecovery(); };

        _messageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _messageTimer.Tick += (_, _) => { _messageTimer.Stop(); StatusMessage.Text = ""; };

        SetDocumentText(string.Empty);
        UpdateTitle();

        Loaded += OnWindowLoaded;
        Closing += OnClosing;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        Editor.Focus();

        if (StartupFolder is { } folder)
            SetBaseDirectory(folder, remember: false);
        else if (_settings.BaseDirectory is { } dir && Directory.Exists(dir))
            SetBaseDirectory(dir, remember: false);

        OfferRecovery();
    }

    // ================================================================ Text

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

    // ========================================================= Formatieren

    private static bool IsFence(string text)
    {
        var t = text.TrimStart();
        return t.StartsWith("```", StringComparison.Ordinal) || t.StartsWith("~~~", StringComparison.Ordinal);
    }

    private void RestyleAll()
    {
        var paragraphs = Editor.Document.Blocks.OfType<Paragraph>().ToList();
        if (paragraphs.Count == 0) return;

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

        if (_settings.AutoSave)
        {
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }
    }

    // ============================================================ Positionen

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

    // ================================================================ Layout

    private void ApplyColumnPadding()
    {
        double side = Math.Max(32, (Editor.ActualWidth - Theme.MaxColumnWidth) / 2);
        Editor.Document.PagePadding = new Thickness(side, 34, side, 60);
    }

    private void OnEditorSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) ApplyColumnPadding();
    }

    private void OnToggleTree(object sender, RoutedEventArgs e)
    {
        bool show = MenuShowTree.IsChecked;
        TreePanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        TreeSplitter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        TreeColumn.MinWidth = show ? 180 : 0;
        TreeColumn.Width = show ? new GridLength(_settings.TreeWidth) : new GridLength(0);
    }

    private void OnToggleAutoSave(object sender, RoutedEventArgs e)
    {
        _settings.AutoSave = MenuAutoSave.IsChecked;
        _settings.Save();
        if (!_settings.AutoSave)
        {
            _autoSaveTimer.Stop();
            Recovery.Clear(_path);
        }
        Flash(_settings.AutoSave ? "Autospeichern eingeschaltet" : "Autospeichern ausgeschaltet");
    }

    private void OnToggleAllFiles(object sender, RoutedEventArgs e)
    {
        _settings.ShowAllFiles = MenuAllFiles.IsChecked;
        FileScanner.ShowAllFiles = _settings.ShowAllFiles;
        _settings.Save();
        if (CurrentBaseDirectory is { } dir)
            SetBaseDirectory(dir, remember: false);
    }

    // ================================================================ Befehle

    /// <summary>Speichern, Suchen und Co. gibt es nur, solange der Editor zu sehen ist.</summary>
    private void CanEdit(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = IsEditing;

    private void CanSaveVersion(object sender, CanExecuteRoutedEventArgs e) =>
        e.CanExecute = IsEditing && _path is not null;

    /// <summary>Ob rechts gerade der Editor steht (und nicht ein Betrachter).</summary>
    private bool IsEditing => _pane == Pane.Editor;

    // ============================================================ Betrachter

    private enum Pane { Editor, Image, Pdf }

    /// <summary>Zurück zum Editor; das dort geladene Dokument bleibt, wie es war.</summary>
    private void ShowEditor()
    {
        if (_pane != Pane.Editor)
        {
            _pane = Pane.Editor;
            _viewPath = null;
            Images.Visibility = Visibility.Collapsed;
            Pdfs.Visibility = Visibility.Collapsed;
            Images.Clear();
            Pdfs.Clear();
            Editor.Visibility = Visibility.Visible;
            UpdateTitle();
            UpdateWordCount();
            CommandManager.InvalidateRequerySuggested();
        }
        Editor.Focus();
    }

    private void ShowImage(string path) => ShowViewer(Pane.Image, path);
    private void ShowPdf(string path) => ShowViewer(Pane.Pdf, path);

    private void ShowViewer(Pane pane, string path)
    {
        _pane = pane;
        _viewPath = path;
        Editor.Visibility = Visibility.Collapsed;
        Images.Visibility = pane == Pane.Image ? Visibility.Visible : Visibility.Collapsed;
        Pdfs.Visibility = pane == Pane.Pdf ? Visibility.Visible : Visibility.Collapsed;

        if (pane == Pane.Image)
        {
            Pdfs.Clear();
            Images.Load(path);
            Images.Focus();
        }
        else
        {
            Images.Clear();
            Pdfs.Load(path);
            Pdfs.Focus();
        }

        UpdateTitle();
        CommandManager.InvalidateRequerySuggested();
    }

    // ================================================================ Eingabe

    private void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            e.Handled = true;
            EditingCommands.EnterParagraphBreak.Execute(null, Editor);
            return;
        }

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

    // =========================================================== Statuszeile

    private void UpdateWordCount()
    {
        if (_pane != Pane.Editor) return;
        int words = RxWord.Matches(GetDocumentText()).Count;
        StatusCount.Text = words == 1 ? "1 Wort" : $"{words:N0} Wörter";
    }

    private void UpdateTitle()
    {
        if (_viewPath is not null)
        {
            Title = Path.GetFileName(_viewPath) + "  —  MarkOne";
            StatusFile.Text = _viewPath;
            return;
        }

        string name = _path is null ? "Unbenannt" : Path.GetFileName(_path);
        Title = (_dirty ? "• " : "") + name + "  —  MarkOne";
        StatusFile.Text = _path ?? "Unbenannt";
    }

    private void Flash(string message) => ShowMessage(message, Theme.Success, 4);

    /// <summary>Hinweis ohne Erfolgsgrün, bleibt etwas länger stehen.</summary>
    private void Info(string message) => ShowMessage(message, Theme.Muted, 7);

    private void ShowMessage(string message, Brush color, int seconds)
    {
        StatusMessage.Foreground = color;
        StatusMessage.Text = message;
        _messageTimer.Stop();
        _messageTimer.Interval = TimeSpan.FromSeconds(seconds);
        _messageTimer.Start();
    }

    // ========================================================== Navigation

    private void OnOpenFolder(object sender, ExecutedRoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Basisverzeichnis wählen",
            InitialDirectory = _settings.BaseDirectory ?? "",
        };
        if (dialog.ShowDialog(this) == true)
            SetBaseDirectory(dialog.FolderName, remember: true);
    }

    private void SetBaseDirectory(string directory, bool remember)
    {
        _roots.Clear();
        var root = new FolderNode(directory);
        _roots.Add(root);
        TreeHeader.Text = directory;

        root.IsExpanded = true;   // löst das Einlesen im Hintergrund aus

        if (remember)
        {
            _settings.BaseDirectory = directory;
            _settings.Save();
        }
    }

    /// <summary>Das Verzeichnis, das der Baum gerade zeigt.</summary>
    private string? CurrentBaseDirectory =>
        _roots.Count > 0 && Directory.Exists(_roots[0].FullPath) ? _roots[0].FullPath : null;

    private void OnRefreshTree(object sender, ExecutedRoutedEventArgs e)
    {
        if (CurrentBaseDirectory is { } dir)
        {
            SetBaseDirectory(dir, remember: false);
            Flash("Navigation aktualisiert");
        }
    }

    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_suppressTreeSelection) return;
        if (e.NewValue is not FileNode node) return;

        switch (node.Kind)
        {
            case FileKind.Image:
                ShowImage(node.FullPath);
                return;
            case FileKind.Pdf:
                ShowPdf(node.FullPath);
                return;
            case FileKind.Other:
                DescribeFile(node);
                return;
        }

        // Bereits offene Datei erneut angeklickt: nur den Editor nach vorn holen.
        if (ReferenceEquals(node, _currentNode)
            || (_path is not null && string.Equals(node.FullPath, _path, StringComparison.OrdinalIgnoreCase)))
        {
            _currentNode = node;
            ShowEditor();
            return;
        }

        if (!ConfirmDiscard())
        {
            // Abgebrochen: Auswahl im Baum zurücknehmen.
            _suppressTreeSelection = true;
            node.IsSelected = false;
            if (_currentNode is not null) _currentNode.IsSelected = true;
            _suppressTreeSelection = false;
            return;
        }

        _currentNode = node;
        LoadFile(node.FullPath);
    }

    /// <summary>
    /// Hebt die Markierung im Baum auf — nötig, wenn eine Datei über das Menü
    /// geöffnet wurde, sonst reagiert ein erneuter Klick auf den alten Eintrag nicht.
    /// </summary>
    private void ClearTreeSelection()
    {
        DeselectTree();
        _currentNode = null;
    }

    /// <summary>Hebt nur die Markierung auf; das Dokument im Editor bleibt seinem Knoten zugeordnet.</summary>
    private void DeselectTree()
    {
        _suppressTreeSelection = true;
        if (Tree.SelectedItem is TreeNode selected) selected.IsSelected = false;
        if (_currentNode is not null) _currentNode.IsSelected = false;
        _suppressTreeSelection = false;
    }

    /// <summary>Statuszeile für Dateien, die MarkOne nicht selbst öffnet.</summary>
    private void DescribeFile(FileNode node)
    {
        string size = "";
        try { size = Shell.FormatSize(new FileInfo(node.FullPath).Length) + "  ·  "; } catch { }
        string app = Shell.FriendlyAppName(node.FullPath) ?? "Standardprogramm";
        Info($"{node.Name}  ·  {size}Doppelklick öffnet in {app}");
    }

    private void OpenExternally(string path)
    {
        if (Shell.Open(path))
            Flash($"Geöffnet: {Path.GetFileName(path)}");
        else
            Info($"Windows kennt kein Programm für {Path.GetFileName(path)}");
    }

    /// <summary>Der Baumknoten, zu dem ein Element im Baum gehört (Klick, Doppelklick, Kontextmenü).</summary>
    private static TreeNode? NodeAt(object? source)
    {
        var d = source as DependencyObject;
        while (d is not null && d is not TreeViewItem)
            d = d is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(d)
                : LogicalTreeHelper.GetParent(d);
        return (d as TreeViewItem)?.DataContext as TreeNode;
    }

    private void OnTreeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (NodeAt(e.OriginalSource) is not FileNode { Kind: not FileKind.Markdown } node) return;
        OpenExternally(node.FullPath);
        e.Handled = true;
    }

    private void OnTreeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (Tree.SelectedItem is not FileNode { Kind: not FileKind.Markdown } node) return;
        OpenExternally(node.FullPath);
        e.Handled = true;
    }

    private void OnTreeContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        _contextNode = NodeAt(e.OriginalSource);
        if (_contextNode is null || _contextNode is LoadingNode)
        {
            e.Handled = true;   // kein Menü über leerer Fläche
            return;
        }
        MenuOpenExternal.Visibility = _contextNode is FileNode ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnOpenExternally(object sender, RoutedEventArgs e)
    {
        if (_contextNode is FileNode node) OpenExternally(node.FullPath);
    }

    private void OnRevealInExplorer(object sender, RoutedEventArgs e)
    {
        if (_contextNode is { } node) Shell.Reveal(node.FullPath);
    }

    private void OnCopyPath(object sender, RoutedEventArgs e)
    {
        if (_contextNode is not { } node) return;
        try
        {
            Clipboard.SetText(node.FullPath);
            Flash("Pfad kopiert");
        }
        catch
        {
            Info("Zwischenablage ist gerade belegt");
        }
    }

    /// <summary>Aktualisiert die im Baum angezeigte Überschrift nach dem Speichern.</summary>
    private void RefreshNodeHeading()
    {
        if (_currentNode is null || _path is null) return;
        FileScanner.Forget(_path);
        _currentNode.Heading = FileScanner.ReadHeading(_path);
    }

    // ======================================================= Absturzsicherung

    private void WriteRecovery()
    {
        if (!_settings.AutoSave || !_dirty) return;
        Recovery.Write(_path, GetDocumentText());
        Flash("Arbeitskopie gesichert  " + DateTime.Now.ToString("HH:mm:ss"));
    }

    private void OfferRecovery()
    {
        var entries = Recovery.List();
        if (entries.Count == 0) return;

        var dialog = new RecoveryWindow(entries) { Owner = this };
        dialog.ShowDialog();

        if (dialog.Restored is { } entry)
        {
            ShowEditor();
            SetDocumentText(entry.Content);
            _path = entry.OriginalPath;
            _dirty = true;                 // bewusst: die Fassung steht noch nicht in der Datei
            UpdateTitle();
            Flash("Wiederhergestellt — noch nicht gespeichert");
        }
    }

    // =============================================================== Dateien

    /// <summary>Öffnet eine Datei so, wie es zu ihr passt: Editor oder Betrachter.</summary>
    public void LoadFile(string path)
    {
        switch (FileScanner.KindOf(path))
        {
            case FileKind.Image:
                ShowImage(path);
                return;
            case FileKind.Pdf:
                ShowPdf(path);
                return;
            default:
                LoadDocument(path);
                return;
        }
    }

    private void LoadDocument(string path)
    {
        ShowEditor();
        try
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            SetDocumentText(text);
            _path = path;
            _dirty = false;
            _autoSaveTimer.Stop();
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
            Recovery.Clear(_path);
            _path = path;
            _dirty = false;
            _autoSaveTimer.Stop();
            Recovery.Clear(_path);
            UpdateTitle();
            RefreshNodeHeading();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Datei konnte nicht gespeichert werden:\n\n{ex.Message}",
                "MarkOne", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private bool ConfirmDiscard()
    {
        if (!_dirty) return true;

        string name = _path is null ? "Das neue Dokument" : Path.GetFileName(_path);
        var answer = MessageBox.Show(this,
            $"{name} wurde geändert und noch nicht gespeichert. Jetzt speichern?",
            "MarkOne", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        return answer switch
        {
            MessageBoxResult.Yes => Save(),
            MessageBoxResult.No => DiscardChanges(),
            _ => false,
        };
    }

    private bool DiscardChanges()
    {
        Recovery.Clear(_path);
        return true;
    }

    private bool Save()
    {
        if (_path is null) return SaveAs();
        if (!SaveTo(_path)) return false;
        Flash("Gespeichert");
        return true;
    }

    private bool SaveAs()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Markdown (*.md;*.markdown;*.txt)|*.md;*.markdown;*.txt|Alle Dateien (*.*)|*.*",
            DefaultExt = ".md",
            FileName = _path is null ? "Unbenannt.md" : Path.GetFileName(_path),
        };
        if (dialog.ShowDialog(this) != true) return false;
        if (!SaveTo(dialog.FileName)) return false;
        Flash("Gespeichert");
        return true;
    }

    private void OnSaveVersion(object sender, ExecutedRoutedEventArgs e)
    {
        if (_path is null) return;   // Befehl ist ohne Pfad ohnehin abgeschaltet

        try
        {
            // Erst die Datei selbst aktuell halten, dann den Zwischenstand ablegen.
            if (_dirty && !SaveTo(_path)) return;

            string created = Versioning.Save(_path, GetDocumentText());
            Flash($"Version abgelegt: {Path.GetFileName(created)}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Version konnte nicht abgelegt werden:\n\n{ex.Message}",
                "MarkOne", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnNew(object sender, ExecutedRoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        ShowEditor();
        SetDocumentText(string.Empty);
        _path = null;
        ClearTreeSelection();
        _dirty = false;
        UpdateTitle();
    }

    private void OnOpen(object sender, ExecutedRoutedEventArgs e)
    {
        string images = string.Join(";", FileScanner.ImageExtensions.Select(x => "*" + x));
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = $"Alles, was MarkOne zeigt|*.md;*.markdown;*.txt;*.pdf;{images}"
                   + "|Markdown (*.md;*.markdown;*.txt)|*.md;*.markdown;*.txt"
                   + "|PDF (*.pdf)|*.pdf"
                   + $"|Bilder|{images}"
                   + "|Alle Dateien (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != true) return;

        string path = dialog.FileName;
        var kind = FileScanner.KindOf(path);
        if (kind is FileKind.Image or FileKind.Pdf)
        {
            DeselectTree();
            ShowViewer(kind == FileKind.Image ? Pane.Image : Pane.Pdf, path);
            return;
        }

        if (!ConfirmDiscard()) return;
        ClearTreeSelection();
        LoadDocument(path);
    }

    private void OnSave(object sender, ExecutedRoutedEventArgs e) => Save();

    private void OnSaveAs(object sender, ExecutedRoutedEventArgs e) => SaveAs();

    private void OnExit(object sender, RoutedEventArgs e) => Close();

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!ConfirmDiscard())
        {
            e.Cancel = true;
            return;
        }

        Recovery.Clear(_path);

        if (WindowState == WindowState.Normal)
        {
            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
        }
        if (TreeColumn.Width.Value > 0) _settings.TreeWidth = TreeColumn.Width.Value;
        _settings.Save();
    }

    // ======================================================= Suchen/Ersetzen

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
