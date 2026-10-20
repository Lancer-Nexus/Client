# Height-/Bump-Asset-Inventar

Stand: 2026-10-15. Dieses Inventar trennt bestätigte Materialkanäle von einer
heuristischen Suche nach möglichen Height-/Bump-Assets. Es ist kein Beleg, dass
die Pixeldaten aller Texturen frei von Höheninformation sind.

## MAT-Laufzeitpfad

Der UTF-Materialparser in
[`Material.cs`](../../../src/LibreLancer/Utf/Mat/Material.cs) kennt die
Texture-Slots `dt_name`, `et_name`, `bt_name`, `dm_name`, `dm0_name` und
`dm1_name`, zusätzlich `nm_name`, `rt_name` und `mt_name`. `dm_name` und
`dm0_name`/`dm1_name` gehören zu den vorhandenen Detail-Map-Materialtypen und
haben TileRate-Parameter. Im Parser gibt es keinen dedizierten Height-, Bump-,
Parallax- oder Displacement-Map-Eintrag. Das sagt etwas über den bekannten
MAT-Schema-Pfad, aber nicht über beliebige Erweiterungen in Mods oder über
Höheninformation, die in gewöhnlichen Texturen codiert sein könnte.

## Retail-Dateibestand

Read-only-Suche unter `/home/masterbee/Dokumente/FL/DATA` über **2.172** Dateien
mit den Endungen MAT, TXM, UTF, 3DB und CMP. In den Bytes jeder Datei wurden die
Begriffe `heightmap`, `bumpmap`, `parallax`, `displacement` und `normalmap` in
ASCII, UTF-16LE und UTF-16BE gesucht:

- `heightmap`, `bumpmap`, `parallax` und `normalmap`: keine Treffer.
- `displacement`: ein Treffer im Namen `EQUIPMENT/MODELS/ST/ku_displacement_shield.3db`;
  der Name bezeichnet ein Schildmodell, keinen bestätigten Material-Map-Slot.

Die Suche findet keine bestätigten Height-/Bump-Eingaben. Sie ist nur eine
Namens-/String-Heuristik: Hashes, nicht-textuelle Knotenbezeichner und
Höheninformation in gewöhnlichen Bilddaten sind damit nicht ausgeschlossen.

## Nächste belastbare Prüfung

Textur- und MAT-Inhalte repräsentativer Planeten-, Schiffs- und Schildassets
semantisch klassifizieren; Detail-Maps dabei von Height-/Bump-Daten trennen.
Erst bei bestätigten Eingaben einen Parallax-Pfad planen und dessen Kosten
messen. Bis dahin bleibt Parallax optional und wird nicht aus dem `dm_name`-
Slot abgeleitet.
