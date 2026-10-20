# Hi02-Directional-Light-Laufzeitprüfung

Der Schattenpfad wählt nun die aktive gerichtete Systemlichtquelle mit der
höchsten endlichen Rec.-709-Direkthelligkeit. Für die Hi02-Daten unter
`Data/NAP/.runtime-test-stage/package-inputs-20261016/World/universe/systems/hi02/Hi02.ini`
ergibt die Formel:

| Quelle | INI-Farbe | Y (gerundet) |
| --- | ---: | ---: |
| `Hi02_system_light` (steht zuerst) | (232, 80, 0) | 106,54 |
| `Hi02_walkerlight` | (210, 118, 26) | 130,92 |

Damit bildet die neue Auswahlregel die zweite, hellere direkte Quelle ab. Der
Lauf benutzte einen temporären Spielstand, dessen `[Player]`-System auf
`Hi02_system` gesetzt war; die beiden ursprünglichen Spielstände wurden nicht
verändert. Der Client meldete `Game: Finished loading game data`, `System:
Hi02` und `SpawnFlow: Initial spawn requested`. Er wurde mit
`--render-shadows=on` gestartet.

Der Start war NAP-only: die aktive Manifestdatei
`lancer-renderer-aa-profile-20261016` hatte SHA-256
`e6d56a0f74095c89712f35f35586bfbe5db19d28f0f106654037a53140fc3ca1`; Größe
und SHA-256 aller 13 Pakete passten zum Manifest und jedes Archiv bestand
`nexus-pack verify`. Die Paket-Eingaben lagen unter `Data/NAP/`; das
Test-Installationsverzeichnis enthielt kein loses `DATA`-Verzeichnis.

## Aussagegrenze

Die Laufzeit erreichte den Spiel-Spawn in Hi02, protokollierte aber weder den
ausgewählten Lichtnamen noch ein erfolgreich gerendertes Schattenbild. Die
X11-Aufnahme war auf Intel HD Graphics 4600 mit Mesa 23.2.1 schwarz. Dieser
Smoke belegt daher Datenladen und Spawn im Mehrlichtsystem, aber keine visuelle
Schattenwirkung und keine Laufzeitbestätigung der Lichtauswahl. Dafür bleiben
ein sichtbarer Debug-Nachweis oder ein kontrolliertes Bildpaar auf einer
funktionierenden Renderkonfiguration sowie ein Ein/Aus-Leistungsvergleich
offen.
