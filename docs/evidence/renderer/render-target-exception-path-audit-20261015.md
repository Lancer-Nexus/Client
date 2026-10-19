# Render-Target-Fehlerpfad-Audit

Stand: 2026-10-15. Quellcodeprüfung der normalen und ausnahmebedingten
Wiederherstellung von Viewport, Scissor und RenderTarget. Es wurde keine
künstliche Laufzeitausnahme injiziert.

## `SystemRenderer.Draw()`

`SystemRenderer.Draw()` gibt bei Breite/Höhe 0 vor Änderungen zurück. Bei
aktivem Antialiasing erstellt der Renderer ein passendes `AntialiasTarget`,
pusht Viewport und Scissor und bindet danach das MSAA-Target. Das reguläre
Aufräumen poppt beide Stacks, blitted das Ergebnis und setzt `restoreTarget`
wieder. Ein `finally` stellt RenderTarget, offene Viewport-/Scissor-Pushes,
DepthEnabled, DepthWrite, DepthFunction, DepthRange, ColorWrite und ClearColor
wieder her. Es schaltet eine aktive `ThnCamera.DefaultZ()`-Projektion zurück
und leert die Frame-Objektliste auch nach einem Draw-Fehler.

Quellstellen: `src/LibreLancer/Render/SystemRenderer.cs`,
`Draw()` Setup bei 409–425, THN-Projektionswechsel bei 560–606 und Ausnahme-
Wiederherstellung ab 669.

## `AntialiasTarget.SMAABlit()`

Der SMAA-Pfad merkt Texturen 0–2, Sampler 0–1, RenderTarget, Shader, Blend-,
Stencil-, Depth- und Cull-Zustand vor. Danach pusht er Viewport und Scissor, führt
drei Fullscreen-Draws aus und stellt den gespeicherten Zustand erst danach
wieder her. Ein `finally` stellt die gespeicherten Texturen, Sampler,
RenderTarget, Shader und Renderzustände wieder her und gleicht offene Scissor-
und Viewport-Pushes aus. Ein optionaler Offset-Push hat einen eigenen
Wiederherstellungspfad.

Quellstelle: `src/LibreLancer.Base/Graphics/AntialiasTarget.cs`,
`SMAABlit()`. Die Stack-Implementierungen für Viewport und Scissor
liegen in `src/LibreLancer.Base/Graphics/RenderContext.cs` bei 279–358.

## Aussage und nächster Schritt

Die betroffenen Ressourcen-/Zielzustände sind jetzt gegen Fehler zwischen Setup
und normaler Wiederherstellung geschützt. Das deckt die direkt in beiden
Methoden gespeicherten und veränderten Zustände ab, nicht beliebige Zustände
innerhalb benutzerdefinierter Hooks oder beliebiger Draw-Pfade. Die
Wiederherstellung wurde nicht mit injizierten Laufzeitfehlern geprüft;
reguläre Resize-/Minimize-/Restore-Läufe sind ebenfalls noch offen.

Nächster Schritt: Fehler zwischen den Draw-Pässen injizieren und reguläre
Resize-/Minimize-/Restore-Abläufe prüfen.
