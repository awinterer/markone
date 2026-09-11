<p align="center">
  <img src="assets/logo/markone-logo.svg" alt="MarkOne" width="440">
</p>

# MarkOne

**Ein kleiner Markdown-Editor für Windows.** Schlank wie Notepad, aber mit
lesbarer Typografie und einer Navigation, die deine Notizen nach Überschriften
statt nach Dateinamen sortiert zeigt. Bilder und PDF-Dateien aus demselben
Ordner zeigt MarkOne gleich mit an, alles andere öffnet es im zugehörigen
Programm.

Einspaltig und live gestylt: Du tippst Markdown, die Formatierung erscheint
sofort. Die Markdown-Zeichen (`##`, `**`) bleiben sichtbar, treten aber in
dezentem Grau zurück — du weißt jederzeit, was im Dokument steht.

## Warum

Die meisten Markdown-Editoren bringen eine komplette Browser-Engine mit, nur um
Text darzustellen: Obsidian, Typora, Zettlr und Joplin bauen auf Electron,
Ghostwriter auf QtWebEngine. Das kostet mehrere hundert Megabyte auf der Platte
für eine Aufgabe, die keine braucht.

MarkOne rendert nativ mit WPF. Der Editor selbst ist rund **300 KB** groß;
dazu kommen 7,2 MB für den PDF-Kern von Chrome (PDFium), der ohne Browser
auskommt.

## Was es kann

**Navigation.** Ein Basisverzeichnis öffnen, darunter erscheinen alle
Unterverzeichnisse und Markdown-Dateien. Jede Datei zeigt zweizeilig ihre erste
Überschrift und darunter klein den Dateinamen. Ordner, unter denen keine
Markdown-Datei liegt, werden ausgeblendet; `node_modules`, `.git` und
Ähnliches übersprungen. Verzeichnisse werden erst beim Aufklappen im
Hintergrund eingelesen, große Bäume blockieren den Start also nicht.

Das zuletzt gewählte Basisverzeichnis wird gemerkt und beim nächsten Start
wieder geöffnet. Ein Ordner als Startparameter (`MarkOne.exe C:\Notizen`)
öffnet ihn nur für diesen Start.

**Alle Dateien.** Der Baum zeigt auch Dateien, die MarkOne nicht selbst
bearbeitet, einzeilig mit einem Typkürzel wie PDF, DOCX oder XLSX. Ein Klick
nennt in der Statuszeile Größe und zugeordnetes Programm, Doppelklick oder
Enter startet es, etwa Word für eine .docx. Das Kontextmenü bietet
*Standardprogramm*, *Explorer* und *Pfad kopieren*. Wer nur seine Notizen
sehen will, schaltet *Alle Dateien anzeigen* unter *Ansicht* ab.

**Bilder.** PNG, JPEG, GIF, BMP, TIFF und ICO öffnen im eingebauten
Betrachter; WebP, HEIC und AVIF ebenfalls, wenn die zugehörigen
Windows-Erweiterungen installiert sind. Das Bild wird eingepasst, kleine
Bilder nicht aufgeblasen. Strg+Mausrad zoomt um den Mauszeiger, Ziehen mit
der Maus verschiebt, Doppelklick wechselt zwischen Einpassen und 100 %. 100 %
heißt ein Bildpixel je Bildschirmpixel, auch auf skalierten Bildschirmen.
Die Drehung aus den Kameradaten (EXIF) wird beachtet.

**PDF.** Alle Seiten untereinander, fortlaufend gescrollt, auf Seitenbreite
eingepasst. Gezeichnet wird nur, was im Blick ist; was weit weg scrollt, gibt
seinen Speicher wieder frei. Kommentare und Markierungen werden mitgezeichnet.
Die Datei wird beim Öffnen vollständig gelesen und bleibt auf der Platte
nicht gesperrt: Ein aus Word neu exportiertes PDF lässt sich einfach neu
anklicken.

Der Editor bleibt im Hintergrund erhalten, solange ein Bild oder PDF zu sehen
ist. Ein Klick auf die offene Markdown-Datei holt ihn ohne Neuladen zurück.
Speichern, Suchen und Versionen sind in der Zeit abgeschaltet.

**Absturzsicherung.** Drei Sekunden nach der letzten Eingabe schreibt MarkOne
eine Arbeitskopie nach `%APPDATA%\MarkOne\recovery\`. Die Originaldatei bleibt
unberührt, bis du bewusst speicherst. Beim nächsten Start meldet sich ein
Dialog mit den liegengebliebenen Fassungen. Abschaltbar unter *Ansicht*.

**Versionen.** Der Knopf *Version sichern* legt eine fortlaufend nummerierte
Kopie im Unterordner `.versions` neben der Datei ab — `entwurf_v001.md`,
`entwurf_v002.md` und so weiter. Der Ordner ist als versteckt markiert und
erscheint nicht in der Navigation.

## Bauen

Voraussetzung ist das .NET 9 SDK. Gebaut wird für 64-Bit-Windows, damit
genau eine `pdfium.dll` neben der EXE liegt.

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
| `Enter` / Doppelklick im Baum | Datei im zugehörigen Programm öffnen |

Im Bild- und PDF-Betrachter:

| Kürzel | Funktion |
|---|---|
| `Strg+Mausrad` | Zoomen um den Mauszeiger |
| `Strg+0` | Einpassen (Bild) bzw. Seitenbreite (PDF) |
| `Strg+1` | 100 % |
| `Strg++` / `Strg+-` | Größer / kleiner |
| Doppelklick (Bild) | Einpassen und 100 % wechseln |
| Ziehen (Bild) | Verschieben |
| `Bild ↑` / `Bild ↓` / `Leertaste` (PDF) | Blättern |

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
| `FileTree.cs` | Navigationsbaum: Einlesen, Filtern, Dateiart bestimmen, Überschriften auslesen |
| `Shell.cs` | Übergabe an Windows: Standardprogramm, Explorer, Programmname |
| `ImageViewer.xaml(.cs)` | Bildbetrachter mit Zoom und Verschieben |
| `PdfViewer.xaml(.cs)` | PDF-Betrachter: Seitenlayout, Zoom, Zeichnen nach Bedarf |
| `PdfDocument.cs` | Ein geöffnetes PDF: Seitengrößen, Seiten zeichnen |
| `Pdfium.cs` | Die Handvoll PDFium-Aufrufe und der eine Thread dafür |
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
| Speicherplatz | **7,6 MB** (0,3 MB ohne PDF-Kern) | 431 MB | (Systembestandteil) |
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
- **PDF:** kein Textmarkieren, keine Suche, keine Formularfelder; Seiten
  werden als Bild gezeichnet. Bei 200 % Bildschirmskalierung kostet eine
  eingepasste Seite rund 15 MB Arbeitsspeicher, gehalten werden die
  sichtbaren Seiten plus je eine davor und danach.
- **Bilder:** animierte GIFs zeigen das erste Bild, SVG wird nicht dargestellt.
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
