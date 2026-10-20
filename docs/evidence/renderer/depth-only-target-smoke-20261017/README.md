# Depth-only-Ziel Smoke-Nachweis (2026-10-17)

Der Release-Client wurde mit OpenGL 4.6 auf Mesa 23.2.1 / Intel HD Graphics
4600 und den 13 NAP-Archiven aus `Data/NAP/.runtime-test-stage/package-inputs-20261016`
kurz gestartet. `packages/active.json` identifizierte
`lancer-render-20261016-data-nap-only`; Größe und SHA-256 aller 13 Archive
wurden direkt vor dem Lauf gegen das Manifest geprüft.

Ein nur für diesen Smoke-Lauf eingebauter Diagnoseaufruf erzeugte ein
64×64-`RenderTarget2D` mit `SurfaceFormat.Depth`, setzte es als aktives Ziel,
leerte den Depth-Puffer und las die Depth-Textur anschließend als `float[]`
aus. Der Client protokollierte:

```
Graphics: Sampleable depth-only target FBO created, cleared and read back (1..1).
```

Damit sind auf dieser OpenGL-Konfiguration FBO-Vollständigkeit, Depth-Attachment,
Clear und `GetTexImage`-Readback belegt. Der Diagnosecode wurde nach dem Lauf
entfernt und ist nicht Teil der Patch-Serie.

Nicht geprüft sind Shader-Sampling/PCF, GLES, das Null-Backend zur Laufzeit,
andere GPUs sowie Shadow-Caster- und Receiver-Pässe. Der Nachweis belegt nur
das Depth-Target-Fundament.
