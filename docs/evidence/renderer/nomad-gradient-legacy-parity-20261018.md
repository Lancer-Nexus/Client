# Nomad-Gradient: Legacy-Winkelabbildung

Stand: 2026-10-18

Der aktuelle HLSL-Fragmentshader bildete den normalisierten View-/Normal-
Dot-Product mit `(dot + 1) / 2` auf den Bereich `[0, 1]` ab. Das verändert
die Zuordnung der Texturkoordinate gegenüber dem historischen
LibreLancer-Shader `NomadMaterial.frag` aus Commit `4193dd4bd`: Dort wird
`dot(normalize(V), normalize(N))` direkt auf `[0, 1]` begrenzt. Der historische
Vertexshader erzeugt dieselbe Richtung `V = normalize(-View * position)`.

Patch `2388-nomad-gradient-legacy-parity.patch` stellt die HLSL-Abbildung auf
das belegte Verhalten zurück. Damit wird der Nomad-Gradient im Backface-Bereich
am ersten Sample begrenzt, statt über den halben Gradient zu laufen.

Die Überprüfung belegt Quellcode-Parität und erfolgreiche Shader-Kompilierung,
nicht die visuelle Übereinstimmung mit dem Retail-Spiel. Ein fester
Nomad-Schiff-A/B-Lauf mit verifiziertem Retail-NAP und einer Hardware-GPU bleibt
offen.
