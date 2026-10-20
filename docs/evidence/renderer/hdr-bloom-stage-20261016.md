# HDR Bloom-Stufe (2026-10-16)

## Umsetzung

- Die optionale Bloom-Stufe filtert lineare HDR-Szenenwerte anhand der
  Rec.-709-Luminanz `dot(RGB, (0.2126, 0.7152, 0.0722))` ab einer Schwelle
  von 1.0. Ein weicher Knee von 0.5 verhindert einen harten Übergang an der
  Schwelle. Gesättigtes Blau trägt dadurch weniger zur Luminanz bei als Grün.
- Die Extraktion wird auf ein Viertel der Szenenauflösung heruntergesampelt,
  danach wird ein separabler 5-Tap-Gauß-Blur horizontal und vertikal ausgeführt.
  Das geblurte Licht wird vor der ACES-Kurve zur linearen Szene addiert.
- `render_bloom` ist standardmäßig ausgeschaltet und unabhängig von
  `render_hdr` gespeichert. Die CLI akzeptiert `--render-bloom=on|off`; der
  Effekte-Tab kann den Schalter während des Spiels ändern. Schwelle und Stärke
  sind über `render_bloom_threshold` (0–8, Standard 1) und
  `render_bloom_strength` (0–2, Standard 0,18) konfigurierbar; beide können
  auch mit `--render-bloom-threshold` und `--render-bloom-strength` überschrieben
  werden. Ohne HDR greift die Bloom-Einstellung nicht. Bei nicht unterstützten
  Bloom-Zielen bleibt HDR aktiv und Bloom wird für den Prozess deaktiviert.
- Bloom-Targets werden in den geschätzten GPU-Speicher der AA/HDR-Ziele
  eingerechnet und mit diesen beim Resize/Dispose freigegeben.

## Grenzen und Prüfung

- Release-Build am 2026-10-16 mit .NET 10 erfolgreich; der Shadercompiler hat
  beide neuen Bloom-Bundles verarbeitet. Am 2026-10-17 wurde die Extraktion
  auf Rec.-709-Luminanz vereinheitlicht und erneut im Release-Build kompiliert.
- Der NAP-Laufzeitpfad ist auf OpenGL/Mesa vorhanden. Ein kontrollierter
  gesättigt-blau/grün/weiß-Vergleich für diese Luminanzschwelle, Bloomstärke,
  Halo-Radius und Performance bleibt offen.
- Dies ist ein einfacher Viertelauflösungs-Blur und noch keine mehrstufige
  Bloom-Pyramide. UI-/Cutscene-Blur bleibt davon getrennt.
- Der Effekte-Tab hat nun Regler für beide Werte. Sie werden beim Verlassen der
  Optionsseite übernommen und mit den übrigen Clientoptionen gespeichert.
