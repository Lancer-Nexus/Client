# Tradelane-Reiseeffekt: Daten- und Laufzeitpfad (15.10.2026)

## Datenbestand

Der Client-NAP enthält die relevanten Originaldefinitionen und Ressourcen:

- `Data/NAP/Models/equipment/SelectEquip.ini` bindet `basic_trade_lane_eq` an `tl_player_travel = basic_tl_player_travel` und `tl_ship_travel = basic_tl_ship_travel`.
- `Data/NAP/Effects/fx/Effects.ini` definiert `basic_tl_player_travel` als `EFT_LANE_SHIP_TRAVEL`, referenziert `tradelaneinship01` und den Ton `tl_wind_int`.
- `Data/NAP/Effects/fx/misc/MiscAle.ini` bindet `tradelaneinship01` an `fx\misc\gf_tradelaneinship01.ale` und die SARMA-, Planetflare- und Kioncannon-Texturbibliotheken.
- `Data/NAP/Effects/fx/misc/gf_tradelaneinship01.ale` ist vorhanden (9.0 KiB).

## Tatsächlicher Clientpfad

`Tradelane` parst sowohl `tl_player_travel` als auch `tl_ship_travel`. `GameItemDb.CreateTradelaneEquipment` löst beide Definitionen in getrennte `ResolvedFx`-Objekte auf. Bei `SpaceGameplay.StartTradelane` hängt der Client `PlayerTravel` über `AttachShipEffect` als `ParticleEffectRenderer` an `player.ExtraRenderers` und startet den Effektton als Loop. Die Instanz aktualisiert und zeichnet über den allgemeinen Partikeleffektpfad; der Renderer berücksichtigt den normalen 20.000-Einheiten-Cull-Abstand.

Der Serverpfad für NPC-Schiffe startet `ShipTravel` separat als Netzwerk-FX über `SFuseRunnerComponent`. Das ist kein Ersatz für den Spielereffekt und erklärt allein keine Abweichung in der Spieleransicht.

## Befund und offene Abnahme

Die Definitionen werden gefunden und an den erwarteten Spiel- bzw. NPC-Pfaden konsumiert. Damit ist ein fehlender INI-Verweis oder eine fehlende ALE-Datei als Ursache der vereinfachten Darstellung nicht belegt. Der Audit weist auch nicht nach, dass die ALE-Geometrie, Partikelparameter, Blickwinkel oder Animation identisch zum Original gerendert werden.

Offen bleibt ein synchroner Vergleich der originalen Freelancer- und Client-Ansicht in denselben Eintritts-, Fahrt- und Austrittsszenen. Erst dabei lassen sich Abweichungen auf den ALE-Parser/-Renderer, die Effektauswahl oder die Laufzeitbindung eingrenzen. Es wurde in diesem Audit keine Renderfunktion geändert.
