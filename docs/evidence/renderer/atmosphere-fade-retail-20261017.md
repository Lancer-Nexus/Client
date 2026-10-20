# AtmosphereMaterial Fade: Retail-Daten- und Binärprüfung (2026-10-17)

## Retail-MAT

`/home/masterbee/Dokumente/FL/DATA/SOLAR/PLANETS/PLANET_EARTH/planet_earth.mat`
enthält den UTF-Knoten `Material library/planet_earth_atmosphere` mit:

| MAT-Feld | Wert |
|---|---:|
| `Type` | `AtmosphereMaterial` |
| `Fade` | `1.1` |
| `Alpha` | `0.6` |
| `Scale` | `1.025` |

Die Werte wurden mit `UtfFile.parseFile` und `Material.FromNode` gelesen,
nachdem der Harness wie der Spieldatenpfad eine globale `MaterialMap`
initialisiert hatte. Ohne diese Initialisierung wirft `Material.FromNode` bei
`MaterialMap.Instance.Get(...)` eine NullReferenceException; `MatFile` fängt
den Fehler pro Material ab und liefert anschließend eine leere Materialliste.
Der erste Harnesslauf ohne `MaterialMap` war daher kein Befund zur Retail-Datei.

Das bestätigt den MAT-Eingabewert. Es zeigt nicht, wie `shading.dll` ihn
verwendet.

Der gleiche Parserlauf über alle 239 `*.mat`-Dateien unter
`../Data/NAP/Material/` fand 78 Atmosphärenmaterialien in 78 Planeten-MATs. Alle
78 setzen `Fade=1.1` und verwenden die Textur `atmosphere`. `Alpha` variiert:
36 Materialien setzen `0.6`, 34 setzen `0.7`, vier `0.3`, zwei `0.4` und zwei
`0.5`. `Scale` ist bei 48 Materialien `1.025` und bei 30 `1.03`. Die
Verteilung bestätigt, dass `Fade` in diesem Retaildatenbestand konstant ist;
sie belegt weiterhin keine konkrete Shaderwirkung.

## Binärspur

- `shading.dll`, SHA-256
  `3c590a134b2f0bb4d6aecfbbca59dba58733017127705b25af535c2c7bfbc15d`.
- Ghidra 12.1.4, vorhandenes `FreelancerRetail`-Projekt, `-noanalysis`.
- Die untersuchten Material-VTable-Funktionen und der Render-/Bindepfad
  enthalten keine eindeutige Zuordnung des MAT-Felds `Fade` zu einer
  Shaderoperation oder Uniform.
- String-Xref-Suche in `flmaterials.dll` fand weder `AtmosphereMaterial` noch
  `Fade`.

Das ist kein Beweis, dass die Retail-Engine den Wert ignoriert: Feldzugriff und
Shaderberechnung können ohne symbolische Namen oder über generische
Materialdaten laufen. `Atmosphere.frag.hlsl` im Client verwendet den
übertragenen `Fade`-Parameter derzeit nicht. Eine Kurve wie `pow`, `lerp` oder
eine Alpha-Skalierung wäre mit den vorliegenden Belegen geraten und wird nicht
als Retail-Verhalten umgesetzt.

## Nächster belastbarer Schritt

Die Werte `Fade` und `Alpha` müssen in einer kontrollierten Planetenansicht
gegen den Retail-Client oder eine aus dem Originalshader rekonstruierte
Parameterwirkung verglichen werden. Danach kann die Shaderänderung mit einem
reproduzierbaren Bildvergleich abgesichert werden.

Die unveränderten Ghidra-Auszüge und Metadaten liegen außerhalb des Git-Repos
unter `/home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/`.

## Follow-up Ghidra inspection (2026-10-18)

The targeted `shading.dll` string references for `Fade` resolve to registered
`Fade 1D Texture` properties: texture name, wrap mode, and U/V address modes.
The associated material setup binds the 1D texture resource and configures its
sampler. These references do not identify a scalar MAT `Fade` uniform or its
shader equation. The inspection therefore narrows the earlier string search
but still does not establish how retail uses the MAT value `Fade=1.1`.

The function and instruction exports, with `shading.dll` hash metadata, are
stored under `/home/masterbee/Dokumente/FL/reverse-engineering/librelancer/exports/2026-10-18-atmosphere/`.
