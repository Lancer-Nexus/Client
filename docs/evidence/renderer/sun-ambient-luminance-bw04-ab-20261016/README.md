# Sonnenambient nach luminanzerhaltender Mischung: BW04 A/B (2026-10-16)

## Lauf und Daten

- Zwei getrennte Starts des aktuellen Release-Clients mit demselben kopierten statischen Save `bw04-static.fl`; nur `--render-sun-ambient=on` beziehungsweise `off` unterschied sich. Beide Logs erreichen `Finished loading game data`, `World restore completed for Bw04` und `Entering system Bw04`.
- Das Runtime-Verzeichnis lag unter `/tmp/lancer-explosion-fresh-20261016/runtime-app` und enthielt kein loses `DATA`. Es verwendete 13 NAP-Archive aus dem dokumentierten Build von `Data/NAP/`. Vor dem Start wurden alle 13 Archivgrößen und SHA-256-Werte gegen das aktive Manifest geprüft; alle `nexus-pack verify`-Aufrufe waren erfolgreich. Manifest-ID: `lancer-explosion-qa-20261016`; `active.json` SHA-256: `7fe5b1a74a1f4d14876507bb8d693ffb478cb08f4cf5dcaf64e2f3461feca871`.
- Beide Starts verwendeten `--invulnerable=on` und denselben `client.ini`, NAP-Satz und Save. Auflösung: 1920×1080. Aufnahmen jeweils kurz nach `Entering system Bw04`.
- Das geladene `LibreLancer.dll` entspricht dem aktuellen Release-Build; SHA-256 `7cc4fd66b0f9ea7ccddaaf7de5f26f56498fd57ac7b9ea6621af572ddfc63e7a`.

| Ambient | Aufnahme |
| --- | --- |
| Ein | [BW04 mit neuer Mischung](bw04-luminance-ambient-on.png) |
| Aus | [BW04 ohne neue Mischung](bw04-luminance-ambient-off.png) |

SHA-256: Ein `ebe8487d2b6c0a10c07d092926989834ffd04cb56ca32233fd38313f287d5b06`, Aus `edb5893dd9e2bdbd7eff54ceddc9baa814ab5756a4b9f1b00b239a93353f3783`.

## Vergleich und Grenzen

Im festen Schiffsausschnitt `(860, 820)–(1090, 945)` betrugen die Mittelwerte für Ein R/G/B `78,07 / 43,52 / 15,67`, für Aus `76,39 / 43,07 / 15,83`. Die mittlere absolute Abweichung zwischen den Ausschnitten beträgt `1,07` RGB-Stufen. Das ist eine kleine messbare Verschiebung, aber visuell kein überzeugender Nachweis einer gut kalibrierten Beleuchtung. Der Schiffsrenderer und kurze Spawn-/Kameraframe-Unterschiede tragen ebenfalls zum Pixelunterschied bei.

Die Bildschirmkulisse ist in beiden Bildern stabil. Diese Szene reicht nicht aus, um die sonnenfarbene diffuse Beleuchtung zuverlässig zu beurteilen. Der Lauf belegt, dass der neue Build und beide Schalterstellungen im NAP-only-Spielstart funktionieren; er schließt die visuelle Abnahme nicht ab. Offen bleibt ein Same-Frame-Live-Toggle mit ruhender Kamera in einem geeigneten, nicht von Nebelambient überschriebenen Objektbereich sowie der rote/grüne/weiße Kontrollvergleich.
