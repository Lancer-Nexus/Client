# Renderer Performance Acceptance Matrix

Stand: 2026-10-18. Diese Matrix trennt Renderer-GPU-Kosten von Client-/Server-Simulation und legt fest, wie die bestehende Intel-Messung mit dem späteren RTX-5060-System verglichen wird. Sie setzt noch keine FPS-Zielzahl fest.

## Verifizierte Ausgangslage

Der lokale Linux-Desktop verwendet OpenGL 4.6/Mesa 23.2.1 auf Intel HD Graphics 4600 (Haswell GT2, `i915`). Die Quadro K1100M ist vorhanden, läuft derzeit aber mit `nouveau`; die dokumentierte Hi02-Messung lief auf der Intel-GPU. Der 300-Frame-Lauf bei 1280×720 hatte VSync aus und alle neuen Renderoptionen aktiv. Der Debug-Profiler war ebenfalls aktiv. Er ergab 99,44 FPS Median, 106,26 FPS p95, 5,80 ms CPU Update+Draw Median und 7,59 ms p95. Der transparente Pass kostete im Mittel 2,645 ms, SSAO 1,087 ms und AA Resolve 1,182 ms.

Diese Werte zeigen, dass die aktuelle Intel-OpenGL-Umgebung beschleunigt rendern kann; sie sind wegen Debug-Overlay und einzelner Hi02-Szene keine saubere Release-Baseline. Siehe [vollständiges Hi02-Profil](hi02-gpu-performance-intel-20261018/README.md). `llvmpipe`-/Xephyr-Läufe sind getrennt als Software-Rendering zu kennzeichnen und dürfen nicht mit GPU-Läufen zusammengefasst werden.

Ein aktueller Release-Lauf schaltete die Renderoptionen per INI ein und startete das Profil ausschließlich mit `--render-profile-capture=on`; Debug-Overlay und VSync waren aus. Nach Laden von `Mission_01a`/`FP7_system` wurden 300 Frames mit Optionssnapshot erfasst. Median Frameintervall: 11,221 ms; p95: 13,807 ms; p99: 79,527 ms; FPS-Median: 89,13. Mittlere GPU-Zeit einzelner Pässe: opak 3,801 ms, SSAO 1,597 ms, AA 1,414 ms. Beim Introübergang wurden 32 Simulationstakte verworfen. Dieser einzelne bewegte Lauf bestätigt den CLI-Export und die Passmessung auf Intel, aber keine stabile Baseline. Details, Manifest und Log: [Release-Profil mit aktiven Renderoptionen](profile-capture-release-smoke-20261018/README.md).

## Vergleichsprofile

| Profil | Hardware | Auflösung | Renderoptionen | Zweck |
|---|---|---:|---|---|
| A | Intel HD 4600 / Mesa OpenGL | 1280×720 | kontrollierte Legacy- und moderne Varianten | Rückfall-/Altgeräte-Baseline |
| B | RTX 5060 / dokumentierter Herstellertreiber | 1280×720 | identische Varianten wie A | GPU-Generationsvergleich |
| C | RTX 5060 / dokumentierter Herstellertreiber | 1920×1080 und 2560×1440 | identische Varianten wie A | Auflösungs- und Pixelkosten |
| D | RTX 5060 | 1920×1080 | Effekte und Lichtbudgets einzeln variiert | Kostenbeitrag der Features |

Vor jeder Messreihe Shader und Assets vorwärmen, danach mindestens drei 300-Frame-Aufnahmen je Zustand machen. Debug-Overlay und VSync ausschalten. Kamera, Missions-/Simulationszeit, Sichtobjekte, NAP-Manifest, Client-Build, Treiber, Energieprofil und Fenstergröße festhalten. Für die Optionen einen Lauf mit Legacy-Grundeinstellungen, einen mit allen vorgesehenen Qualitätsfunktionen und einzelne Ein/Aus-Vergleiche für HDR/Bloom, Schatten, SSAO, Soft Particles, Surface/PBR, SMAA und dynamische Lichter erfassen.

Für eine Aufnahme ohne sichtbares Debug-Overlay `debug = false` setzen und den
Client mit `--render-profile-capture=on` starten. Nach 300 gerenderten Frames
exportiert der Client CSV mit CPU-Framezeiten, Frameintervall, Auflösung,
Renderer, asynchronen GPU-Passproben und einer JSON-Spalte mit den beim ersten
Profilframe aktiven Renderoptionen. Diese Anfangswerte gelten als Profilmetadaten;
bei während der Aufnahme live umgeschalteten Optionen muss die Änderung separat
protokolliert oder mit getrennten Aufnahmen verglichen werden. Der Ausgabepfad steht im Client-Log
unter `Profile`; unter Linux liegt die Datei in
`$XDG_DATA_HOME/LancerNexus/profiles/` (bei leerem `XDG_DATA_HOME` im
Standarddatenverzeichnis). Der Profilmodus gilt nur für diesen Start, beendet
die Aufnahme nach dem Export und blendet die Debug-Oberfläche nicht ein.

## Zu erfassende Kennzahlen

- Frameintervall p50/p95/p99 und FPS-Verteilung, getrennt vom CPU Update+Draw.
- Asynchrone GPU-Zeit je Renderpass inklusive Sample-Alter; Passzeiten nicht als Gesamt-GPU-Zeit aufsummieren, falls sie sich überlappen.
- Auflösung, sichtbare Objekte, aktive Lichter/Partikel und Renderoptionen je Profil.
- Simulation-overload- und Slow-update-Meldungen getrennt vom Renderprofil erfassen.
- Wiederholungen und Streuung berichten; keine einzelne Messung als FPS-Garantie ausgeben.

Intro, Kampf/Explosion, Nebel, Asteroiden, Basis, Planet und DFM bleiben eigene Szenenprofile. Erst wenn diese Profile auf dem RTX-System vorliegen, lassen sich sinnvolle FPS-Ziele und gezielte Optimierungen festlegen.
