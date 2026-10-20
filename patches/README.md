# Client patch groups

The active series contains 486 Client patches and two native patches. Each
file belongs to one functional bundle; the combined output preserves the
integration, upstream compatibility fixes, tests and project documentation.

`base-client-commit`, `base-lancerdecode-commit` and `base-blurgtext-commit`
pin the source trees used for reconstruction. Update a pin only while porting
the full corresponding overlay. Protocol contracts remain in the Protocol
repository and are not duplicated in these bundles.

| Bundle | Responsibility |
|---|---|
| 2000 | Gateway sessions, characters, persistence and transfers |
| 2001 | NPC population, identity, checkpoints and handoffs |
| 2002 | Server lifecycle, operations, permissions and FLHookCompat |
| 2003 | Rendering, interface, materials, animation and THN |
| 2004 | Managed builds, shader toolchain, project and CI integration |
| 2005 | Dynamic market trades and trader routing |
| 2006 | Gameplay, SDL3 clipboard MIME ownership and upstream compatibility fixes |
| 2007 | NAP assets, packaging tools and editor workspace |
| 2008 | Audio decoder integration and room playlists |
| 2009 | Client and asset regression tests with synthetic fixtures |
| 2010 | Project findings, TODOs and graphics implementation plan |
| 2013 | Surface normal-map UV selection across metadata, tangent generation and shaders |
| 2014 | LancerEdit solar archetype material preview |
| 2015 | Sphere surface normalmaps |
| 2016 | Independent startup and configuration toggles for existing render features |
| 2017 | DFM normal-map UV0/UV1 comparison screenshots and report |
| 2018 | Rolling CPU frame-work and frame-pacing percentiles in the Debug overlay |
| 2019 | LancerEdit Surface profile selection and material binding inspector |
| 2020 | LancerEdit Surface-map editing, renderer guidance and comparison gallery |
| 2034 | Runtime supplemental roughness and metallic maps with independent controls |
| 2035 | Release PBR map leases and preserve room/backface lighting |
| 2036 | Optional PBR reflections from existing Freelancer environment cubes and preview control |
| 2060 | Independent BC4/BC5 capability detection and CPU RGTC fallback |
| 2061 | Sun lens glow, jump gate and tradelane effect renderer TODOs |
| 2062 | StarArch lens glow rendering and independent startup/config toggle |
| 2063 | Ignore cubemaps in 2D material texture slots to prevent renderer crashes |
| 2064 | Fade sun lens glow as the sun enters or leaves the camera frustum |
| 2065 | Prioritize frame lights by attenuated contribution near the camera |
| 2066 | Enable PBR materials and surface roughness/metallic maps on skinned DFM meshes |
| 2067 | Keep PBR ambient lighting energy-conserving for metallic materials |
| 2068 | Reject out-of-range transient lights before they consume frame slots |
| 2069 | Guard sun lens-glow fades against invalid durations and frame deltas |
| 2070 | Normalize invalid system-renderer frame deltas before effect updates |
| 2071 | Parse star lens-glow colors as normalized float RGB values |
| 2072 | Prioritize transient effect lights and visible beam geometry within fixed pools; add independent beam, particle, transient-light, and weapon-muzzle-light toggles; document retail flash_size evidence |
| 2073 | Correct dynamic-lighting audit to match attenuated-contribution frame-pool selection |
| 2074 | Reconcile Surface Maps TODOs with implemented metadata, PBR, tangent, and editor paths |
| 2075 | Record the native Wayland NAP startup retest and remaining DFM visual gap |
| 2076 | Distinguish Surface Maps sampler support from pending visual filter-quality checks |
| 2077 | Preserve lgt_range_scale through transient effect light creation without applying unknown semantics |
| 2078 | Mark CubeMap catalog strength integration complete in the renderer plan |
| 2079 | Add a configurable per-frame dynamic-light budget |
| 2080 | Configure the active transient effect-light event budget |
| 2081 | Expose implemented renderer effects and light budgets in the in-game options |
| 2082 | Record current retail MAT and asset-name evidence for height/bump maps |
| 2083 | Show accepted frame-light and active effect-light counts in the debug overlay |
| 2084 | Avoid recurring allocations in renderer frame-time percentile diagnostics |
| 2085 | Record the LightEquipment emitter versus billboard visibility audit |
| 2086 | Reconcile shader-variant and GL-state cache roadmap with source |
| 2087 | Record SystemRenderer zero-size and MSAA target resize handling |
| 2088 | Record missing exception-safe render target and stack restoration |
| 2089 | Add source-level render target exception-path evidence and follow-up |
| 2090 | Record the SystemRenderer forward-pass order and missing pass contract |
| 2091 | Record the static AllShaders cache limit across render-context lifetimes |
| 2092 | Define per-context shader bundle recreation acceptance criteria |
| 2093 | Audit renderer option wiring across config, CLI, menu and runtime consumers |
| 2094 | Derive existing scene and antialias target resource contracts |
| 2095 | Restore render targets and state stacks after draw exceptions |
| 2096 | Restore SystemRenderer graphics state after exceptions |
| 2097 | Load typed jump gate and gate tunnel effect data |
| 2098 | Start the configured jump tunnel interior effect |
| 2099 | Parse and render timed jump gate ring effects |
| 2100 | Render profile-driven gate tunnel geometry and preserve arrival transitions |
| 2101 | Audit Tradelane effect dataflow and asset presence |
| 2102 | Restore randomized particle spread for the Tradelane cube emitter |
| 2123 | Bind resolved environment CubeMaps as cube textures in legacy and PBR materials |
| 2103 | Decouple LightEquipment emission from billboard visibility |
| 2011 (`lancerdecode`) | Native Vorbis EOF/reset handling |
| 2012 (`blurgtext`) | System FreeType without OT-SVG hooks |

Shared integration files may contain several related features; their bundle
owns the complete file delta. Bundles apply in `series` order. Ordinary feature
work may use a focused follow-up patch or regenerate the affected bundle after
clean reconstruction; preserve unrelated deltas and synthetic fixtures.

## Consolidation evidence

`consolidation-2026-10-12.json` records the pinned baselines, previous active
series, preserved documentation history and file hashes before bundling.
The old patch files remain available in Git history at the recorded Client
baseline. Historical patch numbers in runtime reports refer to that history.
The consolidation reduced 192 active patches to 13 before the later focused
renderer and editor patches; the active series now carries those additions and
the original local documentation findings omitted by the earlier port.

Run `python3 scripts/verify-lancer-nexus-overlay.py` to reconstruct and compare
the complete overlay. `scripts/tests/test_verify_overlay.py` checks routing,
pinned baselines, drift rejection and the patch-state recorder. The normal
build applier records only a verified complete overlay; avoid manually copying
source files between branches.

The operator-authorized `/clusterjump <system> <source gate>` implementation
is part of the server operations bundle. It still requires
`command.clusterjump`, Gateway/Coordinator authorization and a reciprocal
jump-gate route.
| 2104 | Update transient-light tests for the configurable per-instance frame budget |
| 2105 | Keep dynamic light emitters behind geometry from leaking unshadowed light |
| 2106 | Export recent CPU frame-pacing samples from the Debug overlay as CSV |
| 2107 | Record renderer and resolution per sample and escape CSV fields |
| 2108 | Audit nonblocking GPU timer-query readiness and pass boundaries |
| 2109 | Add optional asynchronous OpenGL GPU pass timing |
| 2110 | Include asynchronous GPU pass samples in frame profile CSV |
| 2111 | Log built-in shader compilation wall time per bundle and in total |
| 2112 | Count asynchronous GPU sample age by rendered system frame |
| 2113 | Advance asynchronous GPU sample age on every rendered system frame |
| 2114 | Track cubemap estimates and expose known GPU memory counters in the Debug overlay |
| 2115 | Validate OpenGL framebuffer completeness and release failed allocations |
| 2211 | Add scaled target resolution to the reusable separable Gaussian blur |
| 2212 | Expose Bloom blur resolution scale through runtime settings, CLI and Effects options |
| 2213 | Add executable pass interface for Bloom and HDR tone mapping |
| 2214 | Add configurable bilinear Gaussian kernel width for Bloom blur |
| 2215 | Add HDR exposure compensation to tone mapping, settings, CLI and Effects options |
| 2216 | Use linear Rec. 709 luminance for the Bloom extraction threshold |
| 2217 | Show estimated Bloom and tone-map target memory separately in the debug overlay |
| 2218 | Export per-frame AA, Bloom and tone-map target memory estimates in frame profiles |
| 2219 | Retry failed Bloom and blur targets after render-size changes |
| 2220 | Allow sun-tinted ambient above one only in active HDR scene targets |
| 2221 | Record exploratory HDR sun-ambient runtime images and limitations |
| 2222 | Preserve material UV animations in batched asteroid fields |
| 2223 | Wrap material animation time in constant time and guard invalid durations |
| 2224 | Run SMAA inside the common HDR/LDR post-process pass chain |
| 2225 | Record the four-mode HDR/LDR and SMAA runtime comparison |
| 2116 | Estimate active SMAA and MSAA render-target memory |
| 2117 | Record the M01A dark station and ship lighting report |
| 2118 | Apply THN ambient and dynamic lights to shared gameplay lighting, then remove them on cleanup |
| 2119 | Validate story character, costume and THN spot data before spawning |
| 2120 | Diagnose queued room cutscenes and guard malformed reserve lists |
| 2121 | Guard story cutscene character spawning against missing body data and per-character failures |
| 2122 | Add opt-in OpenGL material markers for RenderDoc draw identification |
| 2124 | Record M01A runtime lighting evidence and the limits of the visual comparison |
| 2125 | Document M01A sun lens-glow runtime toggle comparison |
| 2126 | Record initial 300-frame M01A CPU/GPU profile and conditions |
| 2127 | Verify M01A cubemap fix in a fresh NAP-only Release run and update renderer evidence |
| 2128 | Add timing breakdowns to slow server update diagnostics |
| 2129 | Audit sun-colored ambient lighting data paths and acceptance requirements |
| 2130 | Add optional per-object ambient tint from persistent system light colors |
| 2131 | Add sun ambient regression tests and runtime evidence |
| 2132 | Add standalone local-player invulnerability launch option and plan character face texture work |
| 2133 | Add standalone invulnerability hull damage regression tests |
| 2134 | Record exploratory sun ambient NAP runtime images and verification limits |
| 2135 | Add a live in-game toggle for sun-colored ambient lighting |
| 2136 | Apply sun ambient toggle immediately while paused for controlled A/B captures |
| 2137 | Record same-session HI02 sun ambient live-toggle evidence and remaining calibration work |
| 2139 | Port six Codeberg updates for texture safety, model import/export and editor tooling |
| 2140 | Port ScriptRunner safety, log export and shared ContentEdit material mappings |
| 2141 | Contain Lua button callback failures and record the active Client status |
| 2142 | Record the corrected NAP-only M01A ship-light test launch |
| 2143 | Record the M01A explosion particle-toggle finding |
| 2144 | Verify the M01A explosion with fresh current Data/NAP packages and default particle rendering |
| 2145 | Record the sun-ambient color-channel contribution and measurement boundary |
| 2146 | Record the isolated NAP save-start state for sun-ambient testing |
| 2147 | Record corrected NAP-only HI02 sun-ambient startup and diagnostic images |
| 2148 | Register authored persistent fuse-effect lights at object hardpoints |
| 2149 | Record the BW04 NAP-only sun-ambient on/off comparison |
| 2150 | Preserve the live sun-ambient checkbox value when leaving options |
| 2151 | Preserve ambient luminance while applying system sun color |
| 2152 | Verify normal map tangent edge cases under world transforms |
| 2153 | Record luminance-preserving sun ambient runtime A/B and calibration limits |
| 2154 | Preserve the applied sun ambient value when leaving graphics options |
| 2155–2164 | Trace BeamEffect state, owner context, elapsed progress and retail CBeam velocity dispatch |
| 2165 | Add typed SystemRenderer passes, time actual opaque draws, audit render-target attachments, and trace Retail CBeam velocity |
| 2166 | Profile SMAA subpasses and MSAA resolve with asynchronous GPU timers in a NAP-only M01A run |
| 2167 | Audit the HDR, tonemapping, linear-color and AA transition requirements |
| 2168 | Add opt-in HDR scene output and tone mapping across AA paths |
| 2169 | Linearize scene shader outputs before HDR blending and tone mapping |
| 2170 | Add optional quarter-resolution HDR bloom with separable blur |
| 2171 | Configure and validate HDR bloom threshold and strength |
| 2172 | Add bloom threshold and strength controls to Effects options |
| 2173 | Linearize HDR inputs to lit legacy material shaders |
| 2174 | Linearize HDR color inputs before legacy effect composition |
| 2175 | Record NAP-only HDR target data and presentation capture discrepancy |
| 2176 | Diagnose HDR presentation output against the exact source target |
| 2177 | Fix fullscreen HDR and Bloom UV attribute bindings |
| 2178 | Record fixed HDR and Bloom NAP runtime evidence |
| 2179 | Document HDR and default-strength Bloom runtime check |
| 2180 | Document Bloom startup controls |
| 2181 | Add sampleable depth-only render targets |

| 2226 | Diagnose transient effect-light frame candidates and rejection priorities |

| 2227 | Keep visible object lights when their emitter is beyond the camera light radius |
| 2228 | Record transient effect-light frame-selection QA captures and limits |
| 2229 | Align post-processing roadmap status with implemented pass and blur code |
| 2230 | Fix nebula light-slot overflow and preserve directional system-light types |
| 2231 | Record Hi02 directional-light, shadow-pass, and stability runtime evidence |
| 2232 | Apply surface PBR and IBL option changes immediately in gameplay |
| 2233 | Record live PBR/IBL toggles and NAP runtime evidence |
| 2234 | Add GGX-prefiltered cubemap mip metadata and PBR sampling |
| 2235 | Add cosine-convolved diffuse irradiance cubemaps to PBR IBL |
| 2236 | Record LI_ELITE cubemap-preview toggle A/B evidence |
| 2237 | Add RenderDoc debug groups for render passes |
| 2238 | Record marker-enabled NAP runtime smoke |
| 2239 | Add optional SSAO depth and post-processing path |
| 2240 | Reconstruct view-space positions for optional SSAO occlusion |
| 2241 | Record Hi02 directional shadow live-toggle evidence |
| 2242 | Quantify same-session shadow-toggle scene response |
| 2243 | Identify selected shadow light in runtime diagnostics |
| 2244 | Select a directional shadow-map source explicitly |
| 2245 | Record shadow-source INI runtime verification |

| 2246 | Record a tighter same-session directional shadow toggle capture and remaining validation limits |
| 2247 | Add and smoke-test three-scale HDR bloom with per-scale GPU timing and target contracts |
| 2248 | Add validated SSAO radius, strength and occlusion-bias settings and startup controls |
| 2249 | Add live SSAO quality sliders to the in-game Performance options |
| 2250 | Fit the SSAO quality controls in the visible Performance options panel and record runtime proof |
| 2251 | Verify live SSAO radius adjustment and persistence from the Performance options |
| 2252 | Validate post-process resource dependencies and pass lifetimes |
| 2253 | Log structured OpenGL driver diagnostics for renderer failures |
| 2254 | Record the targeted M01A ship-light runtime attempt and remaining evidence gap |

| 2260 | Reconcile the PBR/IBL implementation plan with the active shader and bindings |
| 2261 | Split diffuse PBR irradiance from specular environment reflections |
| 2262 | Correct the documented diffuse IBL implementation status |
| 2263 | Record NAP-backed diffuse IBL A/B on Kusari Fighter |
| 2264 | Restore billboard render state if a draw fails |
| 2265 | Validate post-process subpass resource dependencies at runtime |
| 2266 | Execute main renderer passes through a failure-aware pass runner |
| 2267 | Honor required and optional scene attachment contracts at runtime |
| 2268 | Publish only resources backed by available pass attachments |
| 2269 | Validate attachment store operations against pass resource declarations |
| 2270 | Align static pass validation with optional resource reads |
| 2271 | Declare system pass execution order in the pass descriptors |
| 2272 | Schedule and execute main SystemRenderer passes as a reusable dependency graph |
| 2273 | Reuse SystemRenderer pass graph nodes across frames |
| 2274 | Share the compiled render graph scheduler with post-processing |
| 2275 | Centralize replaceable SystemRenderer target ownership |
| 2276 | Replace SystemRenderer targets transactionally on resize and option changes |
| 2277 | Remove local closure helpers from per-frame render graph compilation |
| 2278 | Reuse simple SystemRenderer frame callbacks |
| 2279 | Apply MaterialAnim UV scale to normal-map tangent frames |
| 2280 | Cache SystemRenderer pass schedules when topology is unchanged |
| 2281 | Reuse frame-clear callbacks across SystemRenderer frames |
| 2282 | Reuse SystemRenderer depth-prepass callbacks across frames |
| 2283 | Reuse opaque and transparent SystemRenderer callbacks across frames |
| 2284 | Reuse SystemRenderer star and nebula callbacks across frames |
| 2285 | Reuse the SystemRenderer antialias resolve callback |
| 2286 | Reuse the SystemRenderer shadow depth callback |
| 2287 | Align render-graph tests with validation messages |
| 2288 | Blend sun color into authored ambient without the dark-only product mix |
| 2296 | Decode PBR albedo texture and diffuse material color separately before multiplication |
| 2297 | Reconcile renderer roadmap with current IBL, bloom, shadow and color-space implementation |
| 2298 | Fix unsupported code-fence language in renderer documentation |
| 2299 | Accept reusable render order buffers with spare capacity |
| 2300 | Record NAP-only RenderGraph intro smoke evidence |
| 2301 | Add FP7 intro renderer feature-matrix evidence |
| 2302 | Add independent legacy environment-map toggle |
| 2303 | Add PBR alpha testing to depth and shadow passes |
| 2304 | Share the sRGB-to-linear conversion between PBR and common color-space shaders |
| 2305 | Record NAP-only PBR map, normal-map and cubemap ModelViewer A/B evidence |
| 2306 | Add HDR DX10 RGBA16F cubemap loading and linear shader sampling |
| 2307 | Clarify cubemap preview residency and switch ModelViewer into Lit mode |
| 2308 | Add headless HDR RGBA16F DDS cubemap regression tests |

| 2309 | Track aliased environment cubemaps once in GPU texture memory diagnostics |

| 2310 | Blend sun-tinted system ambient in linear space for HDR output |

| 2311 | Sample full bloom downsample footprints and retain odd image edges |

| 2312 | Linearize system and nebula frame-clear colors for HDR targets |

| 2313 | Honor legacy vertex tint and material opacity in PBR output |

| 2314 | Stabilize directional shadow projection under sub-texel camera motion |

| 2315 | Use scaled mesh bounds for model visibility and shadow culling |

| 2333 | Render SSAO intermediate targets at local origin |
| 2334 | Make directional shadow bias configurable and validated |
| 2335 | Report the failing stage when fitting a directional shadow camera |
| 2336 | Keep directional shadow fitting stable with the game's long far plane |
| 2337 | Distinguish near-plane and middle-depth shadow fit failures |
| 2338 | Record Hi02 shadow-fit fix and directional bias A/B evidence |
| 2339 | Document soft-particle implementation and outstanding scene acceptance |
| 2340 | Apply depth comparison changes and make depth functions backend-neutral |
| 2341 | Move texture wrap and vertex element enums out of OpenGL constant space |
| 2342 | Record backend-neutral enum and depth-state NAP runtime smoke |

| 2343 | Move SDL screenshot readback behind the render backend interface |

| 2344 | Add SDL_GPU vertex upload, BC5 normal-map sampling, pipeline and pixel readback smoke |
| 2345 | Implement mip and region-aware synchronous OpenGL texture readback |
| 2346 | Add runtime smoke for OpenGL texture mip and region readback |
| 2347 | Validate the configured soft-particle depth fade distance |
| 2348 | Offset soft-particle depth UVs for nonzero viewport origins |
| 2349 | Record a 300-frame Hi02 GPU profile on Intel HD 4600 |
| 2350 | Add SDL_GPU depth and HDR scene-copy smoke coverage |
| 2352 | Apply the system sun ambient tint to nebula ambient overrides |
| 2353 | Render sky spheres against the frame clear depth |
| 2354 | Record the NAP-only sky sphere runtime comparison and verified roadmap result |
| 2356 | Group backend-neutral render contracts and the SDL_GPU indexed FVF3 normal-map smoke |
| 2357 | Render dynamic lights attached to projectile travel effects |
| 2358 | Guard legacy material lighting when dynamic lights are disabled |
| 2359 | Resolve NAP Resource DLLs under EXE for librelancer.ini |
| 2360 | Record the NAP-only dynamic-lighting intro comparison and crash rerun |
| 2361 | Fit the Effects options and prevent HDR/Bloom checkbox hitbox overlap |
| 2362 | Define controlled Intel and RTX renderer performance acceptance profiles |
| 2363 | Capture a 300-frame render profile without displaying the Debug overlay |
| 2364 | Include the initial renderer-option snapshot in frame-profile CSV exports |
| 2365 | Release old full-resolution render targets before resize reallocation |
| 2366 | Record NAP-only resize and restore runtime evidence for render-target replacement |
| 2367 | Add NAP-only M01A visual A/B evidence for the soft-particle toggle |
| 2368 | Remove stale OpenGL imports and refresh the backend boundary audit |
| 2369 | Compact SDL_GPU SPIR-V bindings and serialize original-to-SDL slot maps |
| 2370 | Keep selected PBR shader variants within SDL_GPU's uniform-buffer limit |
| 2371 | Execute the embedded production PBR shader in SDL_GPU and read back its pixel |
| 2372 | Validate production PBR shadow sampling with an SDL_GPU depth-map pixel readback |
| 2373 | Isolate PBR shadow-map sampling with a same-shader SDL_GPU depth A/B |
| 2374 | Make shadow receiver depth mapping explicit across OpenGL and Vulkan |

| 2375 | Verify the shared shadow depth mapping in the NAP-only OpenGL system renderer |

| 2376 | Parse and render configured particle effects for dynamic asteroids |

| 2377 | Record NAP-only dynamic asteroid particle-effect parsing and startup evidence |

| 2378 | Distinguish the empty sun Sph placeholder from StarArch sun and lens-glow rendering |

| 2379 | Distinguish retail Fade 1D sampler settings from the Atmosphere MAT scalar |

| 2380 | Share SDL_GPU shader program decoding and validate compact descriptor remaps |

| 2381 | Audit every built-in shader bundle with the SDL_GPU reader |

| 2382 | Create and release every production SDL_GPU shader stage on the active driver |

| 2383 | Create SDL_GPU graphics pipelines for every production shader variant |

| 2384 | Reconcile HDR cubemap visual acceptance status with the NAP-only A/B evidence |

| 2385 | Preserve FX particle spawning when linked appearance transforms are singular |

| 2390 | Supply production FVF and DFM vertex layouts to SDL_GPU pipeline audits |

| 2391 | Use the production Freelancer FVF declaration in the SDL_GPU PBR draw smoke |

| 2392 | Draw real NAP CMP/VMesh FVF3 geometry through the SDL_GPU production PBR smoke |
| 2393 | Sample real Freelancer MAT diffuse DDS textures in the SDL_GPU PBR VMesh smoke |

| 2394 | Draw complete indexed NAP VMesh material surfaces through the SDL_GPU PBR smoke |
| 2395 | Load and sample real NAP normal, roughness and metallic maps on a VMesh |
| 2396 | Load and draw a skinned DFM with real surface maps through the SDL_GPU smoke using the active NAP provider |
| 2397 | Keep SDL_GPU DFM diagnostic geometry inside Vulkan clip depth and add opt-in framebuffer captures |
| 2398 | Load SDL_GPU VMesh diagnostic inputs through the active NAP provider |
| 2399 | Apply the authored CMP hierarchy transform to the selected SDL_GPU VMesh diagnostic draw |
| 2400 | Select an individual CMP component in the NAP-backed SDL_GPU VMesh smoke |
| 2401 | Normalize source VMesh FVF streams with normals and UVs for SDL_GPU part draws |
| 2402 | Report CMP component VMesh formats when an SDL_GPU part draw cannot be selected |
| 2403 | Allow selected unmapped CMP components in Surface-disabled SDL_GPU runs |
| 2404 | Record NAP SDL_GPU CMP part transform Surface-map A/B evidence |
| 2423 | Record repeated full-quality renderer profiles with NAP-only provenance |
| 2424 | Capture cumulative dropped simulation ticks in renderer profile CSVs |
| 2425 | Document the local invulnerability option in renderer smoke evidence |
| 2426 | Resolve diffuse textures from embedded CMP libraries in SDL_GPU diagnostics |
| 2427 | Cover ambient-only material lighting without a source-light owner |
| 2428 | Pass selected light indices directly to material shader binding |
| 2429 | Grow transparent command and sort buffers past the fixed scene limit |
| 2430 | Capture transparent command and vertex draw-call counts in frame profiles |
| 2431 | Record an all-render-options NAP runtime profile and performance evidence |
| 2432 | Arm a renderer profile for paused F9 capture, route F1 around UI keyboard focus, and record NAP-only evidence |

| 2445 | Include expected and archive versions in NAP manifest mismatch errors |

| 2446 | Remove avoidable LINQ allocations from renderer frame and buffer paths |

| 2447 | Record the offscreen SDL_GPU production pipeline audit |
| 2448 | Route DetailMap materials with metallic/roughness data through the PBR shader while preserving legacy fallback |
| 2449 | Route Masked2 detail maps through PBR while preserving alpha selection, tile rates and the second sampler |
| 2450 | Route IllumDetailMap through PBR while preserving its alpha-weighted illumination layer and feature flags |
| 2451 | Route DetailMap2Dm1Msk2Pass through PBR while preserving its diffuse-alpha detail mask |
| 2452 | Record current SDL_GPU PBR material pipeline coverage and the OpenGL NAP capture limit |
| 2453 | Include NAP manifest identity and resolved paths in package version mismatch errors |
| 2454 | Record NAP stage package verification and distinguish stale app manifests from display startup failures |
| 2455 | Route DetailMap MAT normal maps through the lit PBR shader and cover the routing gate |
| 2456 | Remove unsupported Atmosphere Fade behavior speculation and record binary scan limits |
| 2457 | Cover MAT normal-map PBR routing for all specialized detail-map materials |
| 2458 | Preserve asteroid LOD fades in PBR draws and reset per-command material fade state |
| 2459 | Bind the legacy Nomad BT facing-gradient sampler with compatibility fallbacks |

| 2460 | Reject render-graph callback mutation and reentrant execution |

| 2461 | Split renderer CPU profiles into update and draw plus GPU submit time |

| 2462 | Record Hi02 PBR and HDR runtime captures on Mesa llvmpipe with NAP-only provenance |
| 2463 | Apply linear nebula fog and XYZ view distances to PBR and legacy materials |
| 2464 | Record NAP-only PBR and legacy nebula render captures with explicit visual limits |
| 2465 | Record the NAP version mismatch diagnosis and successful current manifest recheck |
| 2466 | Record paired Hi02 normal-map option captures and visual limits |
| 2467 | Avoid redundant asteroid exclusion-zone and cube-distance work during rendering |
| 2468 | Serialize asteroid culling tasks before reusing shared result buffers |
| 2469 | Record current NAP-only Hi02 runtime and asteroid profile after culling changes |

| 2470 | Record Freelancer main-executable atmosphere registration trace and remaining Fade uncertainty |
| 2471 | Extract SDL_GPU device lifetime ownership for the renderer backend |
| 2472 | Reuse checked SDL_GPU device ownership in the production pipeline audit |
| 2473 | Add sRGB transfer and alpha regression coverage for HDR color handling |
| 2474 | Record Hi02 opaque and transparent GPU pass hotspots with measurement limits |
| 2475 | Add opaque and transparent renderer drawcall group counters to profiles |
| 2476 | Attribute transparent renderer drawcalls to scene, asteroid, nebula, effect, billboard and fallback groups |
| 2477 | Split transparent object drawcalls by model, renderer type and batched beams |
| 2478 | Map SDL3-offscreen NVE7 profiles to Quadro K1100M and correct renderer evidence |
| 2479 | Reuse particle child-effect update scratch collections |
| 2480 | Record NAP-only particle child-effect runtime and baseline limits |
| 2481 | Cull projectile particle effects outside the camera frustum |
| 2482 | Profile projectile effect culling candidates, finite bounds and skipped draws |
| 2483 | Compute conservative bounds for live particles in unbounded effects |
| 2484 | Record live projectile-effect culling bounds and profile evidence |
| 2485 | Skip redundant square-root work while measuring conservative projectile-effect bounds |
| 2486 | Skip CPU work for fully transparent particles before GPU submission |
| 2487 | Preflight active NAP snapshots before runtime launch |
| 2488 | Fix front-facing PBR spotlight attenuation direction |
| 2489 | Add an SDL_GPU pixel probe for production PBR spotlights |
| 2490 | Align PBR uniform layout with compiled SPIR-V |
| 2491 | Record Hi02 live render after PBR uniform alignment |
| 2492 | Verify PBR alpha blending and legacy material opacity routing |
| 2493 | Record M01A retail explosion transient-light runtime evidence |
| 2494 | Audit retail weapon muzzle-light configuration and hpfire coverage |
| 2495 | Add weapon muzzle-light budget diagnostics |
| 2496 | Expose renderer options in freelancer.ini |
| 2497 | Cover renderer freelancer.ini option completeness |
