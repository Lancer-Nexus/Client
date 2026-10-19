# Shader-Lebensdauer: Prüfung vom 13.10.2026

Shader/IShader und ShaderBundle unterstützen nun Dispose. Ein RenderContext
registriert jede öffentliche Shader-Erstellung und besitzt die Referenzen bis
zur expliziten Freigabe oder `DisposeShaders`. Die Shaderzählung zählt lebende
Wrapper. Der Shader-Scope lässt nach dem Schließen keine Neuerstellung zu;
Shaderzugriffe und Lebensdaueroperationen verlangen den zugehörigen Renderthread.
Dies verhindert ein natives Free aus einer Worker-/Finalizer-Threadausführung.

GLShader.Dispose löst den angewandten Shaderzustand, entfernt das GL-Programm
und gibt seinen NativeBuffer frei. GLBind fragt dabei den wirklichen aktuellen
GL-Programmzustand ab, da native Renderer die Cachewerte umgehen können. Ist
das Programm noch gebunden, wird es zuerst entbunden. Die GL-Deletion folgt den
[Khronos-Regeln](https://wikis.khronos.org/opengl/GLAPI/glDeleteProgram), welche
für weiterhin gebundene Programme eine verzögerte Löschung vorsehen.

Die Bindings für DeleteProgram/IsProgram wurden im Generator registriert und
die GL.Generated.cs daraus regeneriert. Kein anderer generierter Funktionscode
wurde geändert. GLShader räumt auch temporäre Vertex-/Fragmentobjekte und
Uniformpuffer bei Aufbaufehlern auf. ShaderBundle räumt bereits gebaute Varianten
bei einem späteren Fehler auf; doppelte Featureeinträge werden abgewiesen.
RenderContext räumt seine bis dahin registrierten Shader bei Init-Fehlern auf.

SDL2/SDL3 rufen DisposeShaders im finally nach dem Anwendungscleanup und vor
Platform.Shutdown/SDL_Quit auf. Andere RenderContext-Ressourcen werden dadurch
nicht automatisch freigegeben. Shader aus geschlossenen Bundles/Scopes sind
nicht weiter benutzbar. Wiederholtes Dispose ist idempotent.

## Verifizierte Prüfungen

- 94 relevante Tests bestanden, darunter fünf neue Lebenszyklustests: explizite
  Shader-/Bundlefreigabe, Scope-Shutdown, Worker-Abweisung und Bundle-Rollback.
- Finale Solution-Kompilierung: null Fehler, 52 Warnungen.
- Null-Renderer-Tests liefen mit der gebauten libblurgtext auf LD_LIBRARY_PATH.
  Der erste Lauf ohne sie scheiterte; das war keine grüne Teilprüfung.
- Im NAP-only-SDL3/OpenGL-Test bleiben alle sieben Normalmap-GPU-Readbacks grün.
  IsProgram bestätigt das Vorhandensein des Probeprogramms vorher und dessen
  Entfernung nach Dispose. NativeBuffer-Bilanz vor/nach dem Test: jeweils
  13.309.656 Bytes; Kontextshader vor/nach: jeweils 378.
- Normaler Mainmenu/Options/Escape/Exit-Pfad beendet den Client regulär.
  Nach Game.Run: `OwnedShaderCount=0`, `Shader.TotalShaders=0`. Erzwungene GC
  erzeugt keinen NativeBuffer-Leakhinweis aus GLShader.
- Der externe Klickcontroller traf zunächst einen teilweise geschriebenen
  JSON-Beobachtungsstand. Nach begrenztem Read-Retry wurde derselbe laufende
  Client weitergeprüft und regulär beendet; er wurde nicht neu gestartet.

## Kontextwechsel und globale Shader-Bundles

`AllShaders` hält die eingebauten `ShaderBundle`-Instanzen und den
`iscompiled`-Status in statischen Feldern. `RenderContext.DisposeShaders()`
schließt den Shader-Scope und gibt dessen Shader frei; `ShaderBundle.Get()`
weist Zugriffe auf freigegebene Bundles/Shader anschließend zurück. Ein erneuter
`AllShaders.Compile()`-Aufruf setzt den statischen Status nicht zurück und kehrt
früh zurück. Die partiellen `CompileBillboard()`-/`CompilePhysicsDebug()`-Pfade
verwenden `??=` und ersetzen ein bereits vorhandenes Bundle ebenfalls nicht.

Damit ist ein zweiter RenderContext nach der Freigabe des ersten im selben
Prozess über diese globalen Bundles nicht wiederverwendbar. Die aktuellen SDL2-
und SDL3-Pfade erstellen den Context einmal pro Game-Lauf und schließen ihn
beim Cleanup; ein Geräteverlust-/Kontext-Neuerstellungspfad ist nicht belegt.
Das ist deshalb ein Architektur-Risiko für Kontextwiederherstellung und
Mehrfachkontexte, keine bestätigte Störung des normalen Einzel-Context-Laufs.
Die Bundles müssen vor einer solchen Erweiterung einem RenderContext gehören
oder kontrolliert pro Kontext neu aufgebaut werden.

### Abnahmekriterien für eine Kontext-Neuerstellung

- Jedes Built-in-Shader-Bundle gehört eindeutig zu genau einem `RenderContext`;
  kein statisches `iscompiled`-Flag darf einen zweiten Kontext als bereits
  initialisiert behandeln.
- Bundlezugriffe auf disposed Shader bleiben fail-fast; ein neuer Kontext erhält
  neue Shader und kann sie unabhängig vom alten Scope freigeben.
- Fehler beim Aufbau eines neuen Kontextes geben alle bis dahin erstellten
  Shader dieses Kontextes frei.
- Ein Folgepatch prüft zwei aufeinanderfolgende Kontexte im selben Prozess und
  verifiziert Freigabe sowie Neuaufbau.

## Grenzen

Keine Windows-/SDL2-/GLES-Laufzeitprüfung und kein Geräteverlust-/Crash-Test.
GL-Compile-/Link-Fehler werden im Code aufgeräumt, sind hier nicht nativ
fehlerinjiziert worden. Diese Änderung führt keine allgemeine GPU-Fence-
Verwaltung ein und aktiviert noch keine Supplemental-Surface-Sampler. Textur-,
Framebuffer-, Renderer2D- und Native-Textengine-Lebensdauer bleiben gesonderte
Arbeitspakete. Die Shaderfreigabe ist eine Grundlage für den Upload-/Residencypfad.
