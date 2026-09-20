using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace MarkOne;

/// <summary>Merkt sich pro Absatz, ob er innerhalb eines ```-Blocks liegt.</summary>
public sealed class LineInfo
{
    public bool InCodeBlock { get; set; }
    public bool IsFence { get; set; }
    public bool IsTable { get; set; }

    /// <summary>Die Zeile beginnt mit einem Strich, ob sie nun zu einer gültigen Tabelle gehört oder nicht.</summary>
    public bool LooksTable { get; set; }

    /// <summary>Erste Zeile des Blocks aus Strich-Zeilen. Erspart den Nachbarn die Suche nach oben.</summary>
    public Paragraph? TableFirst { get; set; }
}

public static class MarkdownStyler
{
    private static readonly Regex RxHeading = new(@"^(#{1,6})([ \t]+)(.*)$", RegexOptions.Compiled);
    private static readonly Regex RxFence = new(@"^\s*(```|~~~)", RegexOptions.Compiled);
    private static readonly Regex RxRule = new(@"^\s*([-*_])(\s*\1){2,}\s*$", RegexOptions.Compiled);
    private static readonly Regex RxQuote = new(@"^([ \t]*(?:>[ \t]?)+)(.*)$", RegexOptions.Compiled);
    private static readonly Regex RxList = new(@"^([ \t]*)([-*+]|\d{1,9}[.)])([ \t]+)(.*)$", RegexOptions.Compiled);

    // Tabellen: | a | b | mit einer Trennzeile |---|---| als zweiter Zeile.
    private static readonly Regex RxTableDelimiter = new(@"^\s{0,3}\|(\s*:?-+:?\s*\|)+\s*(:?-+:?\s*)?$", RegexOptions.Compiled);
    private static readonly Regex RxCellSplit = new(@"(?<!\\)\|", RegexOptions.Compiled);

    private enum TableRole { Header, Delimiter, Body }

    // Reihenfolge zählt: **fett** muss vor *kursiv* geprüft werden.
    private static readonly Regex RxInline = new(
        @"(?<code>`[^`\n]+`)" +
        @"|(?<link>\[[^\]\n]*\]\([^)\n]*\))" +
        @"|(?<bold>\*\*[^*\n]+\*\*|__[^_\n]+__)" +
        @"|(?<strike>~~[^~\n]+~~)" +
        @"|(?<italic>\*[^*\n]+\*|_[^_\n]+_)",
        RegexOptions.Compiled);

    /// <summary>Enthält die Zeile überhaupt Zeichen, die Formatierung auslösen könnten?</summary>
    public static bool HasAnyMarkup(string text) =>
        text.AsSpan().IndexOfAny("#*_`[~>-|".AsSpan()) >= 0 || text.Length > 0 && char.IsDigit(text[0]);

    // ============================================================== Tabellen

    /// <summary>Beginnt die Zeile mit einem senkrechten Strich und hat noch mindestens einen?</summary>
    public static bool LooksLikeTableRow(string text)
    {
        var t = text.AsSpan().TrimStart();
        return t.Length > 1 && t[0] == '|' && t[1..].IndexOf('|') >= 0;
    }

    private static bool IsTableLine(Block? block) =>
        block is Paragraph p && LooksLikeTableRow(GetText(p));

    /// <summary>
    /// Welche Rolle spielt der Absatz in seiner Tabelle? Null, wenn der Block keine
    /// gültige Tabelle ist, also die Trennzeile als zweite Zeile fehlt.
    /// </summary>
    private static TableRole? RoleOf(Paragraph paragraph, LineInfo info, out bool isLast)
    {
        isLast = !IsTableLine(paragraph.NextBlock);

        Paragraph first = FirstOfBlock(paragraph);
        info.TableFirst = first;

        if (first.NextBlock is not Paragraph second || !RxTableDelimiter.IsMatch(GetText(second)))
            return null;

        if (ReferenceEquals(paragraph, first)) return TableRole.Header;
        if (ReferenceEquals(paragraph, second)) return TableRole.Delimiter;
        return TableRole.Body;
    }

    /// <summary>
    /// Erste Zeile des zusammenhängenden Blocks. Die Zeile darüber hat sie sich gemerkt,
    /// sofern sie frisch formatiert ist; sonst wird nach oben gesucht.
    /// </summary>
    private static Paragraph FirstOfBlock(Paragraph paragraph)
    {
        if (!IsTableLine(paragraph.PreviousBlock)) return paragraph;

        if (paragraph.PreviousBlock is Paragraph { Tag: LineInfo { TableFirst: { Parent: not null } hint } }
            && LooksLikeTableRow(GetText(hint)) && !IsTableLine(hint.PreviousBlock))
            return hint;

        Paragraph first = paragraph;
        while (IsTableLine(first.PreviousBlock)) first = (Paragraph)first.PreviousBlock;
        return first;
    }

    /// <summary>
    /// Formatiert den Absatz unter dem Cursor neu. Die ganze Tabelle geht nur dann mit, wenn
    /// sich ihr Aufbau geändert haben kann: Die Zeile ist neu in der Tabelle, fällt heraus,
    /// oder sie ist Kopf- oder Trennzeile. Denn eine Zeile bekommt ihre Rolle von den
    /// Nachbarn; erst die Trennzeile macht aus der Zeile darüber eine Kopfzeile.
    /// </summary>
    public static void ApplyAtCaret(Paragraph paragraph)
    {
        var info = paragraph.Tag as LineInfo;
        bool inCode = info?.InCodeBlock ?? false;
        bool looks = !inCode && LooksLikeTableRow(GetText(paragraph));

        bool nearTop = looks && (!IsTableLine(paragraph.PreviousBlock) || !IsTableLine(paragraph.PreviousBlock?.PreviousBlock));
        if (looks != (info?.LooksTable ?? false) || nearTop)
            RestyleTableAround(paragraph);
        else
            Apply(paragraph, inCode);
    }

    /// <summary>Formatiert den Absatz und alle Strich-Zeilen darüber und darunter, von oben nach unten.</summary>
    public static void RestyleTableAround(Paragraph paragraph)
    {
        var rows = new List<Paragraph>();
        for (var p = paragraph.PreviousBlock as Paragraph; p is not null && LooksLikeTableRow(GetText(p)); p = p.PreviousBlock as Paragraph)
            rows.Add(p);
        rows.Reverse();
        rows.Add(paragraph);
        for (var p = paragraph.NextBlock as Paragraph; p is not null && LooksLikeTableRow(GetText(p)); p = p.NextBlock as Paragraph)
            rows.Add(p);

        foreach (var p in rows)
            Apply(p, (p.Tag as LineInfo)?.InCodeBlock ?? false);
    }

    private static void ApplyTableLook(Paragraph p, TableRole role, bool isLast)
    {
        p.FontSize = Theme.BaseSize - 1.5;
        p.LineHeight = 24;
        p.TextAlignment = TextAlignment.Left;

        // Umbrochene Zeilen rücken ein, damit der Zeilenanfang mit dem Strich erkennbar bleibt.
        p.Padding = new Thickness(20, 4, 10, 4);
        p.TextIndent = -14;
        p.BorderBrush = Theme.TableLine;
        p.Margin = new Thickness(0, 0, 0, isLast ? 14 : 0);

        switch (role)
        {
            case TableRole.Header:
                p.FontWeight = FontWeights.Bold;
                p.Foreground = Theme.Heading;
                p.Background = Theme.TableHeaderBg;
                p.BorderThickness = new Thickness(0, 1, 0, 0);
                p.Margin = new Thickness(0, 8, 0, 0);
                break;

            case TableRole.Delimiter:
                // Muss im Text stehen, soll aber nicht mitlesen: klein, blass, als Linie unter dem Kopf.
                p.FontSize = 7;
                p.LineHeight = 9;
                p.Foreground = Theme.Marker;
                p.Background = Theme.TableHeaderBg;
                p.Padding = new Thickness(20, 0, 10, 1);
                p.BorderBrush = Theme.Rule;
                p.BorderThickness = new Thickness(0, 0, 0, 1);
                break;

            default:
                p.BorderThickness = new Thickness(0, 0, 0, 1);
                break;
        }
    }

    /// <summary>Zellen einzeln auszeichnen, die Striche dazwischen treten zurück.</summary>
    private static IEnumerable<Inline> BuildTableRow(string text, Brush baseBrush)
    {
        var result = new List<Inline>();
        string[] cells = RxCellSplit.Split(text);
        for (int i = 0; i < cells.Length; i++)
        {
            if (i > 0) result.Add(Make("|", Theme.Marker, weight: FontWeights.Normal));
            if (cells[i].Length > 0) result.AddRange(BuildInline(cells[i], baseBrush));
        }
        return result;
    }

    public static string GetText(Paragraph paragraph)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var inline in paragraph.Inlines)
        {
            if (inline is Run run) sb.Append(run.Text);
            else if (inline is LineBreak) sb.Append(' ');
        }
        return sb.ToString();
    }

    /// <summary>Formatiert einen Absatz gemäß seinem Markdown-Inhalt neu.</summary>
    public static void Apply(Paragraph paragraph, bool inCodeBlock)
    {
        string text = GetText(paragraph);

        ResetParagraph(paragraph);
        var info = new LineInfo { InCodeBlock = inCodeBlock, IsFence = RxFence.IsMatch(text) };
        paragraph.Tag = info;

        var inlines = new List<Inline>();

        if (info.IsFence)
        {
            ApplyCodeBlockLook(paragraph);
            inlines.Add(Make(text, Theme.Muted));
        }
        else if (inCodeBlock)
        {
            ApplyCodeBlockLook(paragraph);
            inlines.Add(Make(text, Theme.Code));
        }
        else if ((info.LooksTable = LooksLikeTableRow(text)) && RoleOf(paragraph, info, out bool isLast) is { } role)
        {
            info.IsTable = true;
            ApplyTableLook(paragraph, role, isLast);
            if (role == TableRole.Delimiter)
                inlines.Add(Make(text, Theme.Marker));
            else
                inlines.AddRange(BuildTableRow(text, role == TableRole.Header ? Theme.Heading : Theme.Text));
        }
        else if (RxRule.IsMatch(text))
        {
            paragraph.Foreground = Theme.Rule;
            paragraph.Margin = new Thickness(0, 10, 0, 16);
            inlines.Add(Make(text, Theme.Rule));
        }
        else if (RxHeading.Match(text) is { Success: true } h)
        {
            int level = h.Groups[1].Value.Length;
            paragraph.FontSize = Theme.HeadingSize(level);
            paragraph.LineHeight = Theme.HeadingLineHeight(level);
            paragraph.FontWeight = FontWeights.Bold;
            paragraph.Foreground = Theme.Heading;
            paragraph.Margin = Theme.HeadingMargin(level);

            // Die Rauten bleiben lesbar, treten aber optisch zurück.
            inlines.Add(Make(h.Groups[1].Value + h.Groups[2].Value, Theme.Marker, weight: FontWeights.Normal));
            inlines.AddRange(BuildInline(h.Groups[3].Value, Theme.Heading));
        }
        else if (RxQuote.Match(text) is { Success: true } q)
        {
            paragraph.Foreground = Theme.Quote;
            paragraph.FontStyle = FontStyles.Italic;
            paragraph.BorderBrush = Theme.QuoteBar;
            paragraph.BorderThickness = new Thickness(3, 0, 0, 0);
            paragraph.Padding = new Thickness(14, 2, 0, 2);
            paragraph.Margin = new Thickness(2, 2, 0, 12);

            inlines.Add(Make(q.Groups[1].Value, Theme.Marker, style: FontStyles.Normal));
            inlines.AddRange(BuildInline(q.Groups[2].Value, Theme.Quote));
        }
        else if (RxList.Match(text) is { Success: true } l)
        {
            paragraph.Margin = new Thickness(0, 0, 0, 4);
            inlines.Add(Make(l.Groups[1].Value, Theme.Text));
            inlines.Add(Make(l.Groups[2].Value, Theme.Muted, weight: FontWeights.Bold));
            inlines.Add(Make(l.Groups[3].Value, Theme.Text));
            inlines.AddRange(BuildInline(l.Groups[4].Value, Theme.Text));
        }
        else
        {
            inlines.AddRange(BuildInline(text, Theme.Text));
        }

        paragraph.Inlines.Clear();
        paragraph.Inlines.AddRange(inlines);
    }

    private static void ResetParagraph(Paragraph p)
    {
        p.FontFamily = Theme.Body;
        p.FontSize = Theme.BaseSize;
        p.FontWeight = FontWeights.Normal;
        p.FontStyle = FontStyles.Normal;
        p.Foreground = Theme.Text;
        p.Background = null;
        p.LineHeight = Theme.BaseLineHeight;
        p.Margin = Theme.BodyMargin;
        p.Padding = new Thickness(0);
        p.BorderThickness = new Thickness(0);
        p.BorderBrush = null;
        p.TextAlignment = TextAlignment.Left;
        p.TextIndent = 0;
    }

    private static void ApplyCodeBlockLook(Paragraph p)
    {
        p.FontFamily = Theme.Mono;
        p.FontSize = Theme.CodeSize;
        p.Foreground = Theme.Code;
        p.Background = Theme.CodeBg;
        p.LineHeight = Theme.CodeSize * 1.5;
        p.Margin = new Thickness(0);
        p.Padding = new Thickness(10, 1, 10, 1);
    }

    /// <summary>Zerlegt eine Zeile in Läufe für Fett, Kursiv, Code, Links, Durchstreichen.</summary>
    private static IEnumerable<Inline> BuildInline(string text, Brush baseBrush)
    {
        var result = new List<Inline>();
        if (text.Length == 0)
        {
            result.Add(Make(string.Empty, baseBrush));
            return result;
        }

        int pos = 0;
        foreach (Match m in RxInline.Matches(text))
        {
            if (m.Index > pos)
                result.Add(Make(text[pos..m.Index], baseBrush));

            if (m.Groups["code"].Success)
            {
                string inner = m.Value[1..^1];
                result.Add(Make("`", Theme.Marker));
                result.Add(Make(inner, Theme.Code, family: Theme.Mono, size: Theme.CodeSize));
                result.Add(Make("`", Theme.Marker));
            }
            else if (m.Groups["link"].Success)
            {
                int split = m.Value.IndexOf("](", StringComparison.Ordinal);
                string label = m.Value[1..split];
                string tail = m.Value[split..];
                result.Add(Make("[", Theme.Marker));
                result.Add(Make(label, Theme.Link, underline: true));
                result.Add(Make(tail, Theme.Marker));
            }
            else if (m.Groups["bold"].Success)
            {
                string mark = m.Value[..2];
                result.Add(Make(mark, Theme.Marker));
                result.Add(Make(m.Value[2..^2], baseBrush, weight: FontWeights.Bold));
                result.Add(Make(mark, Theme.Marker));
            }
            else if (m.Groups["strike"].Success)
            {
                result.Add(Make("~~", Theme.Marker));
                result.Add(Make(m.Value[2..^2], Theme.Muted, strike: true));
                result.Add(Make("~~", Theme.Marker));
            }
            else // italic
            {
                string mark = m.Value[..1];
                result.Add(Make(mark, Theme.Marker));
                result.Add(Make(m.Value[1..^1], baseBrush, style: FontStyles.Italic));
                result.Add(Make(mark, Theme.Marker));
            }

            pos = m.Index + m.Length;
        }

        if (pos < text.Length)
            result.Add(Make(text[pos..], baseBrush));

        if (result.Count == 0)
            result.Add(Make(text, baseBrush));

        return result;
    }

    private static Run Make(
        string text,
        Brush brush,
        FontWeight? weight = null,
        FontStyle? style = null,
        FontFamily? family = null,
        double? size = null,
        bool underline = false,
        bool strike = false)
    {
        var run = new Run(text) { Foreground = brush };
        if (weight.HasValue) run.FontWeight = weight.Value;
        if (style.HasValue) run.FontStyle = style.Value;
        if (family is not null) run.FontFamily = family;
        if (size.HasValue) run.FontSize = size.Value;

        if (underline || strike)
        {
            var decorations = new TextDecorationCollection();
            if (underline) decorations.Add(TextDecorations.Underline);
            if (strike) decorations.Add(TextDecorations.Strikethrough);
            run.TextDecorations = decorations;
        }

        return run;
    }
}
