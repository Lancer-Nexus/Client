# Sonnenambient-Stärke: Laufzeitproben (2026-10-17)

Zwei NAP-only Starts desselben Hi02-Saves mit `render_sun_ambient=true` und
identischer gespeicherter Schiffsposition wurden mit Mischstärke 0,25 und 1,0
aufgenommen. Der Client lief auf OpenGL 4.6 / Mesa mit Intel HD Graphics 4600;
die Client-Konfiguration war 1280x720; die Screenshots wurden über X11 aus dem Spielfenster erfasst.

| Aufnahme | Startparameter | Sichtbarer Befund |
| --- | --- | --- |
| [Stärke 0,25](strength-025.png) | `--render-sun-ambient-strength=0.25` | Grüne Systemfärbung ist deutlich; der nahe Jäger und die Umgebung bleiben vergleichsweise dunkel. |
| [Stärke 1,0](strength-100.png) | `--render-sun-ambient-strength=1.0` | Starke Aufhellung und deutliche Übersteuerung im Sonnen-/Nebelbereich; die Szene wirkt sichtbar ausgewaschen. |

## Aussagegrenze

Das ist eine orientierende Qualitätsprobe, keine gültige Same-frame-A/B-Abnahme:
Die Umgebung und Sonnenposition unterscheiden sich zwischen den getrennten
Prozessstarts; Asteroiden und Partikel variieren ebenfalls. Deshalb lässt sich
die Bilddifferenz nicht allein der Ambient-Stärke zuordnen. Die Aufnahmen zeigen
aber, dass 1,0 für diese Szene zu hoch ist. Der Standard 0,25 bleibt bestehen,
bis ein identischer Frame über die Live-Option aufgenommen und rote, grüne,
weiße sowie mehrfach vorhandene Sonnen geprüft wurden.

## Laufzeitnachweis

Beide Starts verwendeten den gleichen NAP-basierten Save
`hi02-ambient-strength.fl`, dieselbe aktive NAP-Paketwurzel und
`--invulnerable=on`. Die Logs erreichten `Game: Finished loading game data`,
`SpawnFlow: World restore completed for Hi02`, `Game: Entering system Hi02` und
`Player: Spawning`. Die Paket-Hashes und der aktive Manifest waren vor dem
Start kontrolliert worden. Der Hinweis, dass `sun.sph` nicht alle sechs Seiten
enthält, ist ein vorhandener Asset-Warnhinweis und gehört nicht zu diesem
Ambient-Stärke-Befund.
