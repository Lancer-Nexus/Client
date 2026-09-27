# LLM-gestützte Client-UI-Tests

Für lokale Tests des **Debug-Clients** gibt es eine kleine Linux-Steuerhilfe:
`tools/first-client-e2e/llm-client-control.py`. Sie findet sichtbare Fenster
nur dann, wenn der laufende Prozess aus `output/dev/client` stammt. Release-
Ausgaben werden nicht angesprochen. Die Hilfe steuert die normale UI und fügt
keine Test-API in das Spiel ein.

## Voraussetzungen

- Client-Debug-Build wurde nach `output/dev/client` gesammelt und läuft.
- `output/dev/client/lancer.pdb` ist als Debug-Build-Marker vorhanden; ohne
  diese Symboldatei verweigert das Werkzeug die Steuerung.
- Eine grafische Sitzung mit X11/XWayland sowie `xdotool` und ImageMagick
  (`import`) sind verfügbar.
- Für Gateway-Tests laufen Gateway, Coordinator und GameServer; der Client
  verwendet eine dafür vorbereitete Debug-Konfiguration.

## Aufrufe

Vom Repository-Stamm aus:

```bash
python3 tools/first-client-e2e/llm-client-control.py status
python3 tools/first-client-e2e/llm-client-control.py screenshot /tmp/client.png
python3 tools/first-client-e2e/llm-client-control.py click 0.50 0.68
python3 tools/first-client-e2e/llm-client-control.py key Return
```

`click` verwendet Fensteranteile zwischen `0.0` und `1.0`, damit Tests nicht
von einer festen Auflösung abhängen. `status` liefert PID, Fenster-ID, Titel
und den überprüften Debug-Programm-Pfad. Bei mehreren Fenstern kann die
Fenster-ID mit `--window ID` vor dem Unterbefehl angegeben werden.

Text kommt über stdin statt über ein Kommandozeilenargument. Dadurch landen
Passwörter nicht in der Prozessliste oder Shell-History:

```bash
printf '%s' 'test@example.invalid' | python3 tools/first-client-e2e/llm-client-control.py type
```

Für Passwörter sollte der Text direkt aus einer Datei mit restriktiven
Berechtigungen gelesen werden, beispielsweise:

```bash
python3 tools/first-client-e2e/llm-client-control.py type < /tmp/test-password.txt
```

Die Hilfe führt keine Login-Daten selbst, protokolliert keine Texte und lässt
Gateway-Tokens unangetastet. Sie bietet allgemeine UI-Eingabe und Screenshots;
Testabläufe und ihre Erfolgskriterien bleiben in den jeweiligen Smoke-Tests.

## Login und Charakter für Debug-E2E

Der Debug-Client kann den Gateway-Login beim Start ohne UI-Klicks ausführen.
Vom Debug-Ausgabeordner:

```bash
./run.sh config/first-e2e.ini --credentials-file=/path/to/test-account.txt
```

The file uses `Email: ...` and `Password: ...` lines and should have mode 0600.
Its path is visible in the process list; the password is not.

Für einen automatischen Charakterbeitritt ergänze `--character`:

```bash
./run.sh config/first-e2e.ini --email=test@example.invalid --password=TESTPASS --character=TestPilot
```

Der vollständige Charaktername wird ohne Beachtung der Groß- und
Kleinschreibung verglichen. Ist er nicht vorhanden, bleibt die Charakterauswahl zur
manuellen Wahl geöffnet. Ohne `--character` öffnet der erfolgreiche Login
ebenfalls direkt die Charakterauswahl. Die Parameter sind nur in Debug-Builds
verfügbar. Übergib hier ausschließlich kurzlebige Testzugänge: Kommandozeilen-
Argumente können während des Prozesses für andere lokale Prozesse sichtbar sein.
