# HDR lineare Szenenausgabe (2026-10-16)

## Umsetzung

- `RenderContext` erkennt das aktive HDR-`AntialiasTarget` und bindet dessen
  Zustand über Uniformblock 6 an jeden Shader, der den gemeinsamen
  `OutputColor.hlsl`-Vertrag verwendet. Der Zustandswert wird pro Shader nur
  beim Wechsel zwischen HDR- und LDR-Ziel aktualisiert.
- Alle 18 Client-Fragmentshader, die Farbwerte in die Szene schreiben, verwenden
  `OutputColor.hlsl`. Die 17 Legacy-Shader geben mit `ToSceneOutput` im HDR-Ziel
  RGB-Werte per sRGB-EOTF linear aus, bevor Fixed-Function-Blending stattfindet;
  der LDR-Pfad lässt sie unverändert. PBR benutzt `ToDisplayOutput`, um seine
  lineare Beleuchtungsfarbe abhängig vom aktiven Ziel linear oder legacy-kodiert
  auszugeben.
- Der PBR-Shader berechnet bereits lineare Beleuchtungswerte. Er schreibt diese
  ohne Gamma-Kodierung in HDR-Ziele und behält für das normale LDR-Ziel die bisherige
  Gamma-2.2-Ausgabe bei.
- `ClearAll` und `ClearColorOnly` dekodieren die eingestellte Clear-Farbe für ein
  aktives HDR-Ziel und stellen danach den ursprünglichen Renderzustand wieder her.
- `Tonemap.frag.hlsl` nimmt nun lineare RGB-Werte an, wendet Belichtung und ACES
  an und kodiert das Ergebnis mit der sRGB-OETF. SMAA erhält weiterhin das bereits
  tonemappte LDR-Bild; MSAA löst vor dem Tonemapping in ein Half-Float-Ziel auf.
- `SpaceGameplay.Draw` rendert zuerst den `SystemRenderer`, danach HUD und UI. Die
  bestehende Reihenfolge setzt UI und Typewriter nach dem Tone-Map-Pass zusammen.

## Grenzen und Prüfung

Legacy-Lichtberechnung und Farbmultiplikation verwenden an einigen Stellen weiterhin
kodierte Diffuse-, Material- und Vertexfarben. Diese Änderung macht Shader-Ausgabe,
Clear und Zielblending linear; sie konvertiert noch nicht alle Eingaben vor der
Beleuchtungsrechnung. Das ist deshalb kein vollständiger linearer Beleuchtungsweg.

Der Release-Build mit .NET 10 (`/opt/dotnet-10`) lief erfolgreich mit 0 Fehlern
und 163 Warnungen aus dem bestehenden Projekt. Die frische Patch-Rekonstruktion wird
nach Registrierung des Bundles erneut geprüft. Es gibt derzeit keine NAP-Archive unter `Data/NAP`, daher wurde kein visueller
HDR-Lauf und keine Screenshot-Abnahme ausgeführt.
