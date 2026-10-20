# Render-Target- und Attachment-Vertrag im SystemRenderer

Stand: 2026-10-16. Quellcode-Audit von `SystemRenderer.Draw()`,
`AntialiasTarget`, den OpenGL-Render-Targets und `RenderContext`.
Die Angaben beschreiben den aktuellen Pfad; sie sind keine Laufzeitmessung.

## Zielauswahl

`SystemRenderer.Draw()` speichert zuerst das aktuell gebundene Ziel. Ohne AA
bleibt dieses Ziel während der Szene aktiv: Es ist entweder die Swapchain
(`RenderContext.RenderTarget == null`) oder ein vom Aufrufer gebundenes Ziel.
Mit SMAA/MSAA zeichnet der Renderer zunächst in ein eigenes `AntialiasTarget`
und gibt das Bild anschließend auf der Swapchain oder dem ursprünglichen
`RenderTarget2D` aus. Viewport und Scissor werden für das AA-Ziel gesetzt und
vor dem Resolve zurückgestellt. Der äußere `finally`-Pfad stellt außerdem das
zuvor gebundene Ziel wieder her.

## Belegte OpenGL-Zielformate

| Ziel | Farbe | Tiefe | Stencil | Nachweis |
|---|---|---|---|---|
| Systemziel ohne AA | Vom Aufrufer oder Backend bestimmt. Ein normales `RenderTarget2D` erstellt seine `Texture2D` mit `SurfaceFormat.Bgra8`; ein Ziel ohne Wrapper ist hier nicht abfragbar. | Vom gebundenen Ziel bestimmt. Der Standard-`RenderTarget2D` verwendet `DepthBuffer` ohne Stencil. | Nur falls das gebundene Ziel es bereitstellt. | `RenderTarget2D`, `Texture2D`, `DepthBuffer`, `GLRenderTarget2D` |
| SMAA-Szenenziel | `SurfaceFormat.Bgra8` | D24, ohne Stencil | Nein | `AntialiasTarget` erstellt `new RenderTarget2D(context, width, height)` |
| SMAA-Kanten- und Gewichtsziel | Je `SurfaceFormat.Bgra8` | Gemeinsamer D24S8-Puffer | Ja, geteilt zwischen beiden Zielen | `smaaEdges` und `smaaBlend` teilen `depthStencil` |
| MSAA-Szenenziel | OpenGL GLES: `GL_RGBA8`; Desktop-OpenGL: unformatiertes internes `GL_RGBA` | D24 | Nein | `GLMultisampleTarget` |
| MSAA-Resolve-Textur | OpenGL-Resolve-FBO mit internem `GL_RGBA` | Keiner | Nein | `GLMultisampleTarget.CreateResolveFbo()` |
| Ausgabe nach AA | Swapchain oder vom Aufrufer gebundenes `RenderTarget2D`; dessen Zielformat ist hier nicht allgemein abfragbar. | Resolve kopiert keine Tiefe. | Resolve kopiert kein Stencil. | Blits verwenden ausschließlich `GL_COLOR_BUFFER_BIT` |

Die Oberflächenformate der wrapper-erzeugten 2D-Ziele kommen vom Defaultparameter
`Texture2D(..., SurfaceFormat.Bgra8)`. Die Desktop-MSAA-Angabe bleibt bewusst
`GL_RGBA`: der Code fordert dort kein explizites sized internal format an.
Das Null-Backend erzeugt keine echten GPU-Attachments und bestätigt daher keine
Format- oder Lebensdauersemantik.

## Load-/Store-Semantik des vorhandenen Ablaufs

| Phase | Ziel und Operation | Erhaltene Inhalte |
|---|---|---|
| Frame clear | Setzt `ColorWrite` und `DepthWrite` auf `true`, löscht dann Farbe und Tiefe. OpenGL löscht zusätzlich Stencil, wenn das gebundene Ziel `HasStencil` meldet; das optionale Szenen-Stencil ist nun als eigene Ressource im Vertrag aufgeführt. Der Clear folgt dem aktiven Scissorbereich. | Danach werden Farbe und Tiefe im Szenenziel gespeichert. |
| Depth prepass | Lädt die zuvor gelöschte Tiefe, testet und schreibt sie; `ColorWrite=false` erhält die Szenenfarbe. | Szenenfarbe bleibt unverändert, Tiefe bleibt für folgende Phasen verfügbar. |
| Opaque | Lädt vorhandene Szene-/Tiefenwerte; synchrone opake Materialdraws schreiben Farbe und können Tiefe schreiben. | Die Szene bleibt für Stars und transparente Draws erhalten. |
| Stars / nebula | Zeichnet bedingt auf dasselbe Szenenziel und liest den vorhandenen Tiefeninhalt. | Vorhandene Szenenfarbe bleibt durch die normalen Draw-/Blendregeln erhalten. |
| Transparent | `DepthWrite=false`; transparente Befehle verwenden den Szenentiefenpuffer und blenden in die vorhandene Szenenfarbe. | Tiefe bleibt erhalten; Farbe wird fortgeschrieben. |
| MSAA resolve | Blittet Farbe aus dem multisampled Ziel in den Ausgabebereich. | Pixel außerhalb des Blit-Rechtecks bleiben erhalten; Tiefe/Stencils werden nicht kopiert. |
| SMAA resolve | Edge-Detection schreibt Kantenfarbe und Stencil; der Gewichtspass löscht zuerst die Gewichtsfarbe auf transparent schwarz, liest dann Kanten und Stencil und schreibt Gewichte; Neighborhood-Blending liest Szenenfarbe plus Gewichte und schreibt in den Ausgabebereich. | Der interne `ClearDepth()` setzt `DepthWrite` vorübergehend auf `true` und löscht beim Edge-Ziel sowohl die Depth- als auch die Stencil-Komponente des gemeinsamen D24S8-Attachments. Beide Aspekte sind im Vertrag getrennt ausgewiesen; die Farbe des Edge-Ziels wird durch den anschließenden Vollbild-Draw geschrieben. Ausgabe außerhalb des betroffenen Viewports bleibt erhalten. |

Diese Load-/Store-Begriffe beschreiben die beobachtete OpenGL-Semantik. Vor
Frame-Clear setzt `SystemRenderer` die Farb- und Tiefenmaske explizit; sein
äußerer `finally`-Pfad stellt die zuvor vom Aufrufer verwendeten Masken wieder
her. Der SMAA-Gewichtspass setzt vor seinem stencil-maskierten Draw die Farbmaske auf
Schreiben und löscht das Gewichtsziel auf transparent schwarz, damit unberührte
Pixel keine Werte aus dem vorherigen Frame behalten. Clearfarbe und Farbmaske
werden im `finally` wiederhergestellt. Auch der SMAA-interne Tiefen-/Stencil-
Clear stellt seine Tiefenmaske bei Fehlern wieder her. Die Grafikabstraktion stellt dafür noch keine expliziten
Attachment-Operationen bereit; die SMAA-Gewichtsfarbe wird direkt im bestehenden
Pfad bereinigt. SMAA besitzt drei interne Draw-Pässe, die derzeit gemeinsam unter
`AntialiasResolve` gemessen werden; MSAA hat an dieser Stelle einen Farbblit.
`SystemRenderPasses` beschreibt nun die sechs gemessenen Hauptphasen sowie vier
AA-Unterpass-Verträge mit Zielrolle, Attachmentformat, Load-/Store-Operationen
und Ressourcenabhängigkeiten. Statische Vertragschecks erzwingen für `Load` eine vorherige Leseabhängigkeit,
für gespeicherte `Clear`-/`DontCare`-Attachments eine Schreibabhängigkeit und
prüfen Read-before-write in der Hauptpassfolge, der SMAA-Kette und dem separaten
MSAA-Resolve-Zweig. Präsentationsziel und SMAA-Lookup-Texturen gelten dabei als
importierte externe Ressourcen. Diese Einträge sind Metadaten und führen OpenGL-Bindungen noch nicht selbst aus.
Der `SystemRenderer` prüft vor und nach den Szenenphasen, dass das aktive Ziel
weiterhin dem zu Beginn gewählten Szenenziel entspricht. Die SMAA-Bindung prüft Zielrolle und Stencil-Anforderung. Beim Erzeugen eines
AA-Ziels prüft `AntialiasTarget` außerdem SMAA-Format, Maße und gemeinsamen
Stencil-Puffer sowie für MSAA Samplezahl, Maße und das Fehlen eines Stencil-
Attachments. Der MSAA-Color-Storage-Vertrag unterscheidet die angeforderte GLES-`GL_RGBA8`-
Variante von Desktop-OpenGLs implementation-defined unsized `GL_RGBA`. Das vom
Treiber realisierte sized internal format wird nicht separat abgefragt.

## Laufzeitabgleich für gebundene Renderziele

`IRenderTarget` stellt backendneutral `HasColor`, `ColorFormat`, `HasDepth`
und `HasStencil` bereit. `RenderTarget` gibt diese Angaben auch für ein
`AntialiasTarget` weiter. OpenGL-2D-Ziele liefern das Farbformat ihrer
angehängten Textur und die tatsächlich gesetzten Tiefen-/Stencilattachments.
OpenGL-MSAA liefert ein Farbformat nur für die explizit gewählten RGBA8- und
RGBA16F-Storageformate; beim Desktop-GL-`GL_RGBA`-Pfad bleibt es unbekannt.
Null-Ziele bestätigen nur die angeforderte Struktur und melden kein
GPU-Farbformat.

`SystemRenderer` prüft bei jeder Szenephase, dass ein gebundenes Wrapperziel
Farbe und Tiefe besitzt. Das deckt `RenderTarget2D` und die darunterliegende
AA-Zielkette ab. Zusätzlich gleicht `AntialiasTarget` beim Erzeugen die
aktiven Farb-/Tiefenattachments mit dem LDR-/HDR-Pfad ab und prüft bei SMAA
die tatsächlich angehängten Stencil-Puffer der Kanten- und Gewichtsziel-
Framebuffer. Ein vom Desktop-Treiber implementation-defined angelegtes
MSAA-Farbformat bleibt unbekannt und wird nicht aus dem angeforderten Format
abgeleitet. Die Swapchain (`RenderTarget == null`) bleibt außerhalb dieser
Abfrage; ihr tatsächliches Format und ihre Depth-/Stencilattachments sind
weiterhin nicht portabel abfragbar.

## Offene Architekturpunkte

- Backendneutraler Swapchainformatzugriff fehlt; `RenderTarget2D`-Ziele melden
  ihr bekanntes Format und die tatsächlichen Depth-/Stencilattachments. Das
  Format von Desktop-GL-MSAA-Storage und der Swapchain bleibt unbekannt.
  SMAA-Bgra8-Formate werden beim Zielaufbau geprüft. Die
  MSAA-Samplezahl ist als `ModeSelected` im Quellattachmentvertrag markiert und
  gegen den gewählten AA-Modus geprüft; die vom Treiber realisierte Zahl wird
  nicht separat abgefragt.
- Der MSAA-Offscreen-Pfad nimmt als Elternziel `RenderTarget2D`; ein anderer
  `RenderTarget`-Typ wird an dieser Stelle nicht allgemein unterstützt.
- Resize erzeugt das interne AA-Ziel bei geänderter Größe oder Qualität neu.
  Ressourcenlebensdauer, Sharing/Aliasing und Fehler beim Wechsel sind noch
  kein Render-Graph-Vertrag.

Offen bleiben die Swapchain-Format-/Attachmentabfrage, eine Abfrage des vom
Treiber gewählten Desktop-MSAA-internal-format, die Prüfung von Load-/Store-
Verhalten gegen API-Aufrufe und die Einbeziehung von Editor-/Offscreen-Zielen.
Die Szene-Passfolge ist nun laufzeitgeprüft, wird aber nicht durch einen
Render-Graph-Executor disponiert. Ein Graph müsste zusätzlich Load/Store,
Formate, Ressourcenlebensdauer und Resize-Abhängigkeiten ausführen.
