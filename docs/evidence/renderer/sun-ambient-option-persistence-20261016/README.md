# Sonnenambient-Option: Rücksetz- und Wiederöffnungsprüfung (2026-10-16)

## Änderung

`OptionsWindow.do_goback()` las den Sonnenambientwert erneut aus dem Checkbox-Widget und konnte damit den im laufenden Spiel bereits angewendeten Wert überschreiben. Der Rückweg übernimmt jetzt ausschließlich für diese Option den aktuellen Rendererwert aus `Game.GetCurrentSettings()`. So bleiben die übrigen noch nicht bestätigten Optionen weiterhin im bestehenden Snapshot.

## NAP-only-Lauf

- Release-Client mit dem statischen BW04-Save `bw04-static.fl`; der Startbefehl setzte keinen `--render-sun-ambient`-Override.
- Laufzeitverzeichnis: `/tmp/lancer-explosion-fresh-20261016/runtime-app`, ohne loses `DATA`; 13 Archive aus `Data/NAP/` mit Größen-/SHA-256-Prüfung und `nexus-pack verify` bestätigt. Aktives Manifest: `lancer-explosion-qa-20261016`.
- Log erreichte `Finished loading game data`, `World restore completed for Bw04` und `Entering system Bw04`.
- `client.ini` startete mit `render_sun_ambient = true`. Im pausierten Effekte-Tab wurde die Option ausgeschaltet. Nach Rückkehr zum Spiel stand sie weiter auf `false`; nach erneutem Öffnen des Effekte-Tabs zeigte die Checkbox ebenfalls `false`.
- Aufnahme nach erneutem Öffnen: [Ambient ausgeschaltet und gespeichert](ambient-off-after-reopen.png). Die Datei `client.ini` im Testlauf enthielt zu diesem Zeitpunkt weiterhin `render_sun_ambient = false`.

Das verifiziert den Options-Rückweg und die Checkbox-Wiederherstellung im NAP-only-Lauf. Eine visuelle Kalibrierung der Lichtwirkung ist davon unabhängig und bleibt offen.
