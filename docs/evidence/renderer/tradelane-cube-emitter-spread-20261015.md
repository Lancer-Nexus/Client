# Tradelane-Effekt: ALE-Emission und Cube-Emitter (15.10.2026)

## ALE-Befund

`Data/NAP/Effects/fx/misc/gf_tradelaneinship01.ale` wurde mit LibreLancers eigenem `AleFile`-Parser ausgelesen. Die Datei enthält 15 Referenzen im Haupteffekt `tradelaneinship01`, acht Verbindungen und insgesamt drei Effekte. Die Referenzen verwenden `FxRectAppearance`, `FxConeEmitter`, `FxAirField`, `FxCubeEmitter` und `FxParticleAppearance`; diese Klassen sind im `ParticleLibrary.NodeFromAle` registriert. Der CRC `ee223b51` bezeichnet den erwarteten Attachment-Knoten (`CRC_ATTACHMENT_NODE`), nicht eine fehlende Effektklasse.

Der Effekt `tradelanedonutshooter` enthält einen `FxCubeEmitter`, der über `FxParticleAppearance.LifeName` den Effekt `tradelaneship01donut` für seine Partikel startet. Die NAP-Emitterdaten setzen `CubeEmitter_MinSpread=0` und `CubeEmitter_MaxSpread=15` (Grad), neben einem Druckwert von 10. Die Zufallskomponente des Spread-Winkels ist damit für diese Originalressource relevant.

## Eingrenzte Clientabweichung

`FxCubeEmitter.SetParticle` verwendete bisher `RandomInCone`, dessen Implementierung sofort `Vector3.UnitY` zurückgab. Die darunterliegende zufällige Spread-Berechnung war auskommentiert. Dadurch wurden die NAP-Min-/MaxSpread-Werte für Würfelemittoren ignoriert und deren Partikel alle in derselben lokalen Richtung gestartet.

Der Client besaß bereits eine frühere zufällige `RandomCube`-Berechnung, die Min-/MaxSpread auswertet, wurde aber nicht mehr aufgerufen. Patch 2102 aktiviert diesen vorhandenen Pfad und korrigiert seine Winkelgrenzen auf `z ∈ [cos(maxSpread/2), cos(minSpread/2)]`. Die frühere lineare Obergrenze `1 - minSpread/2` konnte bei positivem Minimum unter der Untergrenze liegen; dann wurden keine gültigen Winkel aus dem beabsichtigten Bereich gezogen. Die Halbwinkel-Konvention des vorhandenen Codes bleibt erhalten. Eine Retail-/Client-Bildabnahme der kompletten Trade-Lane-Sequenz bleibt offen.

## Prüfumfang

- NAP-INI- und ALE-Dateivorkommen sowie die konkreten Emissionswerte wurden direkt gelesen.
- Knotenauflösung wurde mit LibreLancers `AleFile`-Parser geprüft.
- `LibreLancer.csproj` wurde mit .NET 10 im Release-Modus gebaut: 0 Fehler,
  177 Warnungen. Die Warnungen stammen aus dem bestehenden Projektbestand.
- Die vollständige Client-Patchserie rekonstruiert einschließlich Patch 2102
  jetzt 614 Dateien unverändert zum angewendeten Overlay.
- Ein synchroner visueller Eintritts-/Fahrt-/Austrittvergleich wurde nicht durchgeführt; Screenshot-Aufruf in der GNOME-Sitzung endet mit `AccessDenied`.
- Ein LancerEdit-Start mit `--data` auf die isolierte NAP-Testinstallation lud
  alle 53 Systeme und meldete `Finished loading game data`; die Testinstallation
  bezog ihre 13 Archive aus denselben zuvor verifizierten NAP-Paketen und hatte
  kein loses `DATA`-Verzeichnis. Der Editor blieb im GUI-Lauf aktiv, jedoch
  verweigert die GNOME-Sitzung den Screenshot-Aufruf (`AccessDenied`). Damit
  sind Paket-Mount und Datenladen bestätigt, nicht das Öffnen oder die sichtbare
  Darstellung der ALE-Vorschau. Der Bildvergleich bleibt offen.
