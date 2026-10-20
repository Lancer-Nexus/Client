# GPU-Timer: SMAA-Unterpässe im M01A-Intro (2026-10-16)

## Laufbedingungen

- Aktueller Client-Release aus `src/lancer/lancer.csproj`, .NET 10; `LibreLancer.dll` SHA-256: `39cf2172b2c178586cc9bf36cb1b7005bfc5ab9c806bc1530a1265e5fa6529e1`.
- M01A-Neustart mit `--new-game --render-antialias=smaa-high`; `debug=true`, 1280×720, VSync aus. Das Debug-Overlay erfasste 300 Frames.
- OpenGL 4.6 Core / Mesa 23.2.1 auf Intel HD Graphics 4600 (HSW GT2). Der Kontext meldete `GL_ARB_timer_query` und `GL_EXT_timer_query`.
- NAP-only-Anwendung ohne loses Root-`DATA`-Verzeichnis. Die 12 Klassenpakete stammen aus `Data/NAP/.runtime-test-stage/package-inputs-20261016/`; `runtime.nap` stammt aus `Data/NAP/QA/runtime-smoke-20261014/`. Insgesamt 13 Archive wurden gebaut und jedes bestand `nexus-pack verify`. Vor dem Start stimmten Größe und SHA-256 aller Archive mit `nap-active.json` überein. Manifest-SHA-256: `e6d56a0f74095c89712f35f35586bfbe5db19d28f0f106654037a53140fc3ca1`.
- Archive und Laufprotokoll lagen während der Messung unter `/tmp/lancer-renderer-aa-profile-20261016/`. Die Patchserie enthält die reproduzierbaren Profildaten und das Manifest, nicht die mehrgigabytegroßen Container.

## Messwerte

Die GPU-Zeiten sind asynchrone `GL_TIME_ELAPSED`-Samples. Für die Tabelle wurden in der CSV wiederholte Beobachtungen desselben Samples anhand von Zeilenindex minus Sample-Alter zusammengefasst; Perzentile verwenden `ceil((n - 1) * p)`. Das sind Diagnosewerte dieses Laufs, kein plattformübergreifender Benchmark.

| Messung | Samples | p50 | p95 | p99 | maximales Alter |
| --- | ---: | ---: | ---: | ---: | ---: |
| SMAA Edge Detection | 295 | 0,337 ms | 0,424 ms | 1,306 ms | 2 Frames |
| SMAA Blend Weights | 295 | 0,148 ms | 0,178 ms | 0,203 ms | 2 Frames |
| SMAA Neighborhood Blend | 296 | 0,544 ms | 0,738 ms | 0,910 ms | 2 Frames |
| AA resolve (Summe derselben Sample-Frame-ID) | 295 | 1,037 ms | 1,338 ms | 2,351 ms | 3 Frames |

Die CSV enthält außerdem 300 CPU-/Frameintervall-Zeilen: Frameintervall p50/p95/p99 6,400/10,096/12,358 ms; Update-plus-Draw 2,803/4,908/11,174 ms. Das entspricht im p50 des Frameintervalls rund 156 FPS. Die Debug-Oberfläche ist in diesen Werten enthalten. Die Werte stammen aus einer bewegten Intro-Szene mit wechselnden Objekten und Effekten.

`MSAA color resolve` hat in diesem SMAA-Lauf erwartungsgemäß keine Samples. Der sichtbare Debug-Screenshot zeigt die drei Unterpasszeiten, den aggregierten AA-Wert und das Sample-Alter.

### Separater Lauf: MSAA 4×

Der zweite Release-Lauf verwendete dieselbe NAP-only-App, denselben OpenGL-Kontext, dieselbe Auflösung und dieselbe M01A-Intro-Sequenz, mit `--render-antialias=msaa-4x`. Vor dem Start bestanden dieselben 13 Archive erneut `nexus-pack verify`; `active.json` und Archivhashes waren unverändert. Der Client erreichte erneut den M01A-Start.

| Messung | Samples | p50 | p95 | p99 | maximales Alter |
| --- | ---: | ---: | ---: | ---: | ---: |
| MSAA Color Resolve | 300 | 0,383 ms | 0,468 ms | 0,564 ms | 2 Frames |

Die drei SMAA-Unterpass-Slots blieben in diesem Lauf leer; der aggregierte `AA resolve`-Slot entsprach dem MSAA-Resolve. Frameintervall p50/p95/p99: 15,790/19,229/20,244 ms; Update-plus-Draw: 4,030/6,441/8,282 ms. Der Debug-Overlay-Aufwand ist enthalten. Diese Werte stammen aus einem anderen Zeitpunkt der animierten Intro-Sequenz und sind kein kontrollierter Leistungsvergleich mit SMAA.

Dateien: [MSAA-4×-CSV](frame-profile-msaa4x.csv) und [MSAA-4×-Screenshot](profile-msaa4x.png). Beide Läufe wurden nach der Aufnahme manuell beendet; ein regulärer Shutdown wurde nicht abgenommen.

## Aussagegrenzen

Das bestätigt, dass der Release-Client die SMAA-Unterpass-Queries im echten OpenGL-Kontext ausführt, Resultate asynchron liest und als Gesamtwert zusammenfasst. Es belegt weder Retail-Bildqualität noch einen isolierten SMAA-Leistungsaufschlag. Eine kontrollierte Szene mit identischer Kamera und identischem Framebereich für SMAA High, MSAA und Antialiasing aus sowie weitere GPUs bleiben offen. Der Lauf protokollierte außerdem bekannte M01A-`StartIK`-Lücken und langsame Simulationsupdates; deren Ursache ist nicht Teil dieser Renderer-Messung. Der Client wurde nach dem Profil manuell beendet, ein regulärer Shutdown wurde nicht abgenommen.

## Dateien

- [SMAA-300-Frame-Profil (CSV)](frame-profile.csv)
- [SMAA-Debug-Screenshot](profile.png)
- [MSAA-4×-300-Frame-Profil (CSV)](frame-profile-msaa4x.csv)
- [MSAA-4×-Debug-Screenshot](profile-msaa4x.png)
- [Aktives NAP-Manifest](nap-active.json)
