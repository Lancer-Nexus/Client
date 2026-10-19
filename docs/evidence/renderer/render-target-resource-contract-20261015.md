# Render-Target-Ressourcenvertrag: Ist-Aufnahme (15.10.2026)

Dieser Vertrag ist aus `SystemRenderer.Draw()`, `AntialiasTarget` und dem
OpenGL-Backend abgeleitet. Er beschreibt den aktuellen Datenfluss; die
Ressourcen deklarieren diese Abhängigkeiten im Code noch nicht formal.

## Szenenrendering

| Schritt | Reads | Writes / Zustandswirkung |
|---|---|---|
| Frame-Clear | aktuelles Renderziel | Farbe, Tiefe und Stencil löschen; Clear-Farbe ist Hintergrund oder volle Nebelfarbe |
| Depth prepass | sichtbare Game-Objekte und deren Geometrie | Tiefenbuffer; Farbwrites deaktiviert, Depth `Less` |
| opaque commands | vorbereitete Objekt-, Beam-, Asteroiden- und Nebel-Kommandos | Farbe und Tiefe desselben Ziels; Depth `LessEqual` |
| Starsphere / Fog-Übergang | Sternmodelle, Kamera, optionale Nebelübergangsdaten | direktes Schreiben in das Szenen-Farbziel; Depth-Range temporär auf 1 |
| transparent commands | sortierte transparente Kommandos und Materialtexturen | Farbe im selben Ziel; Depth-Test aktiv, Depth-Write deaktiviert |
| Hooks / Debug | Hook-spezifische Daten und Debuggeometrie | aktuelles Szenenziel; Zustand hängt vom Hook-Vertrag ab |

Opaque und transparent sind getrennte CommandBuffer-Listen, aber keine getrennten
Render-Targets. Vorbereitung, Upload und Ausführung liegen innerhalb desselben
`SystemRenderer.Draw()`-Aufrufs.

## Antialiasing-Ziele

- Ohne AA schreibt die Szene in das zuvor aktive `RenderTarget` (oder das
  Bildschirmziel); `SystemRenderer` erzeugt dafür kein eigenes Szenenziel.
- MSAA verwendet im OpenGL-Backend eine multisample RGBA8-Farbe und einen
  Depth-24-Buffer ohne Stencil. `AntialiasTarget` resolved den Farbwert nach
  dem Szenenrendering ins Bildschirm- oder vorherige Renderziel.
- SMAA rendert die Szene in eine Bgra8-Textur mit Depth-Buffer. Der Edge-Pass
  liest diese Szene und schreibt in `smaaEdges`; der Weight-Pass liest Kanten
  sowie globale Area-/Search-Texturen und schreibt `smaaBlend`; der
  Neighborhood-Pass liest Szene und Blend-Gewichte und schreibt ins
  Bildschirm- oder vorherige Renderziel. `smaaEdges` und `smaaBlend` teilen
  den Depth-/Stencil-Buffer; der SMAA-Zielpfad schreibt die finale Farbe ohne
  Szenen-Tiefenattachment.

## Was eine formale Pass-API zusätzlich festlegen muss

Die aktuelle API deklariert keine Reads/Writes, Load-/Store-Operationen,
Attachment-Formate, Größenabhängigkeiten oder Lebensdauern. Der Renderer muss
die Zielauswahl und Zustandsübergänge implizit koordinieren. Vor einem
Render-Graph sind daher mindestens Ressourcenformat und Samplecount,
Depth-/Stencil-Verwendung, Clear-/Preserve-Verhalten, Resolve-Ziel und
