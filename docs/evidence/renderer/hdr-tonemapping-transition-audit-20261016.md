# HDR-/Tonemapping-Übergangsaudit (2026-10-16)

## Umfang

Quellcodeprüfung des vorhandenen OpenGL-Spielpfads für Scene-Color-Format,
Multisample-Auflösung und Shader-Ausgabefarben. Es wurden keine Rendererdateien
geändert und kein visueller HDR-Lauf ausgeführt.

## Befunde

1. `SurfaceFormat.HdrBlendable` und `HalfVector4` werden im OpenGL-Backend als
   `GL_RGBA16F` angelegt ([EnumConversions.cs](../../../src/LibreLancer.Base/Graphics/Backends/OpenGL/EnumConversions.cs)).
   Das allgemeine `Texture2D`-Konstrukt verwendet standardmäßig jedoch
   `SurfaceFormat.Bgra8` ([Texture2D.cs](../../../src/LibreLancer.Base/Graphics/Texture2D.cs)).
   Das belegt Texturspeicher-Unterstützung, aber noch keine vollständige
   renderbare HDR-Pipeline oder Gerätefähigkeit.

2. `SystemRenderer.Draw()` rendert ohne AA in das Caller-Ziel. Mit AA zeichnet
   es direkt in `AntialiasTarget` und kopiert/löst anschließend in das
   Caller-Ziel auf ([SystemRenderer.cs](../../../src/LibreLancer/Render/SystemRenderer.cs)).
   Der SMAA-Pfad legt Szene-, Kanten- und Gewichtstexturen standardmäßig als
   BGRA8 an; der OpenGL-MSAA-Pfad verwendet RGBA8 auf GLES bzw. ein
   implementation-defined Format auf Desktop-GL. Sein Resolve-Target wird mit
   einem 8-Bit-Texeltyp angelegt ([AntialiasTarget.cs](../../../src/LibreLancer.Base/Graphics/AntialiasTarget.cs),
   [GLMultisampleTarget.cs](../../../src/LibreLancer.Base/Graphics/Backends/OpenGL/GLMultisampleTarget.cs)).

3. Die Shader definieren noch keine gemeinsame lineare Szenenausgabe. Der
   PBR-Fragmentshader kodiert seinen berechneten Farbwert explizit mit
   `pow(color, 1/2.2)` ([PBR.frag.hlsl](../../../src/LibreLancer/Shaders/PBR.frag.hlsl)).
   Der Legacy-Pfad liefert dagegen über die bestehenden Beleuchtungsfunktionen
   direkt sein Ergebnis; eine gemeinsame lineare/sRGB-Grenze für Legacy-
   Materialien, Effekte, Nebel und Clears ist nicht festgelegt.

4. In den geprüften OpenGL-Backenddateien gibt es keine Aktivierung eines
   sRGB-Framebuffer-Konversionspfads (`GL_FRAMEBUFFER_SRGB`) und keinen
   Tonemapping-Pass. Ein Quellcode-Suchtreffer für `HdrBlendable` oder
   `GL_RGBA16F` ist daher kein Laufzeitnachweis für HDR.

## Konsequenz und Implementierungsreihenfolge

HDR braucht einen durchgängigen Farbvertrag. Ein isolierter `RGBA16F`-Target-
Tausch würde Legacy- und PBR-Materialien, Alpha-Blending und Antialiasing mit
unterschiedlichen Farbannahmen mischen.

1. Eingangs- und Ausgangsfarbräume festlegen; Legacy-Texturen, PBR-Texturen,
   Emission, Fog, transparente Effekte und Clear-Farben explizit an den
   Szenenfarbraum anbinden.
2. Ein renderbares Half-Float Scene-Color-Target samt Capability-Prüfung und
   fallbackfähigem Resize-/Lifecycle-Verhalten einführen. MSAA muss denselben
   linearen Formatvertrag erhalten; seine Auflösung darf nicht vorzeitig auf
   8 Bit quantisieren.
3. Exposure/Tonemapping in einen definierten LDR-Ausgabepass integrieren.
   SMAA wird auf dem tonemapped Bild ausgeführt; UI und Editor-Overlays werden
   nach dem Tonemapping komponiert.
4. Bloom danach als abschaltbare HDR-Downsample-/Blur-/Composite-Kette ergänzen.
   Der unveränderte Legacy-Pfad bleibt ein Vergleichs- und Rückfallprofil.
5. Erst nach dieser Reihenfolge dieselbe feste Retail-Szene in Legacy/HDR und
   None/SMAA/MSAA aufnehmen, visuell prüfen und GPU-/Speichermehrkosten messen.

Dieser Audit schließt P4 nicht ab: Scene-Target, Farbkonversion, Tonemapping,
Bloom, Capability-Fallback und Laufzeitabnahme sind weiterhin offen.
