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
}

public static class MarkdownStyler
{
    private static readonly Regex RxHeading = new(@"^(#{1,6})([ \t]+)(.*)$", RegexOptions.Compiled);
    private static readonly Regex RxFence = new(@"^\s*(```|~~~)", RegexOptions.Compiled);
    private static readonly Regex RxRule = new(@"^\s*([-*_])(\s*\1){2,}\s*$", RegexOptions.Compiled);
    private static readonly Regex RxQuote = new(@"^([ \t]*(?:>[ \t]?)+)(.*)$", RegexOptions.Compiled);
    private static readonly Regex RxList = new(@"^([ \t]*)([-*+]|\d{1,9}[.)])([ \t]+)(.*)$", RegexOptions.Compiled);

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
        text.AsSpan().IndexOfAny("#*_`[~>-".AsSpan()) >= 0 || text.Length > 0 && char.IsDigit(text[0]);

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
