# NAP explosion-profile inventory (2026-10-16)

This static inventory uses the staged package inputs under
`Data/NAP/.runtime-test-stage/package-inputs-20261016/`, which were built from
`Data/NAP/` for the NAP-only runtime profile. It does not edit or repack game
assets.

## Ship destruction chain

The inventory followed `Models/ships/Shiparch.ini` `explosion_arch` references
through `Effects/fx/Explosions.ini`, the resulting `effect` nickname through
`Effects/fx/Effects*.ini`, then `vis_effect` through
`Effects/fx/explosions/ExplosionsAle.ini` to the referenced ALE file.

- 69 ship archetypes define `explosion_arch`.
- All 69 explosion archetypes resolve. 17 have no direct `effect` entry, as
  authored in the INI.
- The 52 remaining chains resolve to an effect profile. Five profiles are
  audio-only and have no `vis_effect`.
- The visual chains use 18 distinct ALE effects; all declarations and ALE files
  resolve. No missing link was found in the scanned chain.
- 46 ship references carry the `elite_flash_to_red` effect light. Its named
  `LightAnim` profile exists in `Effects/fx/Lightanim.ini`.

Large-ship destruction needs a separate runtime check: for example,
`explosion_rh_battleship` resolves to the audio-only `explosion_sfx_csx_flash01`
profile, while `FuseBrBattleship.ini` separately starts visible
`gf_explosion_br_battleship_*` effects on hardpoints. The server sends fuse
effects through `SFuseRunnerComponent`/`ServerWorld.EffectSpawned`; the Client
resolves them in `CNetEffectsComponent` and attaches `ParticleEffectRenderer`s.
An audio-only final explosion profile therefore does not, on its own, prove a
missing visual effect. The fuse event delivery and rendering still need a
live ship-destruction capture.

## Fighter destruction runtime check (2026-10-16)

A QA-only M01A mission was staged under
`Data/NAP/.runtime-test-stage/explosion-ship-qa-20261016-inputs/`, then packed
into `mission.nap`. The added mission timer spawned one LI fighter and destroyed
it three seconds later with `Act_Destroy = EXPLODE`. The active QA manifest used
the resulting archive (SHA-256
`8308bbfbfc7267f038b25933b950ab6d3c729e58831dc893b59a908dbe519962`); the
other twelve archives were unchanged and verified against their manifest. All
13 archives passed `nexus-pack verify`. Inputs came only from `Data/NAP/` and
the test installation had no loose `DATA` directory.

The Release client (`LibreLancer.dll` SHA-256
`7cc4fd66b0f9ea7ccddaaf7de5f26f56498fd57ac7b9ea6621af572ddfc63e7a`) reached
the existing `fp7_base_explode` trigger, then the QA spawn trigger at 06:16:30
and destroy trigger at 06:16:33. A captured frame shows the fighter amid a
visible multi-particle explosion. The screenshot is
[`fighter-destruction.png`](fighter-destruction.png).

This confirms that one ordinary LI fighter destruction produces a visible ALE
explosion in this Release/NAP-only setup. It does not establish original-game
parity, explosion timing/scale, transient-light contribution, or behavior for
equipment, mines, and large-ship fuse chains. The QA mission is temporary and
is not part of the maintained NAP sources.

## Result and remaining runtime coverage

The NAP assets contain the visual profiles and ALE files for the scanned ship
explosion chains. This rules out a missing-file explanation for those chains;
it does not prove that every runtime event is delivered or drawn.

Next capture a battleship destruction (networked fuse effects), then a mine and
equipment detachment. Compare the independent particle and transient-effect-
light switches in the same scene. The M01A base and fighter captures cover
different trigger chains and are not substitutes for these cases.
