# BW Pygar airlock: Normalmap A/B (2026-10-18)

Dieser LancerEdit-ModelViewer-Lauf prüft eine Basisszene mit identischer Kamera
und Beleuchtung. Er zeigt eine offene Qualitätsfrage bei der Kombination aus
Surface-Normalmaps und zur Laufzeit erzeugten Tangenten.

## Testaufbau

- Debug-LancerEdit, SDL3/OpenGL, Mesa llvmpipe unter XWayland.
- ModelViewer mit `Data/NAP/Models/bases/generic/bw_pygar_airlock.cmp`, Lit-Modus,
  Full-Surface-Profil (34 Bibliotheken, 2.807 Normalmap-Einträge).
- Das aktive Manifest `lancer-renderer-lit-20261018` verweist auf 13 NAP-Archive;
  vorherige Größen- und SHA-256-Prüfung der Archive gegen `active.json` war
  erfolgreich. Die NAP-Eingaben stammen aus `Data/NAP/`.
- Gleiche Modellpose, Kamera, Viewport, Licht und Hintergrund für alle Aufnahmen.
  In der Haupt-A/B-Reihe sind Surface-Roughness/Metallic und PBR-Reflexionen aus,
  diffuse IBL an. Nur `Surface normal maps` wechselt.
- Die Kontrollaufnahme schaltet danach `Generate missing tangents` aus, während
  der Normalmap-Schalter an bleibt.

## Aufnahmen und Befund

| Aufnahme | Normalmaps | Erzeugte Tangenten |
|---|---:|---:|
| [Normalmap aus](normalmap-off.png) | aus | an |
| [Normalmap an](normalmap-on.png) | an | an |
| [Tangenten-Kontrolle](generated-tangents-off.png) | an | aus |

Im Viewport-Crop `(287,58)`, Größe `500×505`, weichen **96.493 Pixel** der
Haupt-A/B-Aufnahmen voneinander ab; normalisiertes RMSE: `0.0441831`. Die
Normalmap erzeugt großflächig flimmernde beziehungsweise stark hervorgehobene
Streifen. Das ist keine akzeptable visuelle Abnahme für diese Szene.

Die vier sichtbaren Materialnamen aus dem Modellviewer lassen sich mit den
aktiven NAP-Zuordnungen abgleichen:

| Diffuse-Material | NAP-Normalmap | Stärke |
|---|---|---:|
| `dig_site_16.tga` | `nm_bw_dig_site_16` | 0,73 |
| `dig_site_18.tga` | `nm_bw_dig_site_18` | 1,09 |
| `dig_site_20.tga` | `nm_bw_dig_site_20` | 4,48 |
| `Dig site background: Monitor` | keine Zuordnung gefunden | — |

Quelle ist `Data/NAP/Surface/fx/SurfaceMaps.ini`; die gezeigte Texturbezeichnung
ist auf die Namensauflösung des Clients ohne Beachtung der Schreibweise
abgeglichen. Die Stärke 4,48 macht `dig_site_20.tga` zum Kandidaten für einen
gezielten Einzelmaterial-/Stärken-A/B, beweist aber nicht, dass diese Karte die
Streifen verursacht.

Bei ausgeschalteter Tangentenerzeugung sind die Bilder mit Normalmap-Schalter
an und aus pixelidentisch (0 abweichende Pixel). Das isoliert die Wirkung auf
den Pfad der erzeugten Tangenten, beweist aber noch nicht, ob die Ursache in der
Tangentengeometrie, der UV-Zuordnung, der Karte oder deren Kombination liegt.
Die Karten sind aus Diffuse-Helligkeit abgeleitete Schätzungen und keine
vermessenen Oberflächen-Normalen. Ein Fix oder eine allgemeine Qualitätsaussage
ist aus diesem einzelnen Modell nicht abgeleitet.

Hashes:

- `normalmap-off.png`: `5fda56723b838a470bf6a46ea0fdee2bc430f26d93d06039d1c1bb22d1ca3293`
- `normalmap-on.png`: `eda204d38d5de733cfd69b29abc259303e41eb2a27225cd453a426c7f3bf63b6`

Nächster Schritt: `dig_site_20.tga` einzeln und mit abgestuften Stärken gegen
die beiden anderen Karten vergleichen; danach die Tangenten dieses Meshes gegen
eine Ebene mit analytisch bekannter UV-Tangentenbasis prüfen. Erst anhand dieser
Kontrollen entscheiden, ob Kartenstärke, Tangentengenerator oder einzelne
Materialzuordnungen geändert werden müssen.
