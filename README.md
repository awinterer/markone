# MarkOne

**Ein kleiner Markdown-Editor für Windows.** Funktionsumfang wie Windows Notepad,
aber mit lesbarer Typografie.

Von [MELTIQ](https://meltiq.at).

Einspaltig und live gestylt: Du tippst Markdown, die Formatierung erscheint
sofort. Die Markdown-Zeichen (`##`, `**`) bleiben sichtbar, treten aber in
dezentem Grau zurück — du weißt jederzeit, was im Dokument steht.

## Warum

Die meisten Markdown-Editoren bringen eine komplette Browser-Engine mit, nur um
Text darzustellen: Obsidian, Typora, Zettlr und Joplin bauen auf Electron,
Ghostwriter auf QtWebEngine. Das kostet mehrere hundert Megabyte auf der Platte
für eine Aufgabe, die keine braucht.

MarkOne rendert nativ mit WPF. Die Anwendung ist rund **200 KB** groß.

## Bauen

Voraussetzung ist das .NET 9 SDK.

```bash
dotnet publish src/MarkOne/MarkOne.csproj -c Release -o dist
```

## Starten

```bash
dist\MarkOne.exe
```

Eine Datei direkt öffnen (funktioniert auch als „Öffnen mit" im Explorer):

```bash
dist\MarkOne.exe pfad\zur\datei.md
```

## Tastenkürzel

| Kürzel | Funktion |
|---|---|
| `Strg+N` | Neu |
| `Strg+O` | Öffnen |
| `Strg+S` | Speichern |
| `Strg+Umschalt+S` | Speichern unter |
| `Strg+F` | Suchen |
| `Strg+H` | Ersetzen |
| `Strg+Z` / `Strg+Y` | Rückgängig / Wiederholen |

## Was dargestellt wird

Überschriften `#` bis `######`, **fett**, _kursiv_, `Code`, ~~durchgestrichen~~,
Links, Zitate `>`, Aufzählungen und nummerierte Listen, Codeblöcke mit
dreifachen Backticks, Trennlinien `---`.

## Aussehen ändern

Alle Schriften, Größen, Abstände und Farben stehen in **einer** Datei:
[`src/MarkOne/Theme.cs`](src/MarkOne/Theme.cs). Dort etwa die Grundschrift von
Georgia auf etwas anderes umstellen, die Zeilenhöhe justieren oder die
Textspaltenbreite ändern (`MaxColumnWidth`, aktuell 760 Punkt — breiter liest
sich schlechter).

## Aufbau

| Datei | Zweck |
|---|---|
| `Theme.cs` | Typografie und Farben — der einzige Ort für Gestaltungsfragen |
| `MarkdownStyler.cs` | Erkennt Markdown pro Zeile und baut die Textläufe |
| `MainWindow.xaml(.cs)` | Fenster, Dateiverwaltung, Suchen/Ersetzen |
| `FindReplaceWindow.xaml(.cs)` | Der Suchen-Dialog |

Kern ist eine WPF-`RichTextBox`, in der **jede Zeile ein eigener Absatz** ist.
Beim Tippen wird nur der Absatz unter dem Cursor neu formatiert; das ganze
Dokument nur dann, wenn sich Codeblock-Grenzen verschieben oder größere Mengen
Text eingefügt werden.

## Messwerte

Gemessen auf einem Windows-11-Rechner mit 20 Kernen:

| | MarkOne | Ghostwriter | Windows Notepad |
|---|---|---|---|
| Speicherplatz | **0,2 MB** | 431 MB | (Systembestandteil) |
| Startzeit | **~490 ms** | — | — |
| Arbeitsspeicher | 264 MB | 200 MB | 210 MB |

Der Platzbedarf-Unterschied ist echt und groß. Beim **Arbeitsspeicher gibt es
keinen Vorteil** — rund 200 MB sind die Grundlast von .NET und WPF, unabhängig
davon, wie klein die Anwendung ist. Wer RAM sparen will, braucht eine andere
Technologiebasis (Win32/C++), nicht ein kleineres Programm.

## Bekannte Grenzen

- **Keine verschachtelte Auszeichnung.** `**fett mit _kursiv_ darin**` wird nur
  auf der äußeren Ebene erkannt.
- **Tabellen** werden nicht besonders dargestellt, nur als normaler Text.
- **Rückgängig** kann in seltenen Fällen einen Formatierungsschritt statt einer
  Texteingabe zurücknehmen — die Neuformatierung landet mit im Undo-Verlauf.
- Umschalt+Enter erzeugt bewusst einen normalen Absatz, keinen weichen Umbruch.
- Kein Dark Mode.

## Verhältnis zu Ghostwriter

Keines. MarkOne ist von Grund auf neu geschrieben und enthält **keine Zeile
Code** aus [KDE Ghostwriter](https://github.com/KDE/ghostwriter) — andere
Sprache, anderes Framework, andere Architektur. Ghostwriter war lediglich der
Anlass: Es diente als Ausgangspunkt der Suche nach einem schlanken Editor und
wird hier nur als Vergleichsmaßstab genannt.

## Lizenz

[Apache-2.0](LICENSE) — Copyright 2026 MELTIQ GmbH.

Die Lizenz erlaubt Nutzung, Änderung und Weitergabe auch kommerziell. Sie
gewährt keine Rechte an der Marke MELTIQ.
