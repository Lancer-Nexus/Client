# Forward-Pass-Reihenfolge im SystemRenderer

Stand: 2026-10-15. Quellcode-Audit der Aufrufreihenfolge in
`SystemRenderer.Draw()`. Dies beschreibt den bestehenden OpenGL/Null-nahen
Rendererablauf und begründet keine Laufzeit- oder GPU-Leistungsmessung.

## Reihenfolge

1. AA-Ziel bei geänderter Größe/Qualität ersetzen; bei Nullgröße den Draw
   überspringen.
2. Kamera, Lichtpool und Nebelzustand aktualisieren.
3. Objekte und temporäre Effekte vorbereiten; Nebel-/Partikeldaten und Knochen-
   sowie Quadbuffer streamen.
4. Farbe, Tiefe und Stencil für das Ziel löschen. In vollständigem Nebel wird
   die Nebelfarbe als Clear-Farbe verwendet.
5. Frame-Writer initialisieren und einen Tiefenvorlauf für Game-Objekte zeichnen
   (`ColorWrite=false`, `DepthFunction=Less`).
6. Die eigentliche Szenengeometrie ausgeben: `Beams.Begin`, Objekt-Draws,
   Asteroiden, Nebel und das Befüllen von Billboards, Effektpool und Polylinien.
   Opaque-Aufrufe aus `CommandBuffer.AddCommand` zeichnen sofort; transparente
   Befehle werden gesammelt.
7. Optional Starsphere und Nebelübergang direkt ausgeben.
8. `OpaqueHook` ausführen; transparente Kommandos mit deaktiviertem Depth-Write
   ausführen; danach Depth-Write aktivieren.
9. `PhysicsHook` und Debuggeometrie ausgeben.
10. Bei AA Viewport/Scissor zurücksetzen, MSAA/SMAA auf das vorherige Ziel blitten
    und das vorherige RenderTarget wieder binden.

Die Reihenfolge steht in `src/LibreLancer/Render/SystemRenderer.cs`, `Draw()`.
`CommandBuffer.DrawOpaque()` rendert keine opaque Geometrie: Es sortiert die
transparenten Befehle und bereitet Billboard-Indizes vor. `DrawTransparent()`
führt danach die gesammelten transparenten Befehle aus. Opaque Materialdraws
werden synchron bei `CommandBuffer.AddCommand()` ausgegeben.

## Erster Passvertrag im Quellcode

`SystemRenderPasses` beschreibt sechs GPU-Phasen des SystemRenderer:
Frame-Clear, Depth, Opaque, Stars/Nebel, Transparent und AA Resolve. Der Clear
wird nun zusammen mit den übrigen Phasen asynchron gemessen. Der Opaque-Timer
umfasst die synchrone Geometrieausgabe innerhalb der Draw-Submission; zuvor lag
er fälschlich um `CommandBuffer.DrawOpaque()`, das keine opaque Meshes zeichnet.
Die Zeitmessung umfasst GPU-Arbeit innerhalb dieses Blocks, nicht CPU-Update-
oder Vorbereitungszeit. Die typisierten IDs steuern die Query-Slots; die
Deskriptoren halten zusätzlich abstrakte Farb-/Tiefenressourcen fest. Stars/Nebel
und Resolve sind bedingt. Der Hauptpfad wird inzwischen über
`SystemRenderPassGraph` von Frame-Clear bis zum optionalen AA-/HDR-Resolve
geplant und ausgeführt. Dazwischen liegen Opaque-/Transparent-Pässe,
`PhysicsHook` und Debug-Overlay als abhängige Aktionen. Der Graph leitet Abhängigkeiten
aus Ressourcen-Reads/Writes ab, auch wenn ein Producer später registriert ist;
Read-after-write, Write-after-read und Write-after-write werden geordnet.
Pflicht-Reads, explizite Vorgänger und Zyklen werden vor Ausführung der
Callbacks geprüft. Die Callbacks verwalten
weiterhin RenderTarget-Wechsel, Attachments und RenderContext-Zustand; der Graph
bindet oder erzeugt keine GPU-Ziele. Eine Laufzeit-/Bildabnahme und Messung der
Scheduler-Allokationen fehlen weiterhin.

## Was für einen vollständigen Render-Graph-Vertrag noch fehlt

Der [Target- und Attachment-Audit](render-target-attachment-contract-20261016.md)
hat die derzeitigen OpenGL-Zielformate, Stencilpfade und Load-/Store-Semantik
nachverfolgt. Diese Angaben stehen noch nicht maschinenlesbar im
`SystemRenderPassDescriptor`. Swapchain-/Fremdziel-Formate, die drei getrennten
SMAA-Unterpässe und generische AA-Ausgaben bleiben offene Vertragspunkte.
Scene extraction, CPU-Vorbereitung und GPU-Ausgabe sind in
`SystemRenderer.Draw()` vermischt.
