# Produktionsshader: DXC-Bundle-Audit

Stand: 2026-10-18

Alle 25 Shader-Beschreibungen aus `src/LibreLancer/Shaders/*.txt` wurden mit
dem in `bin/builddeps/bin/dxc` enthaltenen DXC kompiliert. `LLShaderCompiler`
hat dabei die in jedem Bundle deklarierten Feature-Permutationen erzeugt und
alle Ergebnisse in temporäre `.bin`-Bundles gepackt. Der Lauf endete mit
Exitcode 0 und `Done`; 25 Bundles mit zusammen 3.421.914 Byte wurden erzeugt.

Die Matrix umfasste unter anderem Basic-/Skinned-Basic, PBR/PBR-Skinned,
Materialshader, Nebel, Partikel, Sonne, SMAA-nahe Sprite-/Navmap-Ausgaben,
Depth-only, Alpha-Test, Normal-/Metal-/Roughness-Maps und Shadowmap-Varianten.
Der einzelne Nomad-Bundlebuild nach der Änderung aus Patch 2388 ist in diesem
Vollmatrixlauf enthalten.

Reproduktion aus dem Client-Repository:

```bash
shader_args=()
while IFS= read -r shader; do
  name=${shader##*/}
  name=${name%.txt}
  shader_args+=("$shader" "/tmp/lancer-shader-audit-$name.bin")
done < <(find src/LibreLancer/Shaders -maxdepth 1 -type f -name '*.txt' -print | sort)
LD_LIBRARY_PATH="$PWD/bin/builddeps/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}" \
DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 \
/opt/dotnet-10/dotnet run --project src/LLShaderCompiler/LLShaderCompiler.csproj \
  --no-restore -- --dxc "$PWD/bin/builddeps/bin/dxc" "${shader_args[@]}" --verbose
```

Das ist ein Compiler-/Bundle-Nachweis. Native GL/Vulkan-Pipelineerstellung,
Interface-Kompatibilität zur GPU, visuelle Ausgabe und Performance auf
Hardwaretreibern bleiben gesonderte Abnahmepunkte.
