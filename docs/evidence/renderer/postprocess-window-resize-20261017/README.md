# Postprocess Resize und Restore – Laufzeitnachweis (2026-10-17)

## Lauf

- Client: frisch erstellter Release-Build, Version `03bfd87-git`.
- Grafik: OpenGL 4.6, Mesa 23.2.1, Intel HD Graphics 4600.
- Konfiguration: HDR, Bloom, SSAO und `--render-antialias=smaa-high` aktiv; Fenster 1280×720.
- Das Spiel lud die Freelancer-Daten, meldete `UI: Interface loaded` und startete die Intro-Menüszene. Der Cutscene-Zeichenpfad ruft den `SystemRenderer` auf, sodass die aktive 3D-Szene die Render- und Postprocess-Pipeline durchlief.
- Das Fenster wurde auf 960×540 verkleinert, danach auf 1280×720 zurückgesetzt sowie minimiert und wieder eingeblendet. Die Fenstergeometrie entsprach nach dem Wiederherstellen den angeforderten Maßen. Im Log wurden bei der Prüfung keine Renderer-Fehler- oder Ausnahmezeilen gefunden.

## NAP-Eingaben

Der aktive Manifest-Snapshot ist `lancer-pbr-qa-20261014` mit SHA-256 `57af6496c39b4c35ebb32add550fd58ef00f5eb40f7658b7207a06fc99293652`. Alle 13 referenzierten NAP-Archive bestanden Größen- und SHA-256-Prüfung gegen das aktive Manifest sowie `nexus-pack verify`. Der Anwendungsordner enthielt kein loses `DATA`-Verzeichnis. Die konfigurierte Freelancer-Installation blieb als Daten-Bootstrap/Fallback vorhanden; dieser Lauf belegt deshalb den aktiven NAP-Paketpfad, aber keine vollständig isolierte Laufzeit ohne externe Dateninstallation.

Die zuvor dokumentierte Paketbereitstellung nennt als Eingaben ausschließlich `Data/NAP/.runtime-test-stage/package-inputs-20261016` und `Data/NAP/QA/runtime-smoke-20261014`. Die Eingabeherkunft wird hier entsprechend der aktiven Paketdokumentation referenziert; ein erneuter Hashvergleich jeder einzelnen entpackten Datei gegen die gestuften Quellverzeichnisse ist nicht Teil dieses Nachweises.

## Bilder

- `menu-960x540.png`
- `menu-1280x720.png`

Die Aufnahmen zeigen animierte Frames der Intro-Menüszene. Sie belegen Darstellung und Fenstergrößenwechsel, sind aber kein pixelgenauer A/B-Vergleich und kein kontrollierter FPS-Benchmark.

## Grenzen

Der Nachweis deckt einen OpenGL/Mesa-Lauf auf einer Intel HD Graphics 4600 ab. Andere GPUs und Backends, lange Resize-Zyklen, Fehler-Injektion, tatsächlicher Geräteverlust und die Wiederherstellung nach Geräteverlust sind nicht geprüft. Speicherwerte bleiben Schätzungen; daraus folgt keine vollständige GPU-Speicherbilanz.
