# GPU-Freigabequeue: Prüfung vom 13.10.2026

GpuFrameRetirement sammelt Callback-Aktionen auf dem Renderthread bis EndFrame.
Ein Frame erzeugt einen Batch; seine Aktionen werden erst vom Completioncallback
versucht. Fehlgeschlagene Submission stellt unaufgerufene Aktionen wieder her.
Doppelte Completion führt keine Aktion zweimal aus. Callbackfehler werden nach
Versuch aller Aktionen als AggregateException gemeldet; ein leerer Zähler ist
im Fehlerfall kein Nachweis, dass jede Ressourcenfreigabe erfolgreich war.

OpenGL stellt einen [Fence](https://wikis.khronos.org/opengl/GLAPI/glFenceSync)
hinter die vorherigen GPU-Kommandos und flusht den Stream im selben Context.
QueryFences prüft ohne Wartezeit und ruft nach Signal die Aktionen auf. WAIT_FAILED
löst einen Fehler aus und lässt Ressourcen pending. Ohne Sync wartet der
synchron arbeitende glFinish-Fallback; seine Laufzeit ist hier nicht auf einer
solchen Plattform gemessen. CompleteGpuWork wartet beim normalen Shutdown und
verarbeitet offene Fences. Der neue Finish-Bindingeintrag und GL.Generated.cs
wurden mit dem bestehenden Generator erstellt.

RenderContext integriert die Queue in EndFrame und schließt sie vor seiner
Shaderfreigabe. Neuer Zutritt während des Drain wird abgewiesen. Unvollständige
Completion hält den Scope offen und pending, statt eine Freigabe zu behaupten.
SurfaceGpuTexture.RetireAfterFrame sperrt seinen Texture-Getter sofort, hält
aber die native Textur bis zum Callback. Dispose nach Retire ist absichtlich
kein sofortiges Delete. Vor Retire bleibt explizites Dispose für bekannte
ungefährdete/synchron geprüfte Scopes verfügbar.

## Aktuelle Evidenz

- 124 fokussierte Tests bestanden, davon neun Queueprüfungen: Signal/Batch,
  Shutdown-Drain, Submissionretry, vollständige Callbackversuche, synchrone
  Callbackfehler ohne doppelte Freigabe, unvollständige Completion/Retry,
  falscher Thread und reentrante Zutrittsabwehr.
- Vollständiger Solution-Build: null Fehler und 20 Warnungen.
- Nativer Test mit SDL3, Mesa Intel HD Graphics 4600, OpenGL 4.6:
  Eine hochgeladene BC5-Textur wird mit Renderer2D tatsächlich gezeichnet und
  danach retired. Vor EndFrame: disposed=false, retired=true, pending=1.
  Nach GPU-Signal: disposed=true, pending=0. Der Test sah das Signal bei frame=2.
- Das Anwendungscleanup erzeugt und retired eine weitere Textur. Vor dem Drain
  bleibt sie gültig. Nach Game.Run: pending=0, shaders=0, cleanupTextureDisposed=true.
  Prozessende ist regulär mit Exitcode 0.

Die alte /tmp-Build-/NAP-Testumgebung war nicht mehr vorhanden. Der tatsächliche
Checkout mit initialisierten Submodulen wurde deshalb gebaut und seine Native-
Bibliotheken genutzt. Die Prüfung ist ein aktueller nativer Einzeltest, kein
neuer NAP-only-Gameplay- oder Menü-Nachweis. Historische NAP-Protokolle bleiben
in den früheren Dokumenten; sie beweisen diesen neuen Fencepfad nicht.
[Maschinenprotokoll](evidence/gpu-frame-retirement-20261013.json).

## Weiter offen

Dies liefert Ressourcenretirement, keinen gemeinsamen GPU-Cache, kein globales
Residencybudget und keine aktive Supplemental-Materialintegration. Crash- und
Geräteverlustpfade, native WAIT_FAILED-/GL-OOM-Fehlerinjektion, Windows/GLES und
Sync-freie Hardware bleiben ungetestet. Der synchrone Shutdown/Fallback kann
blockieren; daraus wird keine Performancezusage abgeleitet. Freigabeaktionen
müssen ihre Ressourcen selbst korrekt besitzen und dürfen nicht vorzeitig
außerhalb der Queue ein zweites Delete ausführen.
