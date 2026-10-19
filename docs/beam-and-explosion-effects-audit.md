# Audit: Beam- und Explosionseffekte

Stand: 2026-10-14. Dieses Audit verfolgt die derzeitigen Runtimepfade und den
lokalen NAP-Eingabebestand. Es nimmt keine Änderung an Effektgeometrie,
Gameplay-Timing oder Originalwerten vor.

## Beam-Effekte

- `FreelancerData` parst `BeamBolt` und `BeamSpear` aus
  `DATA/FX/BeamEffects.ini`. Die Felder `head_brightness`, `trail_brightness`
  und `flash_size` sind im Schema vorhanden.
- `ProjectileRenderer.Draw()` reicht aktive Projektile an
  `BeamsBuffer.AddBeamBolt()` oder `AddBeamSpear()` weiter. Der Buffer erzeugt
  dynamische Geometrie und reicht sie als `ProjectileMaterial`-Draw-Call ein.
- `head_brightness` und `trail_brightness` werden auf die Vertex-Alpha der
  jeweiligen Kopf- und Trailbereiche angewendet. Fehlende Helligkeitswerte
  behalten den bisherigen Vollintensitäts-Default.
- `flash_size` wird von LibreLancer im Beam-Renderer aktuell nicht ausgewertet.
- `BeamsBuffer` begrenzt Bolt- und Spear-Geometrie jeweils auf 500 Elemente pro
  Buffer. Beams außerhalb des Kamerafrustums belegen keinen Slot. Bei voller
  Liste ersetzt ein sichtbarer Beam mit höherer Nähewirkung den bisher
  schwächsten Kandidaten; die Priorität nutzt eine konservative Kugel um die
  Geometrie. Ein Min-Heap hält den schwächsten Slot verfügbar und reduziert
  die Ersetzungsauswahl auf logarithmische CPU-Arbeit. GPU-Puffer und feste
  Obergrenze bleiben bestehen. GPU-Zeit und
  Bildqualität bei Überlast müssen noch in reproduzierbaren Szenen gemessen
  werden.

Der aktuelle NAP-Eingang `Data/NAP/Effects/fx/BeamEffects.ini` enthält 55
Definitionen: 51 `BeamSpear` und 4 `BeamBolt`. Alle haben `flash_size`; 47 Werte
sind `3`, 6 sind `9`, einer ist `8` und einer ist `4`. Die INI-Daten belegen
damit die breite Nutzung des Parameters, aber nicht seine Laufzeitsemantik.

## Retail-Parserbefund

Ein lokaler Ghidra-12.1.4-Auszug des Retail-Clients (`Freelancer.exe`, SHA-256
`ea4cb797acb1a31a7841f67200fb022a056af4df7b2f7216f8ae6424cb6d1fe9`) präzisiert
den Parserpfad:

- `FUN_0042e850` behandelt die Abschnitte `BeamSpear` und `BeamBolt` und reicht
  Werte an `FUN_0042e4a0` weiter.
- `FUN_0042e4a0` erkennt `flash_size`, liest einen Float und speichert ihn bei
  Offset `+0x78` des Beam-Datensatzes. Retail liest den Wert also tatsächlich
  in sein Laufzeitmodell ein.
- `FUN_004fa270` liest denselben Beam-Datensatz bei Offset `+0x78` unter einer
  Bedingung: `param_3 == 0`, Flag-Bit `8` an `param_1+0x70` und ein falsches
  Ergebnis von `FUN_0054bc80()`. Der Wert ersetzt in diesem Zweig den zuvor aus
  `core_width` gelesenen Wert und fließt in die Berechnung von Beam-Vertex-
  Positionen ein, unter anderem zusammen mit `head_width` und `tip_length`.
- Der Vergleichswert für `param_3` liegt bei `0x005c7474` und ist im Retail-
  PE `0.0`. Die Aufrufinstruktionen bei `0x004fa258` übergeben zwar explizit
  `0.0`, aber als zweiten Geometrieparameter (`param_2`); `param_3` bleibt der
  zuvor berechnete Countdownwert. Der Wrapper verwirft den Nullwert dieses
  Countdownparameters bereits vor dem Rendereraufruf. Der konkrete
  `flash_size`-Zweig in `FUN_004fa270` ist damit für diesen Wrapperpfad nicht
  belegt; ein anderer erreichbarer Pfad mit `param_3 == 0` wurde noch nicht
  gefunden. Die statischen Aufruferdetails stehen in [Callsite-Fenstern](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/Freelancer-beam-renderer-call-windows.md>) und [Renderer-Referenzen](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/Freelancer-beam-renderer-callees.md>).
- Der Wrapper `FUN_004fa0a0` berechnet einen Geometriefaktor aus
  `this+0x78` und `this+0x80`: solange der erste Wert kleiner ist, wird der
  Quotient verwendet, danach `1.0`. `FUN_004f9c20` addiert sein Updateargument
  auf `this+0x78`. Dieses Argument kommt aus dem Effect-Frame-Update
  `FUN_004f7480`: der Framewert wird auf höchstens `0.1` begrenzt und von
  `FUN_004f7810` an den virtuellen Effect-Update-Slot `+0x18` weitergereicht.
  Derselbe Wert wird dort für `Position += Velocity * delta` benutzt; das
  belegt einen Frame-Zeitwert in Sekunden. Das Feld `this+0x7c` wird als
  Countdown geführt: `FUN_004f9980` begrenzt es auf `DAT_006129cc = 0.3 s`,
  `FUN_004f9c20` zieht denselben Framewert ab, und `FUN_004fa0a0` leitet bei
  positiver Restzeit `this+0x7c / 0.3` als dritten Rendererwert weiter; sonst
  bleibt dieser Wert `1.0`. Der vollständige Callsite-Pfad, der den
  `flash_size`-Zweig mit drittem Argument null erreicht, ist noch offen.
- `FUN_004fc8b0` setzt `this+0x80` auf
  `(core_length + tail_length + 7.5) / speed`; ohne Effektdefinition ist der
  Zähler `7.5`. `speed` ist der Betrag des Vektors, den `FUN_0053f340` über
  VTable-Offset `+0x54` vom Objekt in Wrapperfeld `+0x10` abfragt. Der Pfad
  prüft dort Klasse `0x67`; `CObject::Alloc(0x67)` erzeugt einen `CBeam`, dessen
  VTable-Slot `+0x54` auf `CBeam::get_velocity` zeigt. Diese Methode liefert
  die Felder `+0xf8..+0x100`; `CBeam::init_physics` kopiert seinen ersten
  Vector-Parameter dorthin. `CObject::get_velocity` und dessen Physikzeiger
  `+0x54` bestimmen diesen dynamischen Aufruf also nicht. Welche Routine den
  Kontextvektor initialisiert und welcher Vektor übergeben wird, ist noch
  offen. `this+0x80` ist eine Dauer für die Beam-Strecke bei diesem Objektvektor,
  nicht eine Summe aus Länge und Geschwindigkeit. Bei Bit `0x02` oder
  Fortschrittsfaktor `0` setzt der
  Wrapper den zweiten Geometrieparameter auf `0`; den dritten Countdownwert
  übergibt er separat. Archetypwert `0` führt zu `FUN_004fa270`,
  Wert `1` zu `FUN_004fb1e0`. Exporte: [CObject-Vftable](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/common-CObject-vtable-slots.txt>), [CObject::get_velocity](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/common-CObject-get_velocity.md>) und [CArchGroup::GetVelocity](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/common-CArchGroup-GetVelocity.md>).
  [Wrapper-Decompilierung und Metadaten](/home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-15-flash-size-trace/Freelancer-beam-wrapper-004fa0a0.md)
  dokumentieren den erzwungenen Funktionsanfang bei `0x004fa0a0`; Ghidra hatte
  dort zuvor Instruktionen, aber keinen Funktionsdatensatz.
- MSVC-RTTI bestätigt `BeamEffect : Effect`; sein Vftable-Kandidat beginnt bei
  `005d9dac`, Slot `+0x1c` ist `FUN_004fa0a0`. Der BeamEffect-Konstruktor
  `FUN_004f9540` setzt dieses Vftable und initialisiert `this+0x70` auf null.
  Der zuvor zugeordnete Konstruktor `FUN_004f8d00` gehört laut RTTI zu
  `CameraParticles : Effect` und setzt dessen separate Vftable `005d9d84`.
  Die BeamEffect-Methoden prüfen Bits `0x01`/`0x02`; `FUN_004fa270` prüft
  zusätzlich `0x08`. `flash_size` greift nur bei `param_3=0`, Bit `0x08` und
  außerhalb von `first_person`. Der Wrapper setzt bei Flag `0x02` oder
  Fortschrittsfaktor `0` jedoch `param_2=0`; `param_3` ist der separate
  Countdownwert und wird vor dem Renderaufruf gegen null geprüft.
  Der Dispatcher setzt Bit `0x08` für das Spieler-Schiff; der Viewtest schließt
  den First-Person-Modus aus. Der Writer von Bit `0x02` (`FUN_004fc8f0`) ändert Bits `0x02` und `0x04`,
  aber Ghidra fand weder direkte Codeaufrufe noch gespeicherte Funktionspointer
  auf diesen Helper. Ein indirekter oder dynamischer Aufruf ist damit nicht
  ausgeschlossen. Der Writer von Bit `0x08` liegt außerhalb der virtuellen BeamEffect-
  Methoden: `FUN_0053f340` übernimmt sein Bit `0x08` aus `param_3` und schreibt
  es auf den erzeugten BeamEffect. `FUN_0052dde0` setzt den Parameter auf `1`,
  wenn `[ESI+8]` dem von `FUN_0054baf0()` gelieferten Spieler-Schiffobjekt
  entspricht; dieser Zweig nutzt `ComputeProjectilePosAndVelocity_OneBarrel`.
  Der Retail-Viewtest für `flash_size` ist als First-Person-Kameratest zugeordnet. Details:
  [Runtimeklassen-Trace](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/Freelancer-beam-runtime-class.md>), [Writer-Kontext](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/Freelancer-beam-flag-writer-context.md>) und [Aufrufer-Trace](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/Freelancer-beam-effect-factory-references.md>).
  Im LibreLancer-Aufrufpfad ist die lokale Spielerzuordnung verfügbar:
  `SpaceGameplay` weist das Spielerobjekt `ProjectileManager.Player` zu, und
  jedes `Projectile` speichert seinen `Owner`. `ProjectileRenderer` kann den
  Retail-Spielerzweig daher grundsätzlich von NPC-Projektilen unterscheiden.
  Das ordnet jedoch weder `FUN_0054bc80()` einem Client-Kameramodus zu noch
  beweist es, dass `IsSpecialCamera()` dieselbe Retail-Bedingung abbildet.
  Die Caller-Nachbarschaft aus Ghidra zeigt außerdem `FUN_0052cfd0` aus dem
  `CliLauncher`-Pfad: Im positiven Viewmarker-Zweig prüft dieser den lokalen
  Spieler und erzeugt eine konfigurierte Gun-`LightAnim`. Zusammen mit dem
  Beam-Aufrufer grenzt das den Marker auf einen Zustand ein, der lokale
  Spielerwaffenpfade beeinflusst. `FUN_0054b900` ruft `FUN_00549e70` mit
  `ECX=0x00678b98` und dem Modus `third_person` auf. Derselbe Modus-Helper
  liefert für `first_person` den Eintrag `param_1+0x0c`, also exakt
  `0x00678ba4`. Damit ist `FUN_0054bc80()` als First-Person-Kameratest
  zugeordnet. LibreLancers `IsSpecialCamera()` prüft dagegen, ob die aktive
  Kamera von der Chase-Kamera abweicht, und ist kein entsprechender
  First-Person-Modus. Belege: [View-Update-Instruktionen](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/Freelancer-view-update-instructions-0054b900.md>), [Modusauswahl](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/Freelancer-camera-mode-neighborhood-00549e70.md>) und [Client-Kamerastatus](../src/LibreLancer/GameStates/SpaceGameplay.cs).
- Die Statusprüfung ist weiter eingegrenzt: `FUN_0054bc80()` liefert genau dann
  `true`, wenn der aktive Pointer aus `FUN_0040f130()` auf `&DAT_00678ba4` zeigt.
  Ghidra ordnet `00678ba4` dem initialisierten `.data`-Block zu; das erste
  DWORD ist null. Die Getter `FUN_0054bc30/40/50` liefern drei solche
  nullgefüllten Speicheradressen (`00678ba4`, `00678d48`, `00678f60`), die in
  `FUN_0050c330`/`FUN_005134c0` per Pointergleichheit verglichen werden. Das
  sind Kameraeinträge des Managers; `00678ba4` ist der `first_person`-Eintrag.
  Die zwei weiteren Adressen bleiben hier unzugeordnet. `FUN_00549bc0()` ersetzt
  den aktiven Pointer beim Umschalten; `FUN_00549e70()` wählt benannte Modi wie
  `player`, `first_person` und `third_person`. `FUN_0054b900()` reagiert auf
  denselben Marker `00678ba4` an einem separaten aktuellen Pointer mit einem
  Wechsel zu `third_person`; ob diese Variable dieselbe Rolle wie `DAT_006164dc`
  hat, ist nicht belegt. Der `flash_size`-Zweig hängt damit nachweislich vom
  First-Person-Modus ab; Bit `0x08` kennzeichnet in diesem Pfad den Schuss des
  Spieler-Schiffs. Der
  Storage-Befund und seine Grenzen stehen im
  [Marker-Storage-Trace](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-view-state-trace/Freelancer-view-state-marker-storage.md>). Die
  Referenz- und Decompiler-Auszüge zu [aktivem Zeiger](/home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-15-flash-size-trace/Freelancer-state-global-006164dc.md),
  [Umschalter](/home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-15-flash-size-trace/Freelancer-state-switcher-00549bc0.md)
  und [Sonderinstanz](/home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-15-flash-size-trace/Freelancer-sentinel-00678ba4.md)
  enthalten die Herkunft.
- Damit ist der Fortschrittsfaktor `min(1, elapsed / duration)` mit
  `duration = (core_length + tail_length + 7.5) / speed` belegt. Der Client
  akkumuliert für Projektile denselben Zeitwert in `Projectile.Time`; die
  äquivalente Strecke lässt sich aus `Projectile.Normal.Length() * Time`
  bilden. Der Retail-Aufruf bindet den Beam-Kontext über `FUN_0053f2f0` an ein
  separat alloziertes Objekt der Klasse `0x67` (`CBeam`), das mit dem `Root` des
  feuernden Schiffs erzeugt wird. Die Geschwindigkeitsabfrage dispatcht auf
  `CBeam::get_velocity`, das `+0xf8..+0x100` zurückgibt; `CBeam::init_physics`
  kopiert seinen ersten Vector-Parameter in diese Felder. `CSimple::open` ruft
  `CObject::open` auf, speichert den `Root` bei `+0x88` und setzt zusätzlich
  `+0xe0`. Der bisherige Aufrufpfad belegt nicht, ob `CBeam::init_physics` für
  dieses Hilfsobjekt läuft oder welcher Vektor dabei übergeben wird. Die
  Physikkomponente `CObject+0x54` ist nicht die Quelle dieses dynamischen
  Getters. `Archetype::Root::create_instance`
  delegiert die eigentliche Instanzerzeugung bei geladenem Archetyp an den
  `IEngine`-VTable-Slot `+0x5c`; `DALib::Startup` erwirbt `IEngine` über DACOM.
  Die Freelancer-`dacom.ini` lädt `EngBase.dll` und listet darunter die
  `[Engine]`-Komponenten. `engbase.dll` enthält außerdem den Quellpfad
  `EngBase/Engine/engine.cpp` und Meldungen aus `Engine::load_engine_components`.
  Der EngBase-Pfad ist nun konkreter aufgelöst: `FUN_06621ca0` vergleicht
  Interface-Namen mit der Tabelle ab `06629608`; für `1.11_IEngine` und
  `1.11_IEngine2` ist jeweils Offset `0` eingetragen, sodass beide Interfaces
  auf dieselbe Objektbasis zeigen. Der zugehörige Interface-VTable-Eintrag bei
  `+0x5c` zeigt auf `FUN_06622400`. Diese Funktion liest Archetype-Daten bei
  `+0x1c/+0x20` und liefert einen booleschen Verfügbarkeitswert; sie schreibt
  weder den `CObject`-Physikzeiger noch den `CBeam`-Geschwindigkeitsvektor.
  Damit ist EngBase als Anbieter dieses `IEngine`-Objekts belegt; die
  Initialisierung des `CBeam`-Geschwindigkeitsvektors bleibt offen.
  Die benachbarten Verbraucher `FUN_06625e00` (`HardPoint::Initialize`) und
  `FUN_06616d40` fragen das Interface zusätzlich ab. Belege: [DACOM-Konfiguration](</home/masterbee/Dokumente/FL/EXE/dacom.ini>), [EngBase-IEngine-Xrefs](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/engbase-IEngine-xrefs.md>), [Interface-Methode und Slot](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/engbase-IEngine-implementation-06622400.md>), [EngBase-IEngine-Verbraucher](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/engbase-IEngine-referencing-functions.md>), [Root-Factory](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/common-Root-create_instance.md>) und [DALib-Startup](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/DALib-Engine-startup-shutdown.md>).
  Eine Gleichsetzung
  des Retail-Speed mit `Projectile.Owner` ist deshalb noch nicht abgesichert.
  Der dritte Rendererparameter bleibt separat zuzuordnen. `flash_size`
  pauschal als Mündungsblitz zu interpretieren wäre weiterhin unbelegt.
  Details: [CBeam-Dispatch und Geschwindigkeitsinitialisierung](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/common-CBeam-velocity-dispatch.md>), [Kontext-VTable und Setter](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/Freelancer-beam-owner-binding-slot.md>), [Kontextobjekt-Erzeugung](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/Freelancer-beam-owner-base-setter.md>) und [CObject::open](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/common-CObject-open.md>).
- Die Parser- und Renderer-Auszüge mit Decompiler-Ausgabe liegen lokal in der
  [Freelancer-Reverse-Engineering-Ablage](/home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-14-light-range/Freelancer-beam-parser-0042e4a0.md),
  zusammen mit einem Hash- und Interpretationshinweis. Der Renderpfad steht in
  [FUN_004fa270](/home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-14-light-range/Freelancer-beam-renderer-004fa270.md).

## Treffer und Explosionen

- Projektiltreffer rufen `GameWorld.SpawnTempFx()` mit dem konfigurierten
  Hit-Effekt auf.
- Munitions-, Schiffs-, Minen- und Asteroidenexplosionen sowie CMP-Abtrennung
  nutzen ebenfalls temporäre Effekte. Die zuvor erstellte
  [Lichtprüfung](dynamic-lighting-audit.md) verfolgt diese Anbindung an
  kurzlebige dynamische Effektlichter.
- Die Partikeleffekte werden über `ResolvedFx`/`ParticleEffectInstance`
  abgespielt. Für eine visuelle Überarbeitung müssen repräsentative Treffer,
  kleine Schiffe, große Schiffe und Minen aus dem NAP-Bestand ausgewählt und
  mit unveränderten Timings verglichen werden.

### Codepfadprüfung 2026-10-16

Die Client-Aufrufer für Projektiltreffer (`World/ProjectileManager.cs`),
Munitionszerstörung und CMP-Abtrennung (`Client/CGameSession.Space.cs`),
Schiffs- und Ausrüstungszerstörung (`GameStates/SpaceGameplay.cs`) sowie
Asteroidenzerstörung (`World/Components/AsteroidFieldComponent.cs`) übergeben
`ResolvedFx` an `GameWorld.SpawnTempFx()`. Dieser zentrale Pfad löst das ALE aus
und reicht `DynamicLightAnimation`, Lichtreichweite, Zeit-/Reichweitenfaktor
und Sound an den Renderer beziehungsweise SoundManager weiter. Der Aufrufpfad
ist damit statisch verbunden; diese Prüfung bestätigt keine visuelle Darstellung
für jeden Effekt. Die NAP-only-Release-Laufzeitbelege vom 16.10. prüfen die
M01A-Basisexplosion und eine QA-ausgelöste LI-Jägerzerstörung; die letztere
zeigt eine sichtbare mehrteilige ALE-Explosion. Transientlicht-A/B,
Großschiff-Fuse-Ereignisse, Minen und abgetrennte Ausrüstung bleiben offen.
Ein ergänzender NAP-Inventarlauf fand für 69 Schiff-Archetypen keine fehlenden
Explosion-Arch-, Effektprofil- oder ALE-Dateien. 17 Archetypen haben laut INI
keinen direkten Effekt; fünf weitere nutzen ein Effektprofil ohne `vis_effect`.
Großschiffe starten sichtbare Sequenzen zusätzlich über netzwerkübertragene
Fuse-Effekte. Einzelheiten und Grenzen: [NAP-Explosionsinventar](evidence/renderer/explosion-nap-profile-inventory-20261016.md).

Noch separat visuell abzunehmen sind mindestens:

- Treffer-/Munitionsblitz und Projektil-Aufschlag;
- kleine Schiffszerstörung sowie große Schiffs-/Stationszerstörung;
- Minenexplosion;
- zerstörte Ausrüstung und CMP-Abtrennung;
- sichtbarer Lichtbeitrag bei aktiviertem `render_transient_effect_lights` und
  Kontrolllauf mit ausgeschalteter Option.

## Nächste Arbeit

1. [x] `FUN_004fa0a0` als Aufrufer verfolgen: Der Geometriefaktor wird aus zwei
   Objektwerten berechnet und bei Flagmaske `2` oder Faktor `0` als `param_2=0`
   weitergegeben. `param_3` ist ein eigener Countdownwert; der aufgerufene
   `flash_size`-Zweig mit `param_3=0` ist auf diesem Pfad nicht nachgewiesen.
   Der Archetyp verzweigt zu den Spear-/Bolt-Renderern.
2. [x] Speicherart des Zustandsmarkers bestimmen: `DAT_00678ba4` liegt als
   nullgefüllte statische Kameraadresse im `.data`-Block, keine VTable. Zwei
   weitere Getter liefern gleichartige Adressen. `00678ba4` ist der benannte
   `first_person`-Eintrag. Details: [Marker-Storage-Trace](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-view-state-trace/Freelancer-view-state-marker-storage.md>).
3. [x] Writerbedingung zuordnen: `FUN_0052dde0` setzt den Parameter für
   `FUN_0053f340` im Zweig, in dem `[ESI+8]` dem von `FUN_0054baf0()` gelieferten
   Spieler-Schiffobjekt entspricht. Dieser Zweig berechnet die
   Projektiltransformation über `ComputeProjectilePosAndVelocity_OneBarrel`.
   Quellen: [Dispatcher-Instruktionen](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/Freelancer-beam-effect-dispatch-instructions.md>), [Spielerobjekt-Getter](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/Freelancer-beam-view-marker-0054baf0.md>) und [Watcher-Referenzen](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/Freelancer-beam-global-00679744.md>).
4. [x] Den Marker `DAT_00678ba4` dem `first_person`-Eintrag des
   Kamera-Managers zuordnen. `FUN_0054bc80()` prüft, ob dieser Eintrag aktiv ist.
   [View-Update-Instruktionen](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/Freelancer-view-update-instructions-0054b900.md>) und [Modusauswahl](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/Freelancer-camera-mode-neighborhood-00549e70.md>).
5. [ ] Den über `CBeam::get_velocity` gelesenen Vektor seiner Initialisierung
   zuordnen und mit der Geschwindigkeit des feuernden Schiffs vergleichen.
   Bit `0x08` ist auf das Spieler-Schiff zurückgeführt; der Retail-Renderer
   liest diesen Beamzweig nur außerhalb von `first_person`.
6. [x] Das Updateargument ist Frame-Zeit aus `FUN_004f7480` (maximal `0.1`),
   und `this+0x80` ist die aus Beam-Länge und Kontextgeschwindigkeit
   berechnete Dauer. Der Fortschrittsfaktor ist `elapsed / duration`, begrenzt
   auf `1`. Exporte: [Beam-Update-Aufrufer](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/Freelancer-beam-update-callers.md>), [Frame-Update-Instruktionen](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/Freelancer-beam-frame-update-instructions.md>) und [Kontext-Setter](</home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-16-beam-runtime-class-trace/Freelancer-beam-setter-context.md>).
7. [ ] Verfolgen, ob `CBeam::init_physics` für das separat erzeugte
   Kontextobjekt aufgerufen wird und welchen ersten Vector-Parameter es erhält;
   dann den resultierenden `CBeam::get_velocity`-Vektor mit `Projectile.Owner`
   vergleichen und `elapsed / duration` gegen `currDist` prüfen. Der bisherige
   Setterpfad bindet den Schiff-`Root`; `CSimple::open` speichert ihn, setzt
   aber keinen Geschwindigkeitsvektor. Außerdem klären,
   ob der `flash_size`-Zweig mit drittem Argument null über einen anderen Pfad
   erreichbar ist; der bisherige Wrapperpfad setzt `param_2=0` und führt den
   Nullwert von `param_3` nicht zum Renderer. Das Countdownverhalten von
   `this+0x7c` (0,3 s) ist statisch nachvollzogen. `IsSpecialCamera()` ist kein
   Retail-First-Person-Test und der Client hat derzeit keine First-Person-Kamera.
8. [ ] `flash_size` anhand der Retail-Formeln in die Beam-Spear-Geometrie
   übertragen und Null-, Grenz- und typische NAP-Werte abdecken.
9. [ ] Beam- und Explosionseffekte mit NAP-only-Containern visuell vergleichen;
   Originalwerte und Timing als Referenz erhalten.
10. [ ] Erst nach diesen Vergleichen den übergeordneten Effekt-Todo-Punkt als
   abgeschlossen markieren.
