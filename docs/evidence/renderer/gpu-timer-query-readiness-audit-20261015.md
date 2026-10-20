# GPU-Timer-Query: Bereitschaftsaudit

Stand: 2026-10-15. Quellcode- und Laufzeitbefund zum Roadmap-Punkt
„GPU-Zeit und Speicherverbrauch pro Pass messbar machen“ sowie Ergebnis der
ersten OpenGL-Timer-Query-Implementierung.

## Befund

- Vor der Implementierung bot `IRenderContext` keine GPU-Timer- oder
  Query-Schnittstelle; die Bindungsspezifikation enthielt keine Timer-Query-
  Funktionen. Das ist mit Patch 2109 behoben.
- Die SDL3/OpenGL-Laufzeitprobe auf Intel HD Graphics 4600 / Mesa 23.2 meldet
  `GL_ARB_timer_query` und `GL_EXT_timer_query`. Das belegt die Verfügbarkeit auf
  diesem Gerät, nicht auf allen OpenGL-Zielsystemen. Die Probe und ihr
  vollständiges Log liegen außerhalb des Repositories unter
  `/tmp/lancer-nexus-renderer-profile-20261015/offscreen-run.log`.
- OpenGL stellt `GL_TIME_ELAPSED` über vier Query-Objekte je Pass bereit. Ein
  Query wird nur nach `GL_QUERY_RESULT_AVAILABLE` ausgelesen; wenn alle vier
  Slots noch ausstehen, überspringt dieser Pass seine nächste Messung. Das
  Debug-Overlay zeigt Millisekunden und Sample-Alter. Query-Objekte werden nur
  bei sichtbarem Debug-Overlay angelegt und beim Ausschalten freigegeben.
- `NullRenderContext` besitzt keinen GPU-Zeitgeber und meldet die Funktion als
  nicht unterstützt. Die CPU-Messung (`FrameWorkTime`, `FrameInterval`) bleibt
  eine getrennte Diagnose und kann Treiberwartezeit enthalten.
- Im geprüften Rendererpfad liegen geeignete GPU-Grenzen um den Depth-Prepass,
  `Commands.DrawOpaque`, den Starsphere-/Nebelübergang, `Commands.DrawTransparent`
  und den AA-Blit. CPU-seitige Sichtbarkeit, Vorbereitung, Uploads und
  Command-Erzeugung liegen davor oder zwischen diesen Draw-Aufrufen. Die
  vorhandene Reihenfolge ist im [Forward-Pass-Audit](forward-pass-order-audit-20261015.md)
  festgehalten.
- Es gibt keine einheitliche, backendübergreifende Abfrage für den tatsächlich
  belegten GPU-Speicher pro Pass. GPU-Allokationen werden von mehreren
  Ressourcenobjekten und Treiber-/Backendzuständen verwaltet. Eine Schätzung
  aus Textur-/Buffergrößen wäre Ressourcenbuchhaltung, keine gemessene
  Treiberbelegung.

## Erster Umsetzungsschritt

- Eine optionale `IGpuTimerQuery`-Abstraktion wurde ergänzt. OpenGL verwendet
  `GL_TIME_ELAPSED`; Null meldet keine Unterstützung.
- Der Vierer-Ring fragt nur verfügbare Resultate ab und überspringt Messungen,
  wenn alle Slots noch laufen. Es gibt keinen synchronen Query-Result-Read.
- Depth-Prepass, Opaque, Stars/Nebel, Transparent und AA-Resolve werden separat
  gemessen. CPU-Vorbereitung und Upload bleiben außerhalb dieser GPU-Zeiten.
- Das Debug-Overlay meldet unterstützte/fehlende Query-Fähigkeit sowie
  Messwert und Sample-Alter. CSV-Zeilen enthalten den zuletzt verfügbaren
  asynchronen Wert samt Alter; fehlende Werte bleiben leer. Die Laufzeitabnahme
  auf einer echten Spielsituation steht noch aus.
- Speicher bleibt getrennte Ressourcenbuchhaltung pro Ressourcentyp und ist
  nicht Teil der Timer-Query-Implementierung. Driver-native Memory-Budget-APIs
  bleiben backend- und plattformspezifische Ergänzungen.

## Abnahmegrenzen

- Query-Fähigkeit auf dem rendernden Kontext prüfen; GL-Version allein nicht als
  Beleg verwenden.
- Keine synchronen `GL_QUERY_RESULT`-Reads im laufenden Frame. Im aktuellen
  Quellpfad erfolgt der Result-Read erst nach erfolgreichem
  `GL_QUERY_RESULT_AVAILABLE`-Check.
- Bei deaktiviertem Debug-Profiling keine Query-Aufrufe und keine dauerhaften
  zusätzlichen GPU-Ressourcen.
- Werte mit bekannten GPU-lastigen und CPU-lastigen Proben plausibilisieren;
  GPU-Zeit kann die Framezeit wegen Überlappung nicht einfach addiert werden.
- Null-Backend und fehlende Timer-Query-Unterstützung müssen fehlerfrei bleiben.

## NAP-Laufzeitwiederholung nach Implementierung

Der Testinstallationsordner enthielt 13 aktive Pakete unter `packages/data/`.
Alle Paketgrößen und SHA-256-Werte stimmten mit `active.json` überein; alle
Archive bestanden `nexus-pack verify`. Im Installationsordner lag kein loses
`DATA`-Verzeichnis. `data/runtime.nap` enthält `EXE/newplayer.fl`.

Der Release-Client wurde mit SDL3-Offscreen, isolierten XDG-Datenverzeichnissen
und `--new-game` gestartet. Der Prozess lief bis zum 90-Sekunden-Limit, loggte
aber nur Plattform, SDL3 und Engine-Version. Es gab keine OpenGL-Initialisierung
und keinen Fehlerstack. Der Lauf belegt daher weder Query-Erzeugung noch
Pass-Messwerte; die GL-Laufzeitprüfung bleibt offen. Log:
`/tmp/lancer-nexus-renderer-profile-20261015/gpu-timer-run.log`.
