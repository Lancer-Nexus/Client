# LibreLancer-Codeberg-Upstream: Übernahmestand

**Prüfdatum:** 15.10.2026  
**Upstream:** `https://codeberg.org/Librelancer/Librelancer.git`, Branch `main`  
**Geprüfter Upstream-Commit:** `1dc5b39b2a38a8357c96f2d87f5818659bc869b0`  
**Client-HEAD:** `03bfd87b8cbca1fcb250ba809487de2732466054`  
**Gemeinsamer Vorfahr:** `a34226c75e41b4b084884517c6894aa160018dcc`

## Status

Der aktuelle Codeberg-Branch wurde per Git eingelesen und mit dem Client-Branch verglichen. Acht der 23 Upstream-Commits wurden über die fokussierten Patches `2139` und `2140` in den Client-Overlay portiert. Es gab keinen Merge, Rebase oder Cherry-pick; der vorhandene Arbeitsbaum und die Fork-Basis blieben erhalten.

Seit dem gemeinsamen Vorfahren enthält Codeberg 23 Commits, der Client 183 nur lokal vorhandene Commits. Die Historien sind damit deutlich auseinander gelaufen. Ein direktes Merge oder Rebase in den bestehenden Client-Arbeitsbaum wäre nicht vertretbar. Änderungen müssen nach Funktion und Konflikten geprüft und in die Client-Patchstruktur portiert werden.

Der lokale Remote `origin` zeigt auf `Lancer-Nexus/Client` bei GitHub; `upstream` zeigt auf LibreLancer bei GitHub. Der Codeberg-Stand wurde separat unter `refs/remotes/codeberg/main` eingelesen. Der lokale GitHub-Ref `upstream/main` steht bei `af16981cc5357f635f503b32ffc2628459667588`, dem Trade-Planner-Merge in der Tabelle. Codeberg enthält danach weitere Commits bis `1dc5b39b`. Dieser Bericht bezieht sich auf Codeberg und nicht auf den älteren lokalen GitHub-Ref.

## Commits auf Codeberg seit dem gemeinsamen Vorfahren

| Commit | Datum | Änderung |
| --- | --- | --- |
| `1dc5b39b2` | 15.10.2026 | Importer: `axis`-Eigenschaft für Constructs erkennen |
| `709ce418b` | 15.10.2026 | SDL3-FileDialog: Lebensdauer der Filter korrigieren |
| `11221f323` | 15.10.2026 | SDL3Game: MIME-Typ-Array vor GC schützen |
| `80632b5d3` | 15.10.2026 | Model Editor: Undo und getrennte Modi |
| `6e17cb2da` | 14.10.2026 | LancerEdit: Animationen aus Modellen hinzufügen |
| `03fc55937` | 13.10.2026 | Blender-Export: ungültigen `Filmic`-View-Transform entfernen |
| `edd48c878` | 13.10.2026 | ScriptRunner: keine Nullwerte ins Log schreiben |
| `520f28f86` | 13.10.2026 | LancerEdit: Prozent-Escaping an C-Formatierung anpassen |
| `078f9c5e6` | 13.10.2026 | Content Editor: DefaultMaterialMap und Speichern ergänzen |
| `803fd40c6` | 12.10.2026 | Konfliktverursachendes NuGet-Paket entfernen |
| `4bf6765b6` | 12.10.2026 | Netzwerk: abgeleitete GUIDs statt LI-GUID senden |
| `fa3cc93bd` | 12.10.2026 | Netzwerk: Konten- und Charaktererstellung je IP begrenzen |
| `488b21966` | 11.10.2026 | Netzwerk: maximale Charakterzahl je Konto begrenzen |
| `0b3496a70` | 11.10.2026 | Netzwerk: Login und Charaktererstellung je IP begrenzen |
| `a91466c93` | 11.10.2026 | Netzwerk: aktive Verbindungen je IP begrenzen |
| `7c20faf24` | 11.10.2026 | Netzwerk: Login-Pakete in den Handshake verschieben |
| `af16981cc` | 11.10.2026 | Merge: Trade-Planner v1 (#382) in Codeberg `main` |
| `1b3a7f9c1` | 10.10.2026 | README: Einladungslink aktualisieren |
| `8ae9b66db` | 10.10.2026 | ModelExporter: Kameraeigenschaften als leere Nodes exportieren |
| `2695f4c2f` | 10.10.2026 | TextureData: beschädigte Texturen ohne Absturz behandeln |
| `74720cd74` | 10.10.2026 | Submodul-URLs für thorncompiler und lancerdecode aktualisieren |
| `415f3ff65` | 23.05.2026 | Trade-Planner: Marktdatei-Verarbeitung refaktorieren |
| `602bfb9dc` | 22.05.2026 | Trade-Planner v1 |

## Portierungsbewertung

- **Portiert in `2139-codeberg-2026-10-15-import-editor-port.patch`:** `2695f4c2f` (beschädigte Texturen), `8ae9b66db` (Kameraeigenschaften im ModelExporter), `520f28f86` (Formatzeichen in LancerEdit), `03fc55937` (Blender-View-Transform), `6e17cb2da` (Animationsimport mit Retargeting) und `1dc5b39b2` (Importer-`axis`).
- **Portiert in `2140-codeberg-lanceredit-and-material-followup.patch`:** `edd48c878` (Nullzeilen aus asynchroner ScriptRunner-Ausgabe ignorieren) und `078f9c5e6` (ScriptRunner-Log speichern und `DefaultMaterialMap` in ContentEdit verschieben). Der Textfilter wurde ergänzt; `DcDtBt` war im lokalen Materialtyp bereits vorhanden. Die Anpassungen behalten den lokalen `MainWindow`-Startpunkt und den bereits portierten ScriptRunner-Import bei.
- **Bereits lokal abgedeckt:** `709ce418b` (SDL-Dialog hält Filter und Callback bis zum asynchronen Callback), `11221f323` (MIME-Payload bleibt über statischen `NativeBuffer` erhalten), `803fd40c6` (EF-Design-Paket ist in `LLServer.Core.csproj` bereits nicht referenziert), `74720cd74` (thorncompiler/lancerdecode zeigen bereits auf Codeberg) sowie Trade-Planner-Commits `602bfb9dc`, `415f3ff65` und Merge `af16981cc` (lokaler, verlustfreier Port in Patch-Bundle 2005, dokumentiert im Upstream-Review).
- **Noch offen:** `80632b5d3` (großer Model-Editor-Undo-/Modus-Refactor) und die sechs Login-/Verbindungsänderungen `7c20faf24`, `a91466c93`, `0b3496a70`, `488b21966`, `fa3cc93bd` und `4bf6765b6`. Letztere liegen im Client an einer Gateway-eigenen Identitätsgrenze; vor einer Übernahme muss die jeweilige Schutzwirkung gegen Gateway-Login, Charakteranlage und GameServer-Handshake abgeglichen werden.
- **Nicht übernommen:** `1b3a7f9c1` ändert den LibreLancer-Einladungslink und ist projektspezifische Community-Metadaten, kein Client-Fix.
- **Netzwerkänderungen separat bewerten:** Die Login-Handshake- und Begrenzungsänderungen (`7c20faf24` bis `fa3cc93bd`) greifen in Anmeldung und Netzwerkprotokoll ein. Vor einer Portierung müssen sie mit Gateway-Authentifizierung, Clusterbetrieb und den lokalen Protokollverträgen abgeglichen werden.
- **Trade-Planner vergleichen:** Die zwei älteren Commits müssen gegen den bereits lokal portierten und angepassten Trade-Planner geprüft werden; nicht blind übernehmen.
- **Dokumentations-/Repository-Metadaten:** README-Einladungslink und Submodul-URLs sind vor Übernahme auf Relevanz für den Lancer-Nexus-Fork zu prüfen.

## Nächste Schritte

1. Die sechs Login-/Verbindungsänderungen gegen Gateway-Authentifizierung, Charakteranlage und GameServer-Handshake prüfen; passende Schutzmaßnahmen gegebenenfalls am Gateway-Port umsetzen.
2. Den Model-Editor-Undo-Refactor (`80632b5d3`) mit lokalen Surface-Map- und Importer-Erweiterungen abgleichen.
3. Overlay-Rekonstruktion ausführen; anschließend betroffene Builds, Tests und Laufzeitpfade prüfen.
4. Den Bericht nach jeder Portierungsrunde mit Commit, Patch, Prüfergebnis und offenen Punkten aktualisieren.

## Prüfumfang und Grenzen

Geprüft wurden die Git-Referenzen, der gemeinsame Vorfahr, die Commit-Anzahl und die Commitliste von Codeberg `main`. Patch 2139 und 2140 bestehen die vollständige Overlay-Rekonstruktion mit 683 Dateien. Der abschließende Release-Solution-Build mit .NET 10 endete mit 0 Fehlern und 131 Warnungen. Tests wurden nicht ausgeführt; Laufzeitprüfungen der Upstream-Portierungen bleiben offen.
