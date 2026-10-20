# Lichtequipment: EmitRange unabhängig vom Billboard (15.10.2026)

## Quellcodebefund

`LightEquipRenderer.PrepareRender()` registrierte `LightEquipment.EmitRange` bisher nur innerhalb des Billboard-Sichtbarkeitspfads. Dazu gehören `flare_cone`, ein Frustumtest auf `BulbSize`, eine 20.000-Einheiten-Sichtweite und ein Kamera-zu-Licht-Physikraycast. Damit entfiel die Frame-Lichtquelle, sobald eines dieser Kriterien das dekorative Glühbild ausblendete, selbst wenn die `EmitRange`-Kugel den sichtbaren Bereich schneidet. `SystemLighting.AddFrameLight()` prüft bereits Lichtreichweite und priorisiert begrenzte Frame-Kandidaten nach geschätztem Beleuchtungsbeitrag.

## Änderung

Die lokale Frame-Lichtquelle wird nun separat registriert, wenn Lichtequipment eingeschaltet ist, seine aktive Blinkphase leuchtet, `EmitRange` positiv ist und die EmitRange-Kugel das Kamerafrustrum schneidet. Billboard-Frustum, Flare-Kegel und Nahsichtdistanz steuern nur, ob Bulb/Glow gezeichnet wird. Der Kamera-Licht-Raytest bleibt für Frame-Licht und Billboard aktiv: blockierte Quellen werden unterdrückt, damit unverschattetes Licht nicht durch Geometrie scheint. `forceCull` gilt weiter für beide Pfade.

Die Trennung verhindert, dass eine nur für die Darstellung bestimmte Flare-Kegel- oder Sprite-Occlusion-Prüfung die Beleuchtung sichtbarer Objekte abschaltet. Eine Retail-Bildabnahme, Schattenwirkung und objektbezogene Occlusion bleiben offen.

## Prüfung

- Der fokussierte Release-Testlauf kompilierte `LibreLancer` und die Testassembly erfolgreich; Compilerwarnungen bleiben bestehen.
- Fokussierter Release-Testlauf `FullyQualifiedName~LightEquipmentEmissionTests`: 11 bestanden, 0 fehlgeschlagen.
- Vollständige Client-Patchserienrekonstruktion: 616 Dateien verifiziert.
- `git diff --check`: sauber.
