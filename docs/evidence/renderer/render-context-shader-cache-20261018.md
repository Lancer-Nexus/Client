# RenderContext-gebundener Built-in-Shadercache

Stand: 2026-10-18

`AllShaders` hielt die Built-in-`ShaderBundle`-Instanzen in statischen Feldern
und verwendete ein prozessweites `iscompiled`-Flag. Ein neu erzeugter
`RenderContext` bekam damit die bereits an den alten Kontext gebundenen
Shaderobjekte zurück. `AsteroidBandMaterial` hielt zusätzlich einen
`Shader` statisch.

Patch `2386-render-context-scoped-shader-cache.patch` bindet den Cache an die
Identität des aktiven `RenderContext`. Bei einem Kontextwechsel werden die
statischen Bündelverweise und das Kompilierungsflag zurückgesetzt; die
GPU-Freigabe der alten Shader bleibt Aufgabe ihres bisherigen `RenderContext`.
Das Asteroidenband fragt seinen Shader jetzt aus dem aktiven Built-in-Bündel ab.
Fehler beim Kompilieren setzen das Flag zurück, sodass ein erneuter Aufruf
fehlende Bündel vervollständigen kann.

Der Headless-Regressionsfall kompiliert die Built-in-Bündel, schließt den ersten
Shader-Scope und prüft nach dem Wechsel auf einen zweiten Kontext, dass ein
anderer, noch nicht entsorgter Asteroid-Band-Shader bereitsteht. Der gezielte
Testlauf bestand mit 1/1 Tests unter .NET 10. Das bestätigt die verwaltete
Kontextbindung; echte GL-Kontextzerstörung, Gerätewechsel und native
Fehler-Injektion sind damit nicht abgenommen.
