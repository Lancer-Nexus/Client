# Sonnenfarbene Umgebungsbeleuchtung: Datenpfad-Audit (2026-10-15)

## Befund

Die aktuelle Weltraumbeleuchtung führt drei getrennte Farbwerte:

1. `[Ambient] color` aus der jeweiligen System-INI wird in
   `GameItemDb.InitSystems()` nach `StarSystem.AmbientColor` geladen. `SystemRenderer.LoadLights()`
   kopiert diesen Wert unverändert in `SystemLighting.Ambient`; `RenderHelpers.ApplyLights()`
   reicht ihn als Ambient-Anteil an die Objektmaterialien weiter.
2. Jede `[LightSource] color` wird als `RenderLight.Color` geladen. Die bestehenden dynamischen
   Lichtpfade verwenden diesen Wert für direktes, gerichtetes/entferntes Licht.
3. `StarArch.ini`-Profile wie `star_glow`/`star_center` werden in `Sun` übernommen und von
   `SunRenderer` für die sichtbaren Sprites benutzt. Sie speisen `SystemLighting.Ambient` nicht.

Damit kann eine farbige Sonne sichtbar und als direktes Licht farbig sein, während der diffuse
Grundanteil unabhängig davon in der System-INI festgelegt bleibt. Ein opt-in Pfad ist inzwischen
implementiert: Er mischt 25% des vorhandenen Ambientwerts, multipliziert mit dem lokal
attentuierten gewichteten Mittel der persistenten Systemlichtfarben, in das Ambient ein. Die
Option ist standardmäßig aus. Nebula-Ambient wird anschließend wie bisher überschrieben.

## Gegenproben in der lokalen Retail-Datenreferenz

Die Daten sind hier nur lesend verglichen worden; es wurden keine Retail-Dateien kopiert.

- `DATA/UNIVERSE/SYSTEMS/HI02/hi02.ini`: Systemambient `40, 80, 50`, Sonnenobjekt
  `star = sm_red_sun`, Systemlicht `232, 80, 0`. Das rote Sonnenprofil, das orange direkte
  Licht und das grünliche Ambient sind drei unterschiedliche Vorgaben.
- `DATA/UNIVERSE/SYSTEMS/EW06/ew06.ini`: ein rotes Sonnenobjekt am Ursprung und ein weiteres
  Sonnenobjekt bei `(145000, 0, -130000)`; die beiden Systemlichter haben `255, 50, 0` und
  `0, 174, 50`. Ein einziger globaler Farbmittelwert würde beide lokalen Lichtumgebungen
  zusammenwerfen.
- `DATA/UNIVERSE/SYSTEMS/BR02/br02.ini`: weißes `med_white_sun` und weißes direktes Licht,
  aber Ambient `35, 30, 40`. Der bestehende Ambientwert trägt also auch unabhängig
  eingestellte Szenenstimmung.

`DATA/SOLAR/stararch.ini` definiert unter anderem `med_white_sun` und `sm_red_sun` mit
unterschiedlichen Glow-/Center-Profilen. Diese Sprites sind kein belastbarer Ersatz für
`LightSource`-Beleuchtungsdaten, da ein System mehrere Sterne und mehrere lokale Lichtquellen
haben kann.

## Implementierungsanforderungen

- Die Lichtfarbe muss pro Objekt beziehungsweise nach lokalem Beitrag ausgewertet werden. Ein
  pauschaler Systemmittelwert ist für EW06 nachweislich ungeeignet.
- Nur persistente Systemlichter dürfen den diffusen Sonnenanteil speisen. Kurzlebige Waffen-,
  Explosions- und Effektlichter dürfen die komplette Szene nicht global einfärben.
- Den vorhandenen `[Ambient]`-Wert als authored Helligkeits-/Stimmungsbasis erhalten; der
  Sonnenfarbanteil ist auf 25% begrenzt und besitzt einen separaten Renderer-Schalter. Der Wert ist
  ein vorläufiger Startpunkt und noch nicht visuell kalibriert.
- Nebel-Ambient und Feld-Ambient haben eigene Datenpfade. Ihre bisherigen Overrides dürfen nicht
  versehentlich durch die neue Systemfarbe ersetzt werden.
- Legacy-Verhalten muss bei ausgeschaltetem Feature bitgleich bleiben. Weiße Sonne dient als
  Kontrollfall; rote und grüne Quellen sowie EW06 mit mehreren Sonnen sind A/B-Fälle.
- Der passende Quellpfad ist `GameItemDb.InitSystems()` → `SystemRenderer.LoadLights()` →
  `RenderHelpers.ApplyLights()`. `StarGlow` bleibt für die Sprite-Darstellung zuständig.

## Beleggrenzen

Der Codepfad und die Retail-INI-Fälle sind statisch geprüft. Fünf fokussierte Regressionstests
bestehen: ausgeschaltetes Feature erhält Ambient exakt, rote/grüne/weiße Lichtfarben, lokale
Attenuationsgewichtung und kein Einfärben durch nicht-systemische/transiente Lichter. Testlauf:
`dotnet test src/LibreLancer.Tests/LibreLancer.Tests.csproj -c Release --no-restore
-p:UseSharedCompilation=false --filter FullyQualifiedName~SunAmbient` (5 bestanden, 0 fehlgeschlagen).

Ein NAP-Lauf mit vorbereitetem HI02-Spielstand lud die Simulation, erzeugte aber kein sichtbares
SDL-Fenster; der Prozess blieb mit wiederholten Simulation-Overload-Meldungen aktiv und wurde
beendet. Daher liegen weiterhin keine verwertbaren NAP-Laufzeitbilder, GPU-Renderproben oder
Retail-Pixelvergleiche vor. Der Release-Build belegt Kompilierbarkeit; visuelle Stärke,
Mehrfachsonnenübergänge und ausgeschalteter Legacy-Fallback brauchen noch eine sichtbare
Laufzeitabnahme.
