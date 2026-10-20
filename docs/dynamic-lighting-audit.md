# Audit: dynamisches Licht für Effekte

Stand: 2026-10-16. Der Renderer unterstützt statische Systemlichter,
THN-Lichter, Lichtequipment, animierte Waffen-Mündungslichter und kurzlebige
Lichter an Effekten, die eine `lgt_effect`-Kurve und `lgt_radius` angeben.
Normale Schiffstriebwerke und angehängte Fuse-Effekte werten die expliziten
Lichtdaten an ihren Hardpoints aus. Die 45 NAP-Engine-Feuerprofile verwenden die Kurve
`engine_fire_warm`; kleine und große Feuer erhalten `lgt_radius` 7,5 bzw. 25.
Auch `gf_continuous_fire`, das von 52 Fuse-Aktionen gestartet wird, nutzt die
Kurve mit Radius 20. Diese Werte ergänzen die Daten des ursprünglichen
Inventars vom 2026-10-14; sie benötigen noch eine visuelle Laufzeitkalibrierung.
Die Kurve wiederholt sich nur für persistente angehängte Effekte.

## Verifizierter Bestand

- `SystemRenderer.LoadLights()` übernimmt die Lichtquellen eines Systems;
  `Update()` aktualisiert deren Farbkurven. `BeginFrame()` kopiert diese
  Quellen in die Frame-Liste; lokale Lichtquellen werden pro Frame ergänzt.
- `RenderHelpers.ApplyLights()` filtert nach Lichtgruppe und Objektabstand und
  wählt bis zu acht passende Lichter nach geschätztem Beleuchtungsbeitrag aus:
  diffuse/ambient Helligkeit multipliziert mit der Punktlicht- oder
  Kurvenattenuation am objektseitig nächsten Punkt. Die Shader unterstützen
  neun Slots, von denen ein weiterer für Nebelblitze vorgesehen ist. Der
  vorgelagerte Frame-Pool priorisiert bis zu 32 transiente Quellen nach ihrem
  geschätzten Beitrag am Kamerapunkt und verwirft Beiträge außerhalb der
  Reichweite oder ohne positive Intensität. Das ist eine Vorauswahl, keine
  objektspezifische Bewertung aller sichtbaren Lichtquellen. Für `LightEquipment`
  prüft `PrepareRender` Frustum und Physik-Occlusion; Effektlichter besitzen
  keine entsprechende allgemeine Sichtbarkeits-/Occlusion-Priorisierung.
- `Settings.DynamicLighting` schaltet den bestehenden Beleuchtungspfad global
  ein oder aus.
- `Settings.DynamicLightBudget` begrenzt die pro Frame zugelassenen lokalen
  transienten Quellen auf 0, 8, 16, 24 oder 32. Die Startoption
  `--render-dynamic-light-budget` überschreibt diesen Wert; persistente
  Systemlichter zählen nicht zu diesem Budget.
- `Settings.TransientEffectLightBudget` begrenzt gleichzeitig aktive
  `lgt_effect`-Lichtkurven auf 0, 32, 64 oder 128. Die Startoption
  `--render-transient-effect-light-budget` überschreibt den Wert. Beim
  Absenken werden die nach geschätztem Kamerabeitrag schwächsten aktiven
  Lichter entfernt; ihre Partikeleffekte und Simulation laufen weiter.
- Die Grafikoptionen im Ingame-Effekte-Tab ändern dynamische Beleuchtung,
  Effektlicht-/Mündungslicht-Schalter, beide Budgets, Nebel, Sternenhintergrund,
  Lens Glow, Projektileffekte und Partikeleffekte. Die geänderten Einstellungen
  werden beim Zurückkehren gespeichert; die Renderer-Schalter wirken live.
- `LightEquipment.EmitRange` und `EmitAttenuation` werden geparst und vom
  `LightEquipRenderer` als lokale Frame-Lichtquelle registriert. `PrepareRender`
  prüft für die Lichtquelle separat den EmitRange-Frustrum und die aktive
  Blinkphase. Billboard-Sichtkegel und Billboard-Frustum begrenzen nur das
  sichtbare Glühbild; ein blockierter Kamera-Licht-Ray unterdrückt zusätzlich
  die Quelle, damit sie ohne Schatten nicht durch Geometrie leuchtet.
  `SystemLighting.AddFrameLight()` begrenzt die zusätzlichen
  Kandidaten auf 32 und wählt bei Überlast anhand der in `RenderHelpers`
  geschätzten Lichtbeiträge am Kamerapunkt; ein stärkerer Kandidat ersetzt den
  schwächsten. Das bewertet nicht die tatsächlich beleuchteten Objekte. Die
  nachfolgende objektspezifische Auswahl bewertet Quellen erneut am
  objektseitig nächsten Punkt.
- Sichtbarkeitspfad (Quellcode-Audit 2026-10-15): Die Frame-Lichtquelle wird
  getrennt von der Billboard-Sichtbarkeit eingetragen. Sie bleibt aktiv, wenn
  ihr `emit_range`-Kugelvolumen das Kamerafrustrum schneidet und die blinkende
  Lichtphase aktiv ist, auch wenn `flare_cone` oder Billboard-Frustum das
  Glühbild ausblenden. Ein blockierter Kamera-zu-Licht-Ray unterdrückt sowohl
  Glühbild als auch Lichtquelle, um unverschattetes Licht hinter Geometrie zu
  vermeiden. `SystemLighting` filtert anschließend Reichweite und Budget.
  Retail-Bildabnahme dieser Trennung sowie objektbezogene Schatten-Occlusion
  bleiben offen.
- `Effect` enthält `lgt_effect`, `lgt_range_scale` und `lgt_radius`; `InitEffects()`
  übergibt Kurve und beide Zahlenwerte nun an `ResolvedFx`.
- `FreelancerData` lädt nun die benannten Kurven aus Retail
  `FX\\lightanim.ini`; `GameItemDb` löst `Gun.light_anim` auf und übergibt die
  Kurve samt `flash_radius` an Gun- und Missile-Launcher-Equipment.
  `AbstractWeapon` registriert beim Schuss pro Feuer-Hardpoint ein Licht,
  wertet die Farb-/Intensitätskurve über deren Dauer aus und nutzt
  `flash_radius` als Reichweite.
- Waffenfeuer startet weiterhin die vorhandenen
  `ParticleEffectRenderer`-Mündungsblitze und zusätzlich die aus `light_anim`
  gespeisten dynamischen Mündungslichter.
- `GameWorld.SpawnTempFx()` übergibt die aufgelöste `LightAnim`-Kurve und den
  `lgt_radius` an `SystemRenderer`. Dieser interpretiert `lgt_radius` derzeit
  als direkte Lichtreichweite; das ist eine Arbeitsannahme. `lgt_range_scale`
  wird bis zum transienten Lichtobjekt durchgereicht und dort unverändert
  aufbewahrt, aber noch nicht angewendet, bis seine Bedeutung verifiziert ist.
- `lgt_time_scale` ist im Retail-Effect-Arch-Pfad als separates Feld mit
  Default `1` belegt. LibreLancer parst es optional und verwendet es für die
  Abspielgeschwindigkeit und Lebensdauer des transienten Effektlichts. Der
  Partikeleffekt selbst wird dadurch nicht zeitlich skaliert.
- Im vollständigen NAP-Effekt-INI-Bestand stehen `lgt_effect`,
  `lgt_range_scale` und `lgt_radius` nur in `EffectsExplosion.ini` und
  `EffectsExplosionNew.ini`. Projektiltreffer, Schiff-/Munitions-/Minenexplosionen
  und CMP-Abtrennungen führen ihre `ResolvedFx` über `GameWorld.SpawnTempFx()`
  und damit durch denselben transienten Lichtpfad.
- Dauerhafte, hardpointgebundene Fuse-Effekte laufen separat über
  `SFuseRunnerComponent` und `CNetEffectsComponent`; sie werden an Schiffe
  angehängt. `CNetEffectsComponent` reicht nun aufgelöste `lgt_effect`,
  `lgt_radius` und `lgt_time_scale` an den jeweiligen `ParticleEffectRenderer`
  weiter. Dieser aktualisiert die Lichtposition am Objekt-/Hardpoint-Transform
  und registriert die Quelle pro Frame über den vorhandenen, budgetierten
  `SystemLighting`-Pool. Die Kurve wird mit dem konfigurierten Zeitmaßstab
  wiederholt ausgewertet, damit dauerhaft laufendes Feuer nicht nach einem
  einzigen Kurvendurchlauf auf dem letzten Wert stehen bleibt. Entfernen des Netzwerk-FX entfernt damit auch die
  Quelle; `render_transient_effect_lights` schaltet sie mit den übrigen
  Effektlichtern. Das belegt den Daten- und Runtime-Pfad, noch keine visuelle
  Lichtwirkung in einer NAP-Kampfszene.
- Die lokalen `Engine`-Renderer für `TrailEffect`, `TrailEffectPlayer` und
  `FlameEffect` reichen die aufgelösten Lichtdaten nun ebenfalls an die
  Renderer der jeweiligen `hpengine*`-Hardpoints weiter. Zuvor kannten diese
  Renderer nur das visuelle ALE und erzeugten trotz INI-Lichtdaten kein Weltlicht.
- Die Partikel-Mündungsblitze von Guns und Countermeasure-Launchern verwenden
  ebenfalls die Lichtdaten ihres `ResolvedFx`. Sie starten beim Schuss neu,
  folgen je `hpfire`-Hardpoint und werden über denselben Effektlichtschalter
  und dasselbe Ereignis-/Framebudget verwaltet. Das ist unabhängig vom bereits
  vorhandenen `Gun.light_anim`-Mündungslicht samt eigenem Schalter.
- Weitere direkte `ResolvedFx`-Renderer behalten die Lichtmetadaten nun bei:
  Jumpgate-Ring-/Tunnel-Effekte, THN-PSys, konstante Raketen- und
  Munitionsspuren sowie ausgerüstete `EffectEquipment`-Effekte. Hardpoint- und
  Objektbindungen bleiben beim jeweiligen Aufrufer; alle registrieren ihr
  Licht über den gemeinsamen, budgetierten Effektlichtpfad.
- Ausgerüstete `EffectEquipment`-Effekte verwenden denselben expliziten
  Lichtpfad: `AttachedEffect` übergibt `ResolvedFx`-Kurve, `lgt_radius` und
  `lgt_time_scale` an den Renderer. Die Quelle folgt dem Equipment-Hardpoint
  und unterliegt `render_transient_effect_lights` sowie dem Framebudget.
- Kurzlebige Effektlichter teilen sich ein deterministisches, konfigurierbares
  Limit von bis zu 128 aktiven Ereignissen. Der Frame-Lichtpool bewertet diese wie weitere
  transiente Quellen anhand des geschätzten Beitrags am Kamerapunkt; für
  Effektlichter gibt es keine separate Sichtbarkeits-/Occlusion-Priorisierung.
- THN-Lichter werden direkt in `SystemLighting.Lights` registriert. Sie nutzen
  also die bestehende globale Lichtliste, aber keinen kurzlebigen Effektpool.

## Umsetzung in Arbeitsschritten

1. Die Bedeutung von `lgt_radius` und `lgt_range_scale` anhand einer belastbaren
   Engine-Referenz oder einer kontrollierten Retail-Beobachtung verifizieren.
   Danach `lgt_range_scale` entsprechend anwenden. Die Parser-/Merge-Spur ist
   dokumentiert; ein Runtime-Verbrauch ist weiter offen.
2. [x] Laufzeitlichter an zeitlich begrenzte Treffer und Effekte anbinden,
   sofern deren Daten Farbe, Reichweite und Dauer liefern. Alle aktuell
   lichtbelegten NAP-Explosionen sowie die geprüften Projektiltreffer,
   Objekt-/Munitions-/Minenexplosionen und CMP-Abtrennungen laufen über den
   gemeinsamen `GameWorld.SpawnTempFx()`-Pfad. Persistente, hardpointgebundene
   Fuse-Effekte und lokale Engine-Feuer werten zusätzlich ihre Lichtdaten am
   jeweiligen Hardpoint aus.
3. [ ] Datengetriebene Lichtwerte für Feuer und weitere Effekte definieren.
   [x] Read-only-Inventar der NAP-Feuer- und Lichtfelder erstellt; siehe
   [Feuerinventar](evidence/effect-fire-light-input-inventory-20261014.json).
   [x] 45 NAP-Engine-Feuerdefinitionen erhalten eine orange Lichtkurve und
   die bereits in `EffectTypes.ini` definierten Radien 7,5/25. Der
   Datenbestand ist damit bestückt; die Laufzeitbilder müssen Farbe und
   Reichweite noch gegen eine reproduzierbare Szene kalibrieren.
   [x] Der Runtime-Pfad wertet vorhandene `lgt_*`-Daten für persistente,
   hardpointgebundene Netzwerk-FX und lokale Engine-Trail-/Flame-Renderer aus.
   Die Quelle folgt Objekt-/Hardpoint-Transform und Rendererlebenszyklus;
   persistente Kurven werden wiederholt. Das Hi02-Bildpaar dokumentiert den
   NAP-Start mit dem Schalter an/aus, isoliert den Lichtbeitrag wegen der
   weiterlaufenden Kampfszene aber nicht. `lgt_range_scale` bleibt ungeklärt.
4. Lebensdauer, Ausschalter, die auswählbare Frame-Budgetstufe, Lichtgruppen und deterministische
   Überlastfälle mit Tests abdecken. Danach typische Kampf- und Stationsszenen
   messen und mit NAP-Daten visuell abnehmen.

## Datenabgleich `lgt_range_scale` / `lgt_radius`

Read-only-Abgleich vom 2026-10-14 zwischen Retail, SDK und den entsprechenden
NAP-Eingaben:

- Retail `DATA/FX/effects_explosion.ini` enthält 25 Effektabschnitte mit
  Lichtparametern. Alle verwenden `elite_flash_to_red`; `lgt_range_scale` und
  `lgt_radius` sind in jedem Abschnitt gleich: 22-mal `30`, je einmal `50`,
  `70` und `150`.
- Die SDK/NAP-Dateien `EffectsExplosion.ini` und `EffectsExplosionNew.ini`
  enthalten zusammen 50 Abschnitte. Ihre Verteilung ist genau doppelt so groß
  wie Retail: 44-mal `30`, zweimal `50`, `70` und `150`. Die beiden NAP-Dateien
  stimmen bytegenau mit den gleichnamigen SDK-Dateien überein.
- Retail- und NAP-`Lightanim.ini` haben denselben Inhalt bis auf eine zusätzliche
  leere Schlusszeile in Retail. Die Kurve `elite_flash_to_red` enthält damit
  die Datenbasis für den Lichtverlauf, aber keine Information, die zwischen
  Radius und Skalierung unterscheidet.

Weil beide Felder in allen vorhandenen Retail-Explosionen denselben Zahlenwert
tragen, lässt sich aus dem Assetbestand nicht ableiten, ob `lgt_range_scale`
einen Radius skaliert oder einen anderen Effektparameter steuert. `lgt_radius`
weiterhin als direkte Reichweite zu verwenden und `lgt_range_scale` zu ignorieren
bleibt deshalb vorläufig. Nächste Evidenz muss aus dem Retail-Enginepfad oder
einer kontrollierten Sicht-/Pixelmessung mit absichtlich unterschiedlichen
Werten stammen; vor einer Runtime-Änderung ist das Feldverhalten zu verifizieren.

Die separate Retail-Parseranalyse weist `lgt_time_scale` auf Objekt-Offset
`+0x44` nach. Die benachbarten Werte `lgt_range_scale` (`+0x40`) und
`lgt_radius` (`+0x48`) werden ebenfalls eingelesen und beim Effekt-Merge
kopiert. Der belegte Pfad endet bei Parser, Initialisierung und Merge; ein
nachgelagerter Retail-Renderer-Verbrauch für `lgt_range_scale` ist nicht
nachgewiesen. Daher wird dieses Feld weiterhin nicht interpretiert.

Der Aufruf zur Registrierung des geparsten Effektobjekts führt zu
`FUN_0046d6d0`, einer generischen Rot-Schwarz-Baum-Einfügefunktion mit
Aufrufern aus mehreren Datenbereichen. Diese Spur bestätigt die Ablage des
Effekts nach Nickname, zeigt aber keinen späteren Verbraucher der Lichtfelder.
Ein `lgt_range_scale`-Verbrauch bleibt damit unbelegt.

## Feuer-Effekt-Datenbestand

Der read-only-Abgleich mit
[NAP-Effektinventar](evidence/effect-fire-light-input-inventory-20261014.json)
findet im Stand vom 2026-10-14 150 `lgt_*`-Feldeinträge in zwei Dateien: den beiden
`EffectsExplosion*.ini`-Dateien. `Effects.ini` enthält 25 visuelle
Engine-Feuer-Effekte; 52 Fuse-INI-Aktionen starten `gf_continuous_fire` an
Hardpoints. Seitdem enthalten die 45 Engine-Feuerprofile und
`gf_continuous_fire` explizite Lichtkurven und Radien in SDK und NAP.

`EFT_ENGINE_FIRE` und `EFT_ENGINE_LARGE_FIRE` nennen `radius` 7,5 bzw. 25 und
`run_time = -1`. LibreLancer parst diese `EffectType`-Felder, verwendet sie aber
im aktuellen Runtime-Code nicht. Sie belegen daher weder eine Punktlicht-
Reichweite noch einen Licht-Lebenszyklus. ALE-Partikelfarben und -größen sind
ebenfalls keine explizite Freigabe, daraus Weltlichter abzuleiten.

Prüfsummen der verglichenen Quellen:

| Datei | SHA-256 |
|---|---|
| Retail `DATA/FX/effects_explosion.ini` | `e52bb826ddd527bc80c6a0c8e0e6b7a8cf2d3516350813ad8362d83e09c94872` |
| SDK `fx/EffectsExplosion.ini` = NAP `Effects/fx/EffectsExplosion.ini` | `34eb9149416e03d9b9b2f1ac08998080516d58aa8ac96f43716c03a336d9a32e` |
| SDK `fx/EffectsExplosionNew.ini` = NAP `Effects/fx/EffectsExplosionNew.ini` | `960387a2a2cf4d3496ed738e5ef51f253a7624653c47c4df6a3f84c0cfda993e` |
| Retail `DATA/FX/lightanim.ini` | `e62724ff273a8027c3a91c805e8dd07fbbd35fb3b9d1dcf0286e66d968b2a0d5` |
| NAP `Effects/fx/Lightanim.ini` | `a92c90eadc90602111d3c0953f9c891945fc6bac8db9565e5cedd36721fbf3ba` |

## Offene Abnahme

Es gibt noch keine festgelegten Referenzwerte für Reichweite, Abklingzeit oder
Maximalbudget pro Qualitätstufe. Diese Werte müssen anhand vorhandener Effekte
und reproduzierbarer Szenen bestimmt werden. Derzeit liegt lokal kein fertiges
NAP-Paket für den vorgeschriebenen In-Game-Lauf vor.
