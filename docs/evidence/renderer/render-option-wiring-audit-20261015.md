# Renderer-Optionen: Verdrahtungs-Audit (15.10.2026)

## Ergebnis

Die vorhandenen Renderer-Optionen sind von der Konfiguration bis zu den
betroffenen Laufzeitpfaden verbunden. Dieses Audit bestätigt Quellcodepfade,
keine visuelle oder Performance-Abnahme.

- `GameSettings` enthält Persistenzschlüssel, Standardwerte, `MakeCopy`,
  `Apply`, `Write` und Validierung für die Surface-Map-Optionen, Lichtbudgets
  und Render-Schalter.
- `lancer --help` listet unabhängige Startparameter. `Program` parst die
  angegebenen Overrides und wendet sie nach dem Laden der Konfiguration auf
  `cfg.Settings` an. Der Startpfad ruft an dieser Stelle kein `Save()` auf;
  CLI-Overrides werden daher nicht als Konfigurationsänderung persistiert.
- `uixml/options.xml` stellt die Schalter dar. `options.lua` lädt sie aus
  Settings, übernimmt Eingaben, ruft `Game.ApplySettings` auf und speichert
  die Einstellungen. Die beiden Gameplay-Einstiegspunkte und das Hauptmenü
  wenden dieselben Renderer- und ResourceManager-Einstellungen an.
- Der Renderer liest die Werte an den jeweiligen Verbrauchsstellen: unter
  anderem `SystemRenderer`, `ParticleEffectRenderer`, `ProjectileRenderer`,
  `SunRenderer`, `AbstractWeapon` und `BasicMaterial`.

## Geltungsbereich und offene Abnahme

Abgedeckt sind Normalmaps, generierte Tangenten, Surface Roughness/Metallic,
Umgebungs-Cubemaps, Per-Pixel- und dynamische Beleuchtung, beide Lichtbudgets,
Nebel, Sternensphäre, Sonnen-Lens-Glow, Projektilstrahlen, Partikeleffekte,
temporäre Effektlichter und Waffen-Mündungslichter sowie AA/Anisotropie.

Der Quellcodeabgleich belegt weder, dass jeder Schalter in allen Szenen
