# BW Pygar airlock: Normalmap-Isolierung und Kalibrierung (2026-10-18)

Kontrollierte Same-Session-A/B-Prüfung des LancerEdit ModelViewers mit dem
NAP-Modell `bw_pygar_airlock.cmp`. Ziel war, die zuvor beobachteten Streifen
materialweise zuzuordnen und einen moderateren Satz Kartenstärken zu beurteilen.

## Testbedingungen und Datenherkunft

- Debug-LancerEdit, SDL3/OpenGL, Mesa llvmpipe unter XWayland, 800×600.
- Lit-Modus; gleiche Kamera, Pose, Beleuchtung und Viewportgröße.
- Full-Surface-Profil: 34 Bibliotheken, 2.807 Normalmap-Zuordnungen.
- Modell und alle getesteten Inhalte stammen aus `Data/NAP/`.
- Je Lauf wurde ein geprüftes NAP-Overlay mit genau einer `fx/SurfaceMaps.ini`
  als einzigem Inhalt verwendet. Die aktive Manifest-Priorität war 100,
  Mount-Reihenfolge 14 und der explizite Override auf das `surface`-Paket
  begrenzt. Jedes Overlay bestand die Prüfung `nexus-pack verify`.
- Vor jedem Start waren die SHA-256-Werte und Größen der 14 Archive gegen das
  aktive Manifest verifiziert. Kein loses `DATA` wurde als Laufzeitquelle
  verwendet.
- Bei Einzelkarten-A/B blieb nur die jeweilige Normalmap-Stärke aktiv; die
  anderen beiden Mapping-Stärken waren 0. Roughness-/Metallic-Maps und PBR-
  Reflexionen waren aus, diffuse IBL war an. Nur der Surface-Normalmap-Schalter
  wechselte innerhalb desselben Prozesses.

## Einzelkarten-Ergebnisse

| Karte | Stärke im Einzeltest | Pixeländerungen Normalmap aus/an | Normiertes RMSE | Sichtbarer Befund |
|---|---:|---:|---:|---|
| `dig_site_16.tga` | 0,73 | 0 | 0 | In dieser Ansicht kein messbarer Beitrag. |
| `dig_site_18.tga` | 1,09 | 79.737 | 0,0439292 | Größter sichtbarer Beitrag; ausgeprägte Streifen und Sprenkel. |
| `dig_site_20.tga` | 4,48 | 12.195 | 0,0241943 | Deckenmuster und Streifen sichtbar. |

Die Aufnahmen sind jeweils paarweise: [Karte 16 an](map16-on.png) / [aus](map16-off.png),
[Karte 18 an](map18-on.png) / [aus](map18-off.png), [Karte 20 an](map20-on.png) /
[aus](map20-off.png).

## Gemeinsame Kalibrierung

Der Originalsatz 0,73 / 1,09 / 4,48 änderte 96.493 Viewport-Pixel und hatte
ein normiertes RMSE von 0,0441831. Ein gemeinsamer Satz 0,73 / 0,50 / 2,00
erreichte RMSE 0,0242191. Er behielt im visuellen Vergleich Oberflächendetails und reduzierte die harschen Streifen deutlich:
[Kalibrierung an](medium-on.png) / [aus](medium-off.png).

Die Generatorbewertung der angepassten Karten ergibt für `dig_site_18` bei
0,50 eine mittlere Neigung von 3,233° und P95 13,6565°; `dig_site_20` bei 2,00
ergibt 3,891° und P95 13,3597°. Beide liegen im vom Data-Generator definierten
P95-Zielbereich 12–40°. Die niedrigeren Probe-Stärken 0,35 und 0,75 lagen
unterhalb des Bereichs und wirkten zu flach.

Übernommen wurden 0,73 / 0,50 / 2,00 in `Data/SDK/fx/SurfaceMaps.ini`,
der NAP-Kopie `Data/NAP/Surface/fx/SurfaceMaps.ini` und
`Data/tools/surface_maps_params.json`. Die beiden INI-Dateien sind bytegleich
(SHA-256 `dff41a54f4138b687d50ea8c11ae38b3d7e21a1ff4fafbce4ee8bd3b3f35938b`).
Die aktualisierte Datenbewertung steht in `Data/surface-maps-bewertung.md`.

## Grenzen

Dies ist eine kontrollierte ModelViewer-Ansicht unter llvmpipe, keine
Hardware-GPU- oder Spielabnahme. Die Karten sind aus Diffuse-Helligkeit
abgeleitete Normalenschätzungen. Die Kalibrierung beweist weder die allgemeine
Richtigkeit der erzeugten Tangenten noch die Materialqualität weiterer Assets.
Die zuvor dokumentierte Tangenten-Aus-Kontrolle und der umfassendere Original-
Satz-A/B liegen in [`BW Pygar airlock Normalmap A/B`](../bw-pygar-airlock-normalmap-ab-20261018/README.md).

## Bilddateien

| Datei | SHA-256 |
|---|---|
1d03258cbb266bcd8caa9c2a620fc4e427b9525d136aaba930b20a94668b551a|map16-off.png|
81f4b9c7e847af222c9cda5332df6e84da57ea4b6c30f82d960ba597e0cfa55a|map16-on.png|
f4509e211132b222fc092bc18e9f719bffd66c72a699ab59014af6db2f26f8fa|map18-off.png|
25bd0f8f2dea6fb9f58a2a599fda466d71dbfb06bfb97bb1ec2273d7f50be8dd|map18-on.png|
f4509e211132b222fc092bc18e9f719bffd66c72a699ab59014af6db2f26f8fa|map20-off.png|
c13022de5b7e4b8f3c4c0ac10623f972898b0913761138f781366e90e1ffa7fd|map20-on.png|
f4509e211132b222fc092bc18e9f719bffd66c72a699ab59014af6db2f26f8fa|medium-off.png|
d8b93fc30e294a83021b125a528e364d13fea825966fa863b760d007dd627078|medium-on.png|
