# SDL3-Clipboard: Lebensdauer des MIME-Texts

## Ursache

Die statische SDL3-Zeigertabelle `_mimeTypes` enthielt einen Zeiger auf den
UTF-8-Text `application/x-librelancer-array`. Der zugehörige `NativeBuffer`
war nur eine lokale Variable des statischen Konstruktors. Der Garbage
Collector konnte ihn nach dem Konstruktor finalisieren und damit den Text
freigeben. Die Tabelle behielt den Zeiger für spätere Clipboard-Aufrufe.

Die Warnung beim Client-Start zeigt diesen verlorenen Speicherbesitzer an.
Der Finalizer gibt die Bytes zwar frei, hinterlässt dabei aber einen ungültigen
Zeiger. Die Korrektur hält den Textpuffer als statisches Feld gemeinsam mit
seiner Zeigertabelle am Leben. Beide kleinen Puffer gehören zur gemeinsam
verwendeten, prozessweiten MIME-Beschreibung.

## Nachweise vom 12.10.2026

- Der neue GC-Test schlägt mit dem bisherigen Code fehl: Nach erzwungenen
  Collections besitzt kein statischer Puffer mehr den Textzeiger.
- Mit der Korrektur besteht derselbe Test. Er prüft Besitzer, Größen,
  Nicht-Freigabe und vollständigen nullterminierten UTF-8-Text. Der Test
  initialisiert nur den statischen Konstruktor; er benötigt kein SDL-Fenster
  und liest den Text erst nach Prüfung eines gültigen Besitzers.
- Vollständiger Build von `LibreLancer.sln`: 0 Fehler, 20 Warnungen.
- Der frisch gebaute NAP-Client erreicht Spiel-/UI-Laden ohne die bisherige
  NativeBuffer-Warnung. Native Optionen-/Escape-/Exit-Eingaben bestehen;
  regulärer Prozessabschluss mit Code 0, ohne Timeout.

Die Änderung liegt im bestehenden Funktionspaket 2006, der Regressionstest
in 2009 und diese Dokumentation in 2010. Die Serie enthält weiterhin 13
Pakete. Die Rekonstruktion wird gegen die gepinnten Baselines geprüft.

[Prüfbefunde und Quell-/Assembly-Hashes](evidence/sdl3-clipboard-mime-lifetime-20261012.json).

## Grenzen

Der fokussierte Test und der Client-Lauf sind kein Nachweis für die gesamte
Testsuite oder alle NativeBuffer-Besitzer. Ein Schreib-/Lese-Rundlauf mit der
Betriebssystem-Zwischenablage wurde nicht ausgeführt. Gameplay, Cluster und
die neuen Normalmap-/Vulkan-Funktionen bleiben gesonderte Arbeitsschritte.
