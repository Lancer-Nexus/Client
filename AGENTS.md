# AGENTS.md – Lancer Nexus Client

## Mission

Maintain the Lancer Nexus client fork while preserving compatibility with upstream LibreLancer wherever possible.

## MVP architecture baseline

- The client authenticates only through Gateway and never owns placement, leases, credits, inventory or other authoritative character state.
- It treats Gateway assignments and short-lived, single-use transfer tickets as untrusted input and follows the transfer lifecycle `Requested -> Reserved -> Prepared -> SourceFrozen -> TargetAccepted -> Committed -> SourceReleased` without returning to the main menu.
- The active game instance remains authoritative until the MySQL-backed lease changes atomically at `Committed`; the client must handle rejection, expiry and recovery states.
- Shared, versioned MessagePack contracts and capabilities belong in `Protocol`; Redis is never exposed to the client.

## Rules

- Keep the LibreLancer fork baseline untouched while authoring Nexus work. Store every Nexus client or engine modification only in a focused patch under `patches/`, register it in `patches/series`, and let the build patch applier apply it immediately before compilation.
- Do not make direct edits to fork source files for Nexus features or fixes. To inspect or test an applied overlay, derive it from the patch series and keep the patch files as the only maintained source of those changes.
- Do not rewrite or reformat unrelated upstream code.
- Keep cluster functionality disabled by default for standalone builds.
- Put shared wire contracts in `Protocol`; do not duplicate MessagePack models here.
- Never store passwords, refresh tokens or private keys in logs or client configuration.
- For local Debug E2E runs, start the client with `SSL_CERT_FILE=/path/to/test-ca.pem output/dev/client/run.sh /path/to/client.ini --credentials-file=/path/to/private-test-account.txt [--character=PilotName]`. The first positional argument is the client configuration; `--credentials-file` starts Gateway login automatically. The credentials file contains `Email: ...` and `Password: ...` lines and must have mode 0600. `--character` automatically selects an existing character after login; it does not create one. For self-signed local Gateway certificates, `SSL_CERT_FILE` must point to the test CA. Keep credentials out of process arguments, logs, commands, commits and captured output. These options are Debug-only and must not be used for Release builds.
- Treat Gateway responses and transfer tickets as untrusted input and validate them.
- Do not let the client decide authoritative placement, ownership, credits or inventory.
- Preserve the existing game simulation unless a change is explicitly part of the MMO integration.
- Transferred population groups must keep their world active without players until they dock, die or transfer again. Player-distance culling must not discard these leased NPCs; do not generate extra ambient population merely to keep an unobserved world active. Retain source worlds while population handoffs are pending.
- Publish the private NPC listener separately in runtime status; use the Coordinator's NpcTransferEndpoint and its explicit port for initial send and recovery. Only legacy peers without that field use the configured port fallback.
- Resolve source jumpers by stable NPC ID rather than nickname. New population nicknames must not collide across world restarts or imported groups. Keep npc-state diagnostics local and Debug-only, and capture them on the simulation queue.
- Add tests for login state, reconnect, instance display and transfer failure paths.

## Existing LibreLancer build and test model

- This repository is the existing LibreLancer fork, not an empty .NET service repository. Preserve its upstream layout, submodules and native dependencies.
- The supported entry point is `./build.sh`, which validates `build.config`, checks native dependencies and submodules, then runs `scripts/BuildLL/BuildLL.csproj`.
- `BuildLL` emits the Windows and Linux engine/SDK release archives as single-root `tar.zst` packages; the packager uses the .NET Zstandard stream and preserves Linux executable modes.
- The main solution is `LibreLancer.sln`; it contains the client, server, editor, launcher, tests and native-integrated projects. Do not invent a second solution or packaging path for MMO work.
- The primary automated test project is `src/LibreLancer.Tests/LibreLancer.Tests.csproj`. Run the relevant test subset after client changes; use the full solution build when the change crosses shared engine or project boundaries.
- Native components are configured through the existing `CMakeLists.txt` and dependencies checked by `scripts/depcheck_unix`; do not replace this with a service-style `dotnet restore`-only workflow.
- Maintain native decoder and text-renderer changes as `lancerdecode` and `blurgtext` targets in `patches/series`, with `base-lancerdecode-commit` and `base-blurgtext-commit` pins. Apply and verify these overlays before building; do not commit modified native submodule sources or publish vendor pointers for local fixes.
- The current project files target `net10.0`. Keep cluster functionality optional and isolated behind existing configuration/capability boundaries while upstream synchronization remains possible.
- On this Linux development host, the .NET 10 runtime and SDK are installed at `/opt/dotnet-10`. Set both `DOTNET_ROOT=/opt/dotnet-10` and `DOTNET_ROOT_X64=/opt/dotnet-10` when starting generated apphosts such as `InterfaceEdit` or `LLShaderCompiler`; the `dotnet` SDK command may work without them while those apphosts fail to locate the runtime.
- `src/LibreLancer/LibreLancer.csproj` references the shared `Protocol` submodule for the optional Gateway version handshake; initialize that submodule before building the client.

## Working-model escalation

- If a task requires complex reasoning beyond the current model's reliable scope, ask the user whether switching to a stronger model is desired before continuing.
- Do not switch models silently or broaden the task because a stronger model may be useful.

## Change process

Cluster `/admin` chat must be intercepted before logging/broadcast, including styled fragments. Keep replies private. Gateway admission supplies account/session or transfer identity server-side; current Gateway leases and Administration SQL roles/scopes authorize access, never local admin flags. Shared query contracts belong in Protocol; maintain engine changes only in patches.

Permission changes use typed Gateway requests and are authorized centrally by Administration. Do not use LLServer's legacy admin/deadmin flags as clustered permissions. The local UUID operator fallback is console-managed and only active when Gateway/cluster login is disabled; it must never bypass Nexus authorization. All related LibreLancer source changes remain patch-only.

Every player-facing server command that is currently guarded by `Character.Admin` must receive its own `command.<name>` permission and be checked fail-closed through Gateway with the account, session, character lease, instance and system context before execution. Keep the command-to-permission catalog in [Administration's permission guide](../Administration/docs/permission-system.md#permissions-fuer-bestehende-librelancer-adminbefehle). Do not silently retain the legacy flag as a clustered fallback; standalone OP fallback must remain explicitly isolated.

See [the Administration permission system guide](../Administration/docs/permission-system.md) for the command syntax, local operator boundaries, and the fact that LLServer revision-sync startup is still pending.

1. Identify whether the change belongs in the client or in Gateway/Protocol/Cluster.
2. Keep the smallest possible patch against the upstream fork.
3. Build the client and run the relevant UI and protocol tests.
4. Document any upstream conflict or compatibility assumption.
5. Keep every client change in the patch series under `patches/series`; the patch applier runs before the supported build entrypoints and skips patches already applied. Patch files are the maintained implementation; applied source changes are build workspace state only.

## Nexus baseline system groups

The base Nexus topology uses eight game instances, one per group: BR01-BR06 (`br-01`), BW01-BW10 (`bw-01`), EW01-EW05 (`ew-01`), IW01-IW06 (`iw-01`), KU01-KU06 (`ku-01`), LI01-LI05 (`li-01`), RH01-RH05 (`rh-01`), and `mixed-01` for all remaining registered systems. System nicknames are compared case insensitively and emitted lowercase. Folder names are not always world nicknames: `fp7` contains `fp7_system`; `intro` and `miners` are asset directories, not registered worlds.
LLServer SystemIds defines owned worlds; SystemId is the primary fallback. Travel within the owned set remains a local system jump; travel to another group uses the fenced Gateway transfer. Runtime status must publish the entire set. The new BindAddress setting binds the game listener privately; cluster servers do not open the legacy discovery listener. Keep these changes patch-only.

## NPC retirement integration

- Queue retirement through the durable background outbox; do not perform file or
  HTTP I/O on the simulation thread. Await durable intent and inspect every
  Coordinator entry result before treating retirement as confirmed.
- Keep pending and confirmed original ownership fences as restore barriers. A
  corrupt outbox must prevent recovery readiness; never discard it on timeout.
- Do not wire terminal hooks through generic object removal: transfer freezing
  also removes objects and must never retire their identities.
- Death of an identified ambient NPC must keep the NPC and its group reserved
  until the atomic survivor-checkpoint plus retirement write is accepted. Block
  population transfers and player-distance culling for that NPC while pending.
- MissionRuntime terminal events remain excluded until Gateway character-lease
  arbitration is implemented. Death and terminal docking now wait for a committed
  group checkpoint before removal. Restart-time checkpoint restoration remains
  required lifecycle work.

## Overlay integrity

- Before recording a manually applied overlay, reconstruct and compare the full
  series with `python3 scripts/verify-lancer-nexus-overlay.py`. The record command
  enforces this check and requires Python 3. Only CRLF/LF differences are ignored.
- A GUI compilation failure involving removed admin types can indicate an
  incomplete server operations bundle. Inspect the local overlay against a fresh reconstruction before
  adding replacement contracts or another patch. The build integration bundle also restores failure
  propagation in the build entry point.
- The integrity checker is read-only; preserve unrelated modifications and repair
  identified overlay drift by applying the relevant maintained patch.

- GameServer writes periodic checkpoints for active stable-ID ambient NPC groups
  from the simulation queue, then persists and delivers them through the bounded
  background outbox. Keep serialization capture on the simulation queue and all
  staging/network I/O on outbox workers. Do not include MissionRuntime until
  Gateway lease arbitration is wired. Terminal death/dock must use a survivor
  checkpoint plus retirement as one write; do not hook generic removals.

## Original Freelancer reverse engineering references

- The local retail reference installation is `/home/masterbee/Dokumente/FL`:
  `DATA/` contains game assets/configuration and `EXE/` contains the original
  executable and engine DLLs. Treat these files as read-only and do not copy them
  into this repository.
- Start with `Freelancer.exe`, `thorn.dll`, `deformable2.dll`, `shading.dll`,
  `rendcomp.dll`, `flmaterials.dll`, `alchemy.dll`, `common.dll`, and `engbase.dll`
  when investigating mission/cutscene, character, material, or rendering behavior.
- Ghidra is installed at `/home/masterbee/Tools/ghidra_12.1.4_PUBLIC`; `radare2`
  is also available as `/snap/bin/radare2`. Both can inspect the original PE32
  binaries. Treat decompiler output as evidence to compare with asset data and
  runtime behavior, not as a specification by itself. Original PDB files have
  not been confirmed available.
- Keep the Ghidra project, source hashes, notes, logs, and generated exports
  under `/home/masterbee/Dokumente/FL/reverse-engineering/librelancer/`. Exports
  should record binary name/hash and function name or address. Do not put binary
  copies or large Ghidra project databases into Git.
- Project bootstrap and export conventions are documented in
  `/home/masterbee/Dokumente/FL/reverse-engineering/librelancer/README.md`.

## Restart-time ambient NPC recovery

- Before publishing a world or starting population simulation, drain pending checkpoint writes and restore that system's current ambient NPC checkpoints from Coordinator. Resolve stable NPC references and preserve the serialized AI/autopilot state. Keep recovery fail-closed when the checkpoint store is unavailable or corrupt; only a Coordinator 404 after listing means ownership changed and may be skipped.

## Consolidated functional bundles

The active series is twenty-one Client bundles and two native bundles, described in
`patches/README.md`. Preserve the pinned baselines and the recorded overlay
hashes when regenerating a bundle. Validate a fresh complete reconstruction
before recording the applied state or publishing; historical patch numbers
in older audit documents refer to Git history.

## Shader lifetime

- Shader and ShaderBundle are render-thread resources. Dispose short-lived
  bundles explicitly; do not use disposed shaders or create them from loader
  workers. RenderContext owns a registry for all remaining shaders.
- Preserve `DisposeShaders()` before SDL/native context shutdown and its
  finally path after the application cleanup hook. Do not delete GL programs or
  free uniform pointers from GC finalizers or background threads.
- Include shader/bundle rollback, wrong-thread access and normal native shutdown
  when changing this ownership model. Headless tests require the built native
  text library on the loader path. Context-wide texture/fence/crash-path cleanup
  remains separate work; do not claim it from shader-only tests.

## Runtime VMesh tangents

- Keep the existing SimpleMesh submodule initialized and include its managed
  assembly in the engine runtime. Do not fork its MikkTSpace implementation.
- Tangent geometry is per material draw and real UV channel. Include both the
  VMeshRef base vertex and TMeshHeader start vertex when resolving indices.
  Retain per-corner tangents at mirrored UV seams; do not overwrite shared
  source vertex tangents or modify original Freelancer/NAP assets.
- Keep authored TEX3/TEX4 tangents unchanged and preserve both real UV sets,
  normals, positions and diffuse bytes. VMeshTangentBuilder output is per draw and must
  enter the async tangent cache; allocate GPU resources on the render thread and
  dispose them with their VMeshResource before allocator shutdown.

## Surface GPU upload ownership

- SurfaceGpuTexture owns one explicit upload. Keep its CPU lease separate and
  dispose the GPU resource on the owning render thread before context shutdown.
- Apply both per-upload and aggregate residency budgets before allocation. Keep
  bytes charged while a texture retires behind a GPU fence; subtract them only
  after successful post-fence disposal. GPU leases are render-thread-only.
  Surface CPU leases remain separately owned by the caller.
- Keep UV animation/selection uniforms initialized in sampler tests. Compare
  real BC5 texture sampling with a CPU reference, not only uniform-fed normals.
  BasicMaterial consumes the cache for authored FVF3/FVF4 or asynchronously generated VMesh tangents from NORMAL+TEX1/TEX2.

## Surface normal-map activation

- `surface_normal_maps` and `surface_pbr_maps` stay independent and opt-in. BasicMaterial supports supplemental normal maps on authored FVF3/FVF4 tangents and generated FVF3/FVF4 geometry from VMesh NORMAL+TEX1/TEX2. Preserve MAT-owned `NmName`, `RtName`, and `MtName`. Supplemental roughness and metallic maps use PBR slots 4 and 3 with the configured UV channel; scalar values multiply their maps or provide map-free PBR values. DFM materials may use PBR through `PBR_Skinned` when skinned; tangent support controls normal mapping independently. Release each temporary GPU lease after its draw and restore the touched texture slots.
- Keep `SurfaceNormalMapsEnabled` synchronized from GameSettings at startup and at every live settings apply. Turning it off retires unused GPU cache entries through the fence queue.
- Keep `SurfacePbrMapsEnabled` synchronized from GameSettings at startup and every live settings apply. The `surface_pbr_maps` config entry and `--render-surface-pbr-maps=on|off` override control supplemental roughness and metallic maps only. Turning it off retires unused GPU cache entries through the fence queue.

## GPU texture cache

- Use `SurfaceGpuTextureCache` for shared supplemental uploads and dispose each
  `SurfaceGpuTextureLease` after its last draw use. Load CPU images outside the
  render thread and pass the lease to the render-thread upload. Active and
  fence-retiring entries both count against the per-resource-manager 256 MiB
  logical payload budget. A budget miss while fences are pending can be retried.
- Cache shutdown queues active resources for retirement before native context
  shutdown; outstanding leases must not be used after manager shutdown.

## GPU retirement queue

- Queue GPU releases through RetireAfterFrame after the last intended resource
  use. Keep scopes alive until completion; Dispose of an already retired
  SurfaceGpuTexture must not bypass its scheduled GPU release.
- Preserve EndFrame submission and shutdown drain before shader/context teardown.
  Submission or wait failures must not cause early release. Callback failures
  are surfaced after attempting the remaining callbacks, not silently swallowed.
- Fence lifecycle is not a global GPU residency cache. Device-loss recovery,
  unsupported-sync platform runtime and budgets require their own evidence.

## Runtime DFM tangents

- Generate DFM tangents asynchronously per FaceGroup and selected `NmFlags` UV
  channel after materials resolve. Preserve both real UV sets and bone data;
  expand strips with alternating winding and retain mirrored per-corner seams.
- Keep original DfmVertex buffers as fallback. DfmTangentVertex draws are
  non-indexed to avoid 16-bit corner-index overflow. Charge uploads against the
  shared 64 MiB generated-tangent payload budget; dispose on feature disable,
  ClearMeshes and resource-manager shutdown on the render thread.
- `surface_generated_tangents` gates all generated DFM geometry. Authored MAT
  normal maps remain independent of the opt-in Surface normal-map setting.
  PBR DFM draws use `PBR_Skinned` to preserve bone skinning; generated tangent
  geometry enables normal mapping where available. Keep the original Basic
  skinned fallback and carry bone buffer and offset with deferred transparent
  draws. Visual material acceptance in an in-game DFM scene remains required.

## Renderer startup overrides

- Keep each renderer override independent so a launch can selectively enable
  or disable supported features without changing `client.ini`. Values supplied
  on the command line override only their matching setting; omitted values keep
  the configuration value.
- Supported controls and their corresponding `client.ini` settings are:
  - `--render-normal-maps=on|off` → `surface_normal_maps`.
  - `--render-surface-pbr-maps=on|off` → `surface_pbr_maps`.
  - `--render-pbr-environment-maps=on|off` → `pbr_environment_maps`.
  - `--render-pbr-diffuse-ibl=on|off` → `pbr_diffuse_ibl` (diffuse irradiance contribution; enabled by default when the PBR environment path is active).
  - `--render-generated-tangents=on|off` → `surface_generated_tangents`.
  - `--render-surface-profile=original|minimal|standard|full` → `surface_profile`.
  - `--render-per-pixel-lighting=on|off` → `per_pixel_lighting`.
  - `--render-dynamic-lighting=on|off` → `dynamic_lighting`.
  - `--render-dynamic-light-budget=0|8|16|24|32` → `dynamic_light_budget` (maximum transient local lights admitted per frame; persistent system lights are unaffected).
  - `--render-transient-effect-light-budget=0|32|64|128` → `transient_effect_light_budget` (maximum active `lgt_effect` light events; particle-effect simulation continues at 0).
  - `--render-nebulae=on|off` → `render_nebulae`.
  - `--render-starsphere=on|off` → `render_starsphere`.
  - `--render-sun-lens-glow=on|off` → `render_sun_lens_glow`.
  - `--render-sun-ambient=on|off` → `render_sun_ambient` (opt-in diffuse tint from locally weighted persistent system-light colors; off by default).
  - `--render-sun-ambient-strength=0..1` → `render_sun_ambient_strength` (finite invariant-culture tint blend; default 0.25; validated in config and adjustable in Effects options).
  - `--render-hdr-exposure=-4..4` → `render_hdr_exposure` (finite EV exposure compensation; default 0)
  - `--render-ssao=on|off` → `render_ssao`; `--render-ssao-radius=0.05..50` → `render_ssao_radius` (view-space radius, default 3); `--render-ssao-strength=0..4` → `render_ssao_strength` (default 1.1); `--render-ssao-bias=0.0001..0.25` → `render_ssao_bias` (occlusion bias, default 0.025). SSAO remains off by default.
- `--render-hdr=on|off` → `render_hdr` (opt-in float scene target and ACES tone mapping; falls back to LDR when the GPU target is unavailable).
  - `--render-bloom=on|off` → `render_bloom` (opt-in bloom pass; requires `render_hdr`).
  - `--render-depth-prepass=on|off` → `render_depth_prepass` (opt-in opaque model depth pass; Basic/PBR materials use depth-only fragment variants; off by default pending runtime performance measurements).
  - `--render-shadows=on|off` → `render_shadows` (opt-in single directional shadow map with opaque model casters and 3×3 PCF receivers in Basic/PBR; off by default).
  - `--render-shadow-source=auto|<light-nickname>` → `render_shadow_source` (defaults to `auto`, which selects the brightest active directional system light by Rec. 709 luminance; an explicit nickname selects only that active directional light and fails closed when it is unavailable).
  - `--render-bloom-threshold=0..8` → `render_bloom_threshold` (finite invariant-culture float; default 1).
  - `--render-bloom-strength=0..2` → `render_bloom_strength` (finite invariant-culture float; default 0.18).
  - `--render-bloom-radius=0.25..4` → `render_bloom_radius` (finite invariant-culture float; default 1).
  - `--render-bloom-resolution-scale=0.125..1` → `render_bloom_resolution_scale` (finite invariant-culture float; default 1; controls blur-target dimensions and is adjustable in Effects options).
  - `--render-bloom-kernel-pairs=1..4` → `render_bloom_kernel_pairs` (integer number of bilinear Gaussian tap pairs; default 2; adjustable in Effects options).
  - `--render-projectile-beams=on|off` → `render_projectile_beams` (only hides `vis_beam`; simulation and projectile particle effects remain active).
  - `--render-particle-effects=on|off` → `render_particle_effects` (suppresses particle geometry while effect timing and simulation continue).
  - `--render-jump-gate-effects=on|off` → `render_jump_gate_effects` (controls the configured jump-tunnel effect renderer).
  - `--render-transient-effect-lights=on|off` → `render_transient_effect_lights` (controls short-lived `lgt_effect` lights independently of other dynamic lights).
  - `--render-weapon-muzzle-lights=on|off` → `render_weapon_muzzle_lights` (controls weapon `light_anim` muzzle lights independently of transient effect lights).
  - `--render-antialias=none|smaa-low|smaa-medium|smaa-high|smaa-ultra|msaa-2x|msaa-4x|msaa-8x` → `antialias`.
  - `--render-anisotropy=0|2|4|8|16` → `anisotropy`.
- `--invulnerable=on|off` applies only to the local player on the embedded
  single-player server. `on` is rejected in Gateway/cluster mode; it is a test
  aid and does not grant invulnerability to remote multiplayer players.
- Boolean controls accept only `on` or `off`. Surface profile selects the
  material-map quality preset; anti-aliasing and anisotropy remain subject to
  backend and hardware support. Do not document controls without a runtime
  setting and parser entry. The dynamic-light budget accepts only its listed
  integer levels; the transient-effect event budget accepts only its listed
  levels and evicts the least useful active lights when reduced.
- Keep the in-game graphics Effects panel wired to these same `GameSettings`
  fields. When adding a live renderer option, update both panel initialization
  and its save/apply path; do not leave implemented features marked as planned.
- For RenderDoc material-to-draw identification only, set `LL_RENDERDOC_MATERIAL_MARKERS=1`; OpenGL KHR_debug groups then label each `BasicMaterial` draw with shader type, diffuse sampler and material key. This is disabled by default and unavailable when the GL debug-group entry points are missing.
- Example for an isolated comparison launch:
  `lancer client.ini --render-normal-maps=on --render-nebulae=off --render-antialias=smaa-high`.

## NAP test-container provenance

- NAP containers used for Client, LancerEdit, or runtime validation must be
  built solely from files under the project `Data/NAP/` source tree. Generated
  QA assets must first be placed under `Data/NAP/` and then packaged.
- A temporary archive output path is allowed; every packaged input must still
  come from `Data/NAP/`. Do not validate with containers assembled from `/tmp`,
  loose Freelancer installs, or other external sources.
- Verify the archive and its active manifest before launching a test.
