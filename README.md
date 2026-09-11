<p align="center">
  <img src="assets/logo/markone-logo.svg" alt="MarkOne" width="440">
</p>

# MarkOne

**Ein kleiner Markdown-Editor für Windows.** Schlank wie Notepad, aber mit
lesbarer Typografie und einer Navigation, die deine Notizen nach Überschriften
statt nach Dateinamen sortiert zeigt. Bilder, PDF- und HTML-Dateien aus
demselben Ordner zeigt MarkOne gleich mit an, alles andere öffnet es im
zugehörigen Programm.

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
auskommt, und 0,9 MB Anbindung an die Edge-Engine von Windows für HTML.
Die Engine selbst gehört zu Windows 11 und wird nur gestartet, solange
eine HTML-Datei zu sehen ist.

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

**HTML.** Für HTML braucht es eine echte Rendering-Engine, und die einzige
brauchbare unter Windows ist die von Edge (WebView2), die auf jedem
Windows 11 liegt. MarkOne startet sie erst, wenn eine HTML-Datei angeklickt
wird, und beendet sie wieder, sobald etwas anderes zu sehen ist. Während
eine Seite offen ist, kostet das rund 350 MB Arbeitsspeicher in sechs
Edge-Prozessen; danach ist alles wieder frei. Links ins Netz öffnen im
Browser, lokale Links zu HTML, Text und Bildern bleiben in MarkOne, andere
lokale Dateien werden nicht angefasst, Downloads gibt es nicht. Eine Seite
ohne eigenes Stylesheet bekommt die Typografie des Editors, eine mit
eigenem Layout bleibt unangetastet. Strg+Mausrad zoomt.

Der Editor bleibt im Hintergrund erhalten, solange ein Bild, PDF oder HTML
zu sehen ist. Ein Klick auf die offene Markdown-Datei holt ihn ohne Neuladen
zurück. Speichern, Suchen und Versionen sind in der Zeit abgeschaltet.

**Export nach PDF und HTML.** Über *Datei*, den Knopf *PDF* in der
Kopfzeile, Strg+Umschalt+P oder per Rechtsklick auf eine Markdown-Datei im
Baum. Der Editor exportiert den aktuellen Text, auch ungespeichert; der Baum
die Datei, ohne sie zu öffnen. Die Umwandlung übernimmt Markdig (CommonMark
mit Tabellen, Fußnoten, Aufgabenlisten), das PDF druckt die Edge-Engine
unsichtbar im Hintergrund: A4, 20 mm Rand, Georgia 11 Punkt, Kopfzeile mit
Datum und Titel, Seitenzahlen im Fuß, Text durchsuchbar, Bilder eingebettet.
Jede Zeile bleibt eine Zeile, so wie sie im Editor steht. Pfade mit
Leerzeichen in Links und Bildern sind erlaubt, auch wenn CommonMark das
eigentlich nicht vorsieht. Das fertige PDF erscheint im Baum und lässt sich
gleich prüfen.

Dasselbe geht ohne Fenster von der Kommandozeile, etwa aus Skripten:

```bash
MarkOne.exe --pdf notizen.md
```

```bash
MarkOne.exe --html notizen.md ziel.html
```

Ohne Zielangabe entsteht die Datei neben der Quelle. Rückgabewert 0 bei
Erfolg, der Pfad steht auf der Konsole.

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
genau eine `pdfium.dll` neben der EXE liegt. Die Pakete kommen von nuget.org;
die Quelle steht in `nuget.config` im Repository.

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
| `Strg+Umschalt+P` | Als PDF exportieren |
| `Strg+Z` / `Strg+Y` | Rückgängig / Wiederholen |
| `F5` | Navigation neu einlesen |
| `Enter` / Doppelklick im Baum | Datei im zugehörigen Programm öffnen |

Im Bild- und PDF-Betrachter:

| Kürzel | Funktion |
|---|---|
| `Strg+Mausrad` | Zoomen um den Mauszeiger |
| `Strg+0` | Einpassen (Bild), Seitenbreite (PDF), 100 % (HTML) |
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
| `HtmlViewer.xaml(.cs)` | HTML über die Edge-Engine, nur solange sie gebraucht wird |
| `MarkdownExport.cs` | Markdown nach HTML (Markdig) und PDF (Edge-Engine, unsichtbar) |
| `HtmlStyle.cs` | Die Typografie des Editors als CSS für Bildschirm und Papier |
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
| `%LOCALAPPDATA%\MarkOne\WebView2\` | Zwischenspeicher der Edge-Engine für HTML |
| `%LOCALAPPDATA%\MarkOne\export\` | Die Zwischenseite des PDF-Exports |
| `<Dokumentordner>\.versions\` | Nummerierte Versionen, versteckt |

## Messwerte

Gemessen auf einem Windows-11-Rechner mit 20 Kernen:

| | MarkOne | Ghostwriter | Windows Notepad |
|---|---|---|---|
| Speicherplatz | **9,0 MB** (0,3 MB ohne PDF-Kern, HTML-Anbindung und Markdig) | 431 MB | (Systembestandteil) |
| Startzeit | **~460 ms** | — | — |
| Arbeitsspeicher | 289 MB | 200 MB | 210 MB |
| Arbeitsspeicher mit offener HTML-Datei | rund 650 MB, davon 350 MB Edge | | |

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
- **HTML:** Tastenkürzel von MarkOne greifen nicht, solange die Seite den
  Fokus hat; erst wieder nach einem Klick in Baum oder Kopfzeile.
- **Export:** Bilder müssen auf demselben Laufwerk liegen und relativ oder
  ohne `file://` angegeben sein; Formeln und Diagramme werden nicht gesetzt.
  Der PDF-Export braucht die WebView2-Laufzeit von Windows 11 und dauert
  ein paar Sekunden, weil die Engine dafür startet.
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
