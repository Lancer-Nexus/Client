# Release-Profil mit aktiven Renderoptionen (2026-10-18)

## Lauf und Datenherkunft

Der Release-Client `03bfd87-git (20261017)` lief auf OpenGL 4.6 / Mesa 23.2.1
mit Intel HD Graphics 4600 bei 1280×720. `debug=false` und `vsync=false` waren
gesetzt. Alle derzeit implementierten Effekt-, Beleuchtungs-, HDR/Bloom-,
SSAO-, Schatten-, Soft-Particle-, Normalmap- und PBR-Optionen waren aktiviert;
der genaue Snapshot steht in jeder Zeile der CSV unter
`render_options_initial_json`. Die INI enthielt keinen Profil-Schalter. Die
Aufzeichnung wurde ausschließlich mit `--render-profile-capture=on` aktiviert.

Die 13 NAP-Archive wurden aus
`Data/NAP/.runtime-test-stage/package-inputs-20261016/{Effects,Equipment,Material,Mission,Models,Runtime,Script,Sounds,Surface,System,UI,Video,World}`
gepackt. Jede Eingabe liegt unter `Data/NAP/`; jedes Archiv bestand
`nexus-pack verify`, und Größe sowie SHA-256 stimmen mit dem beigefügten
[`nap-active.json`](nap-active.json) überein. Manifest-ID:
`lancer-render-profile-current-20261018`, SHA-256:
`fb817c9d87d30bf7b990c469077551fc0b921be04bc81f53fa9f51845fd1541d`.
Die `EXE/freelancer.ini`- und `EXE/dacom.ini`-Dateien im isolierten Datenwurzel-
Ordner stimmen per SHA-256 mit den Dateien aus demselben `Data/NAP`-Staging ab.
Im Client-App-Verzeichnis lag kein loses `DATA`- oder `data`-Verzeichnis.

Der Loglauf erreichte `Finished loading game data`, `UI: Interface loaded`,
`Entering system FP7_system` und `Profile: Saved 300 frames`. Der Client wurde
anschließend sauber beendet. Der vollständige Lauf-Log steht in
[`runtime.log.txt`](runtime.log.txt); die verwendeten Paketquellen sind in
[`package-inputs.txt`](package-inputs.txt) aufgelistet.

## Messwerte

| Kennzahl | p50 | p95 | p99 |
|---|---:|---:|---:|
| CPU Update + Draw | 6,055 ms | 9,967 ms | 74,318 ms |
| Frameintervall | 11,221 ms | 13,807 ms | 79,527 ms |
| FPS-Verteilung | 89,13 FPS | 97,89 FPS | 100,32 FPS |

Asynchrone GPU-Passzeiten (Mittelwert, 298–299 verfügbare Samples):

| Pass | Mittel |
|---|---:|
| Opak | 3,801 ms |
| SSAO | 1,597 ms |
| AA-Auflösung | 1,414 ms |
| Transparenz | 0,812 ms |
| Sterne/Nebel | 0,680 ms |
| SSAO-Tiefenpass | 0,615 ms |
| SMAA-Nachbarschaft | 0,602 ms |
| Tone Mapping | 0,445 ms |
| Bloom-Extraktion | 0,367 ms |
| Schattenkarte | 0,229 ms |

GPU-Samples sind asynchron und können älter als der jeweilige CPU-Frame sein;
Passzeiten dürfen nicht als Gesamt-GPU-Zeit addiert werden.

## Grenzen

Dies ist ein Funktionstest des CLI-Captures und ein einzelnes Diagnoseprofil aus
der animierten Intro-/M01A-Szene, keine stabile FPS-Baseline. Der Client meldete
beim Szenenübergang 32 verworfene Simulationstakte. Das Log meldet außerdem,
dass `sun.sph` nicht alle sechs Seiten enthält und deshalb nicht gerendert wird;
diese Asset-Lücke ist kein Renderer-Benchmark. Für belastbare Optimierungen
bleiben mehrere ruhige Wiederholungen je Szene und Option sowie die spätere
RTX-5060-Messreihe erforderlich.
