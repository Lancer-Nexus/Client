# Surface-GPU-Upload und BC5-Sampler: 13.10.2026

## API und Besitz

`SurfaceGpuTexture.TryUpload(window, cpuLease, maxGpuBytes)` verlangt den
Renderthread. Der Aufrufer behält seine CPU-Lease. Die GPU-Ressource wird vom
Rückgabewert bis zu dessen explizitem Dispose besessen und muss vor dem nativen
Context-Shutdown freigegeben werden. Dispose ist idempotent; neue Zugriffe nach
Dispose werden abgewiesen. Dieser Helfer registriert keine Texturen in einer
globalen Residencyverwaltung und liefert keine Fence-/Async-Upload-Garantie.

EstimateGpuPayloadBytes prüft dieselben DDS-Längen-/Format-/Cubegrenzen wie der
Surface-Leser. BC-Blöcke und alle deklarierten Mips/Faces werden gezählt; RGB24
wird auf die BGRA32-Uploadgröße erweitert. Die Mipflags müssen mit dem bestehenden
DDS-Loader übereinstimmen. Das ist logische Payloadgröße, keine Messung von
Treiberallokationen oder globalem VRAM. Zu kleines Budget wird vor Allokation
abgewiesen. Der vorhandene DDS-Kompressionspfad ist durch S3TC-Backendfähigkeit
gegated; ohne sie wird der Zusatzupload abgelehnt, anstatt eine ungeplante
Dekompressionsallokation vorzunehmen.

Der generische DDS-Loader normalisiert nun Standard-RGB/RGBA-Kanalmasken in
BGRA, ohne Alpha zu verändern. Für 24-Bit-Daten wird Alpha weiterhin 255.
Fehler nach erfolgreicher Texture2D-/TextureCube-Erstellung geben die bereits
erzeugte Textur frei. Native Fehler in Backend-Konstruktoren/Pins, Geräteverlust
und vollständige Fehler-Injektion bleiben zusätzliche Prüfaufgaben.

## Tests und echter Sampler

115 relevante Tests bestanden, davon elf Upload-/DDS-Prüfungen für BC-Blöcke,
Cubegrößen, RGB24-Expansion, Kanalmasken/Alpha, Mipflags, defekte Formate sowie
Worker-/Budgetabweisung ohne GPU-Allokation und Erhalt der CPU-Lease. Der
Solution-Build hat null Fehler und 20 Warnungen.

Der SDL3/OpenGL-NAP-Test lädt erzeugte FVF3/FVF4-Vertices und reale BC5-Texturen
über den Upload-Helfer. Eine bytegleiche Kopie des Basic_FVF-Vertexshaders samt
NormalMap-Helper erzeugt die TBN-Matrix. Der Testfragmentshader benutzt
NtTexture.SampleLevel(NtSampler, selectedUV, 0), rekonstruiert BC5 mit Stärke und
liefert die Weltnormale als BGRA8-Farbe zurück. Shader, Uniformpuffer, Targets,
Vertices, Indizes und Scope-Uploads werden explizit freigegeben.

Acht konstante 4x4-ATI2-Blöcke prüfen X/Y, außerhalb der Unit Disk liegende XY
und Stärke null jeweils mit UV0/FVF3 bzw. gespiegeltem UV1/FVF4. Die neunte
Prüfung nimmt eine echte Normalmap aus dem aktiven NAP über den CPU-Cache. Sie
bleibt Rgtc2/BC5 und benötigt 1.398.096 logische GPU-Payloadbytes.
Eine unabhängige CPU-Referenz liest die beiden BC4-Paletten und 3-Bit-Indizes der
vier Texel um UV=(0.25,0.25), interpoliert bilinear und rekonstruiert die Normale:

- CPU: (0.18795526, -0.15266109, 0.9702409)
- GPU: (0.18431377, -0.15294117, 0.96862745)
- Vektordistanz unter 0,025 einschließlich BGRA8-Quantisierung.

Budget=size-1 wird abgewiesen; Budget=size erlaubt denselben Upload. Der erste
Echttexturtest scheiterte, weil der separate Probehost MaterialAnim=(0,0,0,0)
stehen ließ. Der produktive BasicMaterial-Pfad setzt diesen Uniform bereits.
Nach MaterialAnim=(0,0,1,1) wurde der komplette Lauf grün wiederholt. Dies war
kein grüner Erstlauf und erforderte keine Produktionsshaderänderung.

Die sieben früheren Basis- und acht FVF-Readbacks bleiben erfolgreich. Native
Options/Escape/Exit und Shader-Shutdown liefern weiter owned=0/total=0. Es gab
im finalen Lauf keine GLShader-NativeBuffer-Leakwarnung.
[Maschinenprotokoll](evidence/surface-gpu-upload-20261013.json).

## Begrenzte Materialanbindung

Die Option `surface_normal_maps` ist standardmäßig aus und wird in den
Grafikeinstellungen gespeichert. Ist sie aktiv, bindet `BasicMaterial` eine
zugeordnete Zusatz-Normalmap nur für FVF3/FVF4-Meshes mit autorisierten
Tangenten. Die CPU-Textur wird asynchron außerhalb des Renderthreads geladen.
Der Draw pinnt anschließend nur eine residente CPU-Lease für den GPU-Upload;
nachfolgende Draws erwerben nur die residente GPU-Lease. Diese wird nach jedem Draw
freigegeben und der vorherige Texturslot wiederhergestellt. Fehlende oder
ungültige Karten lassen das Originalmaterial weiterlaufen. Das Abschalten der
Option räumt ungenutzte GPU-Einträge über die Fence-Queue ab.

Meshes mit FVF1/FVF2 erhalten noch keine automatisch erzeugten Tangenten; DFM,
Roughness-/Metallic-Zusatzkarten und eine visuelle Retail-NAP-Abnahme fehlen.
Der Schalter ist deshalb ausdrücklich für tangententragende FVF3/FVF4-Meshes
beschrieben.

## Gemeinsamer GPU-Cache

`SurfaceGpuTextureCache` teilt Texturen innerhalb eines GameResourceManager nach
normalisiertem Bibliothekspfad und UTF-Knotennamen. Sein Standardbudget beträgt
256 MiB logische Payloadbytes. Die Prüfung erfolgt vor dem Upload; aktive
Texturen und bereits aus dem Lookup entfernte, aber noch auf ein Fence wartende
Texturen zählen weiter. Erst nach erfolgreichem Dispose im Fence-Callback sinkt
der Zähler. Bei knapper Residency wird eine ungenutzte LRU-Textur zum Retire
vorgemerkt; bis zum Fence-Signal liefert ein konkurrierender Upload `null` und
kann später erneut versuchen. Der Aufrufer hält pro Materialnutzung eine
`SurfaceGpuTextureLease` und gibt sie nach dem letzten Draw frei. Der bestehende
`ClearTextures()`-Systemwechselpfad retiert ungenutzte Cacheeinträge ebenfalls;
aktive Leases bleiben unangetastet. Der Manager leitet beim Shutdown aktive
Cacheeinträge in die Frame-Retirement-Queue.

Fünf Unit-Tests prüfen Sharing, Renderthreadgrenze, ClearUnused und
Budgetbuchung vor/nach einem kontrolliert verzögerten Retirement-Callback. Ein SDL3/OpenGL-Lauf auf
Intel HD 4600 lädt eine echte BC5-Textur über den Cache, erwirbt sie zweimal
unter verschiedenen Schreibweisen, rendert sie und bestätigt 16 Bytes Residency
bis zum Fence-Signal. Manager-Shutdown mit zwei aktiven Einträgen drainiert beide
Callbacks vor dem SDL-Kontextabbau. Siehe
[Cache-Protokoll](evidence/surface-gpu-cache-20261013.json).

## Weiter erforderlich

BasicMaterial erwirbt und hält GPU-Leases für den opt-in Normalmap-Pfad mit
authored FVF3/FVF4-Tangenten oder asynchron generierten VMesh-Tangenten aus
NORMAL+TEX1/TEX2. CPU-DDS und Tangentengeometrie werden außerhalb des
Renderthreads vorgewärmt. Native Draw-Smokes und Retail-Szenenmatrix,
DFM/Skinning, reflektierte World-Skalierung und Geräte-/Plattformgrenzen
bleiben offen. Für generierte Tangenten gilt ein separates 64-MiB-Limit auf
logische Vertex- und Index-Payload; es gibt keine LRU-Eviction. Cubemap- und weitere Effektschalter bleiben bis zur Integration gesperrt.
