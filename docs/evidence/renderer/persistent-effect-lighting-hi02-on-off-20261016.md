# Persistente Effektlichter: Hi02 Laufzeitbilder

- Datum: 2026-10-16
- Eingabedaten: ausschließlich aktive NAP-Pakete aus `Data/NAP/`; 13 Pakete, kein loses `DATA`-Verzeichnis.
- Spielstand: `hi02-test.fl` aus dem vorherigen Hi02-Renderer-Lauf.
- Der verwendete Release-Build enthält zusätzlich die lokale Engine-Trail-/Flame-Lichtweitergabe aus `Engine.cs`. Beide Starts erreichten `Finished loading game data`, `Interface loaded`, `World restore completed for Hi02` und `Entering system Hi02`.
- Vergleich: `--render-transient-effect-lights=on` links, `off` rechts; Partikeleffekte an, Effektlichtbudget 128, dynamisches Lichtbudget 32.

Die Aufnahmen stammen aus dem Release-Build mit Engine-Hardpoint-Lichtern. Die Szene läuft während der Aufnahme weiter. Gegner, Schüsse, Partikel und Kameraansicht unterscheiden sich zwischen den Bildern. Das Bildpaar dokumentiert deshalb die getesteten Laufzeitoptionen und die Szene, isoliert aber nicht den visuellen Beitrag eines einzelnen persistenten Fuse-Lichts. Eine kontrollierte Standbild-/Pixelkalibrierung bleibt offen.
