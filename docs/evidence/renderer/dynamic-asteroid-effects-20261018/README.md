# Dynamische Asteroiden-Effekte

Patch 2376 verbindet `Asteroidarch.ini: particle_effect` über `Effects.ini` und
`ResolvedFx` mit einem `ParticleEffectRenderer` am dynamischen Asteroiden. Die
Effektlichtkurve wird angebunden; der bestehende globale Partikeleffekt-Schalter
bleibt maßgeblich. Parser-Regressionen decken `gf_blsmallasteroid`,
`gf_dynamiclavaast` und den optionalen Eintrag ab (3/3 bestanden). Der Release-
Client baut mit 0 Warnungen und 0 Fehlern.

Ein frischer Release-Start mit ausschließlich dem aktiven NAP-Snapshot lud alle
Daten, initialisierte die UI und betrat `FP7_system`. Die früheren drei
`Unknown entry particle_effect`-Warnungen sind verschwunden. Der FP7-Intro-Lauf
zeigt keine dynamischen Asteroiden; er bestätigt Parser und Datenstart, aber
keine sichtbare Asteroiden-Effekt-Wirkung. Der gezielte Asteroidenfeld-A/B und
der Retail-Vergleich bleiben offen. Bedingungen und relevante Logzeilen stehen
in [`runtime-summary.txt`](runtime-summary.txt); das verifizierte Manifest und
die Paketquellen sind beigefügt.
