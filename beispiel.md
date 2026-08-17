# Feder — ein kleiner Markdown-Editor

Eine Zeile Fließtext, damit du die **Laufweite** und die _Zeilenhöhe_ beurteilen
kannst. Der Satz ist bewusst etwas länger, weil sich Typografie erst über mehrere
Zeilen hinweg zeigt und nicht an einem einzelnen Wort.

## Überschrift zweiter Ebene

Die Markdown-Zeichen bleiben sichtbar, treten aber optisch zurück — du siehst
jederzeit, was du geschrieben hast, ohne dass es dich beim Lesen stört.

### Dritte Ebene

- Ein Listenpunkt
- Noch einer, mit `Code im Fließtext` mittendrin
- Und einer mit einem [Link](https://example.org)

> Ein Zitat steht kursiv und bekommt eine ruhige Linie an der Seite,
> statt eines lauten farbigen Kastens.

1. Nummerierte Listen
2. funktionieren ebenso
3. ~~durchgestrichen~~ übrigens auch

```csharp
// Codeblöcke stehen in Monospace auf leicht getöntem Grund.
var editor = new Feder();
editor.Open("beispiel.md");
```

---

Ein letzter Absatz nach der Trennlinie.
