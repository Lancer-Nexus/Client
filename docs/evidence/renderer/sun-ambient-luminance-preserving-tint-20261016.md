# Sonnenambient: luminanzerhaltende Farbmischung (2026-10-16)

## Änderung

`RenderHelpers.ApplySunAmbient()` mischt 25% des lokal attenuierten Sonnenfarbmittels mit dem authored Ambient. Danach skaliert es das Ergebnis so, dass seine Rec.-709-Luminanz dem authored Ambient entspricht. Weißes Systemlicht erhält damit den bisherigen Farbwert; rotes und grünes Licht verschieben den Farbton, ohne den mittleren Helligkeitswert des Ambient-Anteils abzusenken.

Der aktuelle Forward-Pfad hat keinen HDR-Lichtpuffer. Deshalb werden Werte, die durch die luminanzerhaltende Skalierung über 1 steigen würden, auf den normalisierten Bereich begrenzt. Bei sehr hellen Ambientfarben kann diese Begrenzung die Luminanz reduzieren; für übliche, niedrigere Systemambientwerte greift sie nicht.

## Regressionen

Die fokussierten Fälle prüfen den ausgeschalteten Legacypfad, Weiß als unveränderten Kontrollfall, rote und grüne Farbmischung sowie die lokale Lichtgewichtung. Ein weißes Ambient mit rotem Licht prüft zusätzlich, dass kein Kanal über 1 ausgegeben wird.

## Noch offene Laufzeitabnahme

Die vorherigen BW04-Bilder entstanden mit der alten Produktmischung und sind keine Abnahme für diese Formel. NAP-only-A/B-Aufnahmen mit identischem Simulationszustand müssen die neue Mischung in weißen, roten und grünen Systemen zeigen. Der Options-Rücktest muss außerdem den Schalter nach Verlassen und erneutem Öffnen der Optionen bestätigen.
