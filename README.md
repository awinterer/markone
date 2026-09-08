<p align="center">
  <img src="assets/logo/markone-logo.svg" alt="MarkOne" width="440">
</p>

# MarkOne

**Ein kleiner Markdown-Editor für Windows.** Schlank wie Notepad, aber mit
lesbarer Typografie und einer Navigation, die deine Notizen nach Überschriften
statt nach Dateinamen sortiert zeigt.

Einspaltig und live gestylt: Du tippst Markdown, die Formatierung erscheint
sofort. Die Markdown-Zeichen (`##`, `**`) bleiben sichtbar, treten aber in
dezentem Grau zurück — du weißt jederzeit, was im Dokument steht.

## Warum

Die meisten Markdown-Editoren bringen eine komplette Browser-Engine mit, nur um
Text darzustellen: Obsidian, Typora, Zettlr und Joplin bauen auf Electron,
Ghostwriter auf QtWebEngine. Das kostet mehrere hundert Megabyte auf der Platte
für eine Aufgabe, die keine braucht.

MarkOne rendert nativ mit WPF. Die Anwendung ist rund **250 KB** groß.

## Was es kann

**Navigation.** Ein Basisverzeichnis öffnen, darunter erscheinen alle
Unterverzeichnisse und Markdown-Dateien. Jede Datei zeigt zweizeilig ihre erste
Überschrift und darunter klein den Dateinamen. Ordner, unter denen keine
Markdown-Datei liegt, werden ausgeblendet; `node_modules`, `.git` und
Ähnliches übersprungen. Verzeichnisse werden erst beim Aufklappen im
Hintergrund eingelesen, große Bäume blockieren den Start also nicht.

Das zuletzt gewählte Basisverzeichnis wird gemerkt und beim nächsten Start
wieder geöffnet.

**Absturzsicherung.** Drei Sekunden nach der letzten Eingabe schreibt MarkOne
eine Arbeitskopie nach `%APPDATA%\MarkOne\recovery\`. Die Originaldatei bleibt
unberührt, bis du bewusst speicherst. Beim nächsten Start meldet sich ein
Dialog mit den liegengebliebenen Fassungen. Abschaltbar unter *Ansicht*.

**Versionen.** Der Knopf *Version sichern* legt eine fortlaufend nummerierte
Kopie im Unterordner `.versions` neben der Datei ab — `entwurf_v001.md`,
`entwurf_v002.md` und so weiter. Der Ordner ist als versteckt markiert und
erscheint nicht in der Navigation.

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
| `Strg+N` | Neues Dokument |
| `Strg+O` | Einzelne Datei öffnen |
| `Strg+Umschalt+O` | Basisverzeichnis für die Navigation wählen |
| `Strg+S` | Speichern |
| `Strg+Umschalt+S` | Speichern unter |
| `Strg+Alt+S` | Als neue Version in `.versions` sichern |
| `Strg+F` | Suchen |
| `Strg+H` | Ersetzen |
| `Strg+Z` / `Strg+Y` | Rückgängig / Wiederholen |
| `F5` | Navigation neu einlesen |

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

Das XAML enthält keine einzige Farbe als Zahl: `Theme.Install` trägt die Pinsel
und Schriften beim Start als benannte Ressourcen ein, auf die die Oberfläche
per `DynamicResource` zugreift. Die Form der Bedienelemente steht in
[`src/MarkOne/Controls.xaml`](src/MarkOne/Controls.xaml). Symbole in der
Kopfzeile kommen aus der Windows-Schrift *Segoe Fluent Icons*, es liegen keine
Bilddateien bei.

## Aufbau

| Datei | Zweck |
|---|---|
| `Theme.cs` | Typografie und Farben — der einzige Ort für Gestaltungsfragen |
| `Controls.xaml` | Form der Bedienelemente: Menü, Knöpfe, Baum, Bildlaufleisten |
| `TitleBar.cs` | Färbt die Titelleiste unter Windows 11 passend zur Kopfzeile |
| `MarkdownStyler.cs` | Erkennt Markdown pro Zeile und baut die Textläufe |
| `FileTree.cs` | Navigationsbaum: Einlesen, Filtern, Überschriften auslesen |
| `Settings.cs` | Was zwischen zwei Starts erhalten bleibt |
| `Recovery.cs` | Absturzsicherung |
| `Versioning.cs` | Nummerierte Zwischenstände |
| `MainWindow.xaml(.cs)` | Fenster, Werkzeugleiste, Dateiverwaltung, Suchen |
| `FindReplaceWindow.xaml(.cs)` | Der Suchen-Dialog |
| `RecoveryWindow.xaml(.cs)` | Auswahl liegengebliebener Fassungen |
| `tools/LogoGen/` | Erzeugt Logo und Symboldatei aus einer Geometriebeschreibung |

Kern des Editors ist eine WPF-`RichTextBox`, in der **jede Zeile ein eigener
Absatz** ist. Beim Tippen wird nur der Absatz unter dem Cursor neu formatiert;
das ganze Dokument nur dann, wenn sich Codeblock-Grenzen verschieben oder
größere Mengen Text eingefügt werden.

## Wo MarkOne Daten ablegt

| Ort | Inhalt |
|---|---|
| `%APPDATA%\MarkOne\settings.json` | Basisverzeichnis, Fenstergröße, Baumbreite, Autospeichern |
| `%APPDATA%\MarkOne\recovery\` | Arbeitskopien der Absturzsicherung |
| `<Dokumentordner>\.versions\` | Nummerierte Versionen, versteckt |

## Messwerte

Gemessen auf einem Windows-11-Rechner mit 20 Kernen:

| | MarkOne | Ghostwriter | Windows Notepad |
|---|---|---|---|
| Speicherplatz | **0,25 MB** | 431 MB | (Systembestandteil) |
| Startzeit | **~460 ms** | — | — |
| Arbeitsspeicher | 289 MB | 200 MB | 210 MB |

Der Platzbedarf-Unterschied ist echt und groß. Beim **Arbeitsspeicher gibt es
keinen Vorteil** — rund 200 MB sind die Grundlast von .NET und WPF, unabhängig
davon, wie klein die Anwendung ist. Wer RAM sparen will, braucht eine andere
Technologiebasis (Win32/C++), nicht ein kleineres Programm.

## Bekannte Grenzen

- **Die Navigation aktualisiert sich nicht von selbst.** Dateien, die außerhalb
  von MarkOne entstehen, erscheinen erst nach `F5`. Ein Dateisystem-Wächter
  wurde bewusst weggelassen — bei Netzlaufwerken und großen Bäumen bringt er
  mehr Ärger als Nutzen.
- **Keine verschachtelte Auszeichnung.** `**fett mit _kursiv_ darin**` wird nur
  auf der äußeren Ebene erkannt.
- **Tabellen** werden nicht besonders dargestellt, nur als normaler Text.
- **Rückgängig** kann in seltenen Fällen einen Formatierungsschritt statt einer
  Texteingabe zurücknehmen — die Neuformatierung landet mit im Undo-Verlauf.
- **Mehrere Instanzen** teilen sich `settings.json`; beim Schließen gewinnt die
  zuletzt beendete.
- Umschalt+Enter erzeugt bewusst einen normalen Absatz, keinen weichen Umbruch.
- Kein Dark Mode.

## Verhältnis zu Ghostwriter

Keines. MarkOne ist von Grund auf neu geschrieben und enthält **keine Zeile
Code** aus [KDE Ghostwriter](https://github.com/KDE/ghostwriter) — andere
Sprache, anderes Framework, andere Architektur. Ghostwriter war lediglich der
Anlass: Es diente als Ausgangspunkt der Suche nach einem schlanken Editor und
wird hier nur als Vergleichsmaßstab genannt.

## Lizenz

[Apache-2.0](LICENSE) — Copyright 2026 Andreas Winterer.

Die Lizenz erlaubt Nutzung, Änderung und Weitergabe auch kommerziell und
gewährt eine ausdrückliche Patentlizenz. Rechte an Namen und Marken überträgt
sie nicht.
