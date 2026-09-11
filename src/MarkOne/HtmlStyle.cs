using System.Globalization;
using System.Text;
using System.Windows.Media;

namespace MarkOne;

/// <summary>
/// Die Typografie des Editors als CSS: einmal für den Bildschirm (HTML-Betrachter,
/// HTML-Export), einmal für Papier (PDF-Export). Farben kommen aus Theme.cs.
/// </summary>
public static class HtmlStyle
{
    public static string Screen() => Build(
        font: Px(Theme.BaseSize), line: "1.7",
        column: $"max-width:{Px(Theme.MaxColumnWidth)};margin:34px auto;padding:0 40px",
        paper: Hex(Theme.Paper), code: Px(Theme.CodeSize),
        h1: "30px", h2: "24px", h3: "20px", print: false);

    public static string Print() => Build(
        font: "11pt", line: "1.5",
        column: "margin:0;padding:0",
        paper: "#FFFFFF", code: "9.5pt",
        h1: "22pt", h2: "17pt", h3: "14pt", print: true);

    private static string Build(string font, string line, string column, string paper, string code,
        string h1, string h2, string h3, bool print)
    {
        var css = new StringBuilder();
        css.Append("body{font-family:Georgia,Cambria,serif;font-size:").Append(font)
           .Append(";line-height:").Append(line).Append(";color:").Append(Hex(Theme.Text))
           .Append(";background:").Append(paper).Append(';').Append(column).Append('}');
        css.Append("h1,h2,h3,h4,h5,h6{color:").Append(Hex(Theme.Heading)).Append(";line-height:1.35;margin:1.4em 0 .5em;font-weight:bold}");
        css.Append("h1{font-size:").Append(h1).Append("}h2{font-size:").Append(h2).Append("}h3{font-size:").Append(h3).Append('}');
        css.Append("p,ul,ol{margin:0 0 .7em}li{margin:.15em 0}");
        css.Append("a{color:").Append(Hex(Theme.Link)).Append('}');
        css.Append("code,pre,kbd{font-family:'Cascadia Mono',Consolas,monospace;font-size:").Append(code)
           .Append(";color:").Append(Hex(Theme.Code)).Append(";background:").Append(Hex(Theme.CodeBg)).Append('}');
        css.Append("code{padding:1px 4px;border-radius:3px}pre{padding:12px 14px;border-radius:4px;overflow-x:auto;line-height:1.45}pre code{padding:0}");
        css.Append("blockquote{color:").Append(Hex(Theme.Quote)).Append(";border-left:3px solid ").Append(Hex(Theme.QuoteBar))
           .Append(";margin:0 0 .7em;padding-left:16px}");
        css.Append("table{border-collapse:collapse;margin:0 0 .9em}td,th{border:1px solid ").Append(Hex(Theme.Rule))
           .Append(";padding:4px 10px;text-align:left;vertical-align:top}th{background:").Append(Hex(Theme.CodeBg)).Append('}');
        css.Append("img{max-width:100%;height:auto}hr{border:0;border-top:1px solid ").Append(Hex(Theme.Rule)).Append(";margin:1.2em 0}");
        css.Append("del{color:").Append(Hex(Theme.Muted)).Append("}input[type=checkbox]{margin-right:.4em}");
        css.Append(".footnotes{font-size:.85em;color:").Append(Hex(Theme.Quote)).Append('}');

        if (print)
        {
            css.Append("h1,h2,h3,h4{page-break-after:avoid}");
            css.Append("pre,blockquote,table,figure,img{page-break-inside:avoid}");
            css.Append("a{text-decoration:none}");
        }
        return css.ToString();
    }

    private static string Hex(SolidColorBrush b) => $"#{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}";

    // CSS will den Punkt als Dezimaltrenner, egal welche Sprache Windows spricht.
    private static string Px(double value) => value.ToString("0.##", CultureInfo.InvariantCulture) + "px";
}
