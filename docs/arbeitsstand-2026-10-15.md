# Arbeitsstand Client / LibreLancer

**Stand:** 15.10.2026 · **Branch:** `main`  
**Client-HEAD:** `03bfd87b8cbca1fcb250ba809487de2732466054`

## Aktueller Schwerpunkt

Zuletzt wurde der Lua-UI-Absturz aus dem Lauf `lancer-ambient-ab-20261015` untersucht. Das Protokoll zeigt einen Aufruf einer fehlenden Lua-Funktion (`attempt to call a nil value`) aus einem `Button.OnClick`-Callback. Der Absturz geschah am 15.10.2026 um 16:24:24, nachdem die Mission in `FP7_system` unter anderem `fp7_attack_dialog`, `attack_prelude_dlg` und `FP7_address_nomads` aktiviert hatte.

Der Stacktrace führt über `Closure.Call()` zu [`Button.cs`](../src/LibreLancer/Interface/Widgets/Button.cs) (`OnClick` und `OnMouseClick`). Das Protokoll nennt weder den angeklickten Button noch das konkrete Lua-Skript oder die fehlende Funktion. Die genaue Ursache ist deshalb noch nicht zugeordnet.

Der Lua-Button-Callback protokolliert Interpreterfehler nun mit Button-ID, Funktionsname, dekorierter Lua-Fehlermeldung und Callstack; der fehlerhafte Klick wird abgefangen, damit ein UI-Skriptfehler nicht den gesamten Client beendet. Die Patch-Rekonstruktion wurde mit 685 Dateien verifiziert; der Release-Build von `LibreLancer.csproj` endete mit 0 Fehlern und 131 Warnungen. Das behebt einen fehlenden Lua-Handler nicht. Ein Laufzeittest, der den Callbackfehler erneut auslöst und die neue Diagnosezeile bestätigt, steht noch aus.

## Renderer

- Normal Maps sowie Metallic-/Roughness-Maps sind integriert und über getrennte Optionen schaltbar: `surface_normal_maps` und `surface_pbr_maps`.
- MAT-eigene Maps haben Vorrang. Ergänzende Maps werden über die Surface-Map-INI aufgelöst.
- DFM-Materialien verwenden den PBR-Pfad; für geskinte DFM-Meshes gibt es `PBR_Skinned`.
- Für Room-Vertex-Beleuchtung und Backface-Normalen ist eine Korrektur dokumentiert, die den zuvor beobachteten Schwarzeffekt im Liberty-Stadtbild beseitigte.
- LI_ELITE-A/B-Vergleiche und weitere Renderer-Befunde sind in [surface-maps.md](surface-maps.md) und [renderer-comparison-gallery.md](renderer-comparison-gallery.md) festgehalten.
- Offen sind unter anderem eine sichtbare DFM-Materialabnahme im Spiel, vollständige Spielabnahme, interaktive Cubemap-Auswahl sowie weitere Prüfung von IBL und Materialwirkung in verschiedenen Szenen.

## Codeberg-Upstream

Der Abgleich mit Codeberg `main` wurde separat dokumentiert: [upstream-sync-status-2026-10-15.md](upstream-sync-status-2026-10-15.md). Acht von 23 Commits seit dem gemeinsamen Vorfahren wurden über Patches 2139 und 2140 portiert; sieben weitere sind bereits lokal abgedeckt. Offen bleiben der größere Model-Editor-Undo-Refactor und sechs Änderungen an Login und Netzwerkbegrenzungen, die vor einer Übernahme mit Gateway und Clusterbetrieb abgeglichen werden müssen. Ein Community-Einladungslink wurde bewusst ausgelassen.

Für die Portierungen bestanden die vollständige Overlay-Rekonstruktion mit 683 Dateien und der Release-Solution-Build mit .NET 10 (0 Fehler, 131 Warnungen). Tests und Laufzeitabnahme dieser Upstream-Portierungen stehen noch aus. Diese Aussagen beziehen sich auf den dokumentierten Prüfstand vom 15.10.2026.

## Arbeitsbaum und nächster Schritt

Der Client-Arbeitsbaum enthält umfangreiche, bereits vorhandene Änderungen und ist nicht sauber. Der aktuelle Branch ist `main`; vor Änderungen müssen diese lokalen Arbeiten erhalten bleiben. LibreLancer-Engine-Anpassungen bleiben gemäß [`AGENTS.md`](../AGENTS.md) patch-only unter `patches/` und müssen in `patches/series` registriert sein.

Als nächstes muss ein reproduzierbarer Klick den Fehler erneut auslösen, sodass Button-ID, Lua-Funktion und Skriptzeilen aus dem Log hervorgehen. Anschließend kann der fehlende Handler selbst gezielt korrigiert werden.

## Belege und Grenzen

- Laufzeitlog: `/tmp/lancer-ambient-ab-20261015/state-live-x11/Librelancer/logs/lancer.log.0.txt`.
- Quellpfad: `src/LibreLancer/Interface/Widgets/Button.cs`, Lua-Closure-Aufruf in `OnClick`, Eventauslösung in `OnMouseClick`.
- Das Log belegt den Lua-Fehler und Aufrufpfad, aber nicht den konkreten Callback oder die Ursache des fehlenden Werts.
- Die Fehlerbegrenzung ist eine Schutzmaßnahme, kein Nachweis, dass der eigentliche Dialogfehler behoben ist. Laufzeitbestätigung der neuen Diagnose steht noch aus. Automatisierte Tests wurden nicht ausgeführt.
