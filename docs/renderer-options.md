# Rendereroptionen in `EXE/Freelancer.ini`

Engine- und Materialoptionen können für eine Installation zusätzlich zur
Benutzerkonfiguration in einem eigenen Abschnitt der Freelancer-Konfiguration
stehen. Die Werte aus `[LancerNexusRender]` dienen als globale Vorgabe. Ein
explizit gesetzter gleichnamiger Eintrag in `[Settings]` der
`librelancer.ini` hat Vorrang. Ebenso haben passende Kommandozeilenparameter
Vorrang. Dadurch bleiben lokale Benutzerwahl, Einstellungen aus dem Optionsmenü
und gezielte Startprofile wirksam. Nicht angegebene Schlüssel behalten ihren
bisherigen Standardwert.

```ini
[LancerNexusRender]
; Oberflächen und Beleuchtung
surface_profile = standard
surface_normal_maps = true
surface_pbr_maps = true
surface_generated_tangents = true
pbr_environment_maps = true
pbr_diffuse_ibl = true
render_legacy_environment_maps = true
per_pixel_lighting = true
dynamic_lighting = true
dynamic_light_budget = 32
transient_effect_light_budget = 128

; Szene und Licht
render_nebulae = true
render_starsphere = true
render_sun_lens_glow = true
render_sun_ambient = true
render_sun_ambient_strength = 0.25
render_transient_effect_lights = true
render_weapon_muzzle_lights = true

; Schatten, Ambient Occlusion und HDR
render_shadows = true
render_shadow_source = auto
render_shadow_bias = 0.0015
render_ssao = true
render_ssao_radius = 3.0
render_ssao_strength = 1.1
render_ssao_bias = 0.025
render_hdr = true
render_hdr_exposure = 0.0
render_bloom = true
render_bloom_threshold = 1.0
render_bloom_strength = 0.18
render_bloom_radius = 1.0
render_bloom_resolution_scale = 1.0
render_bloom_kernel_pairs = 2

; Geometrie und Effekte
render_depth_prepass = true
render_soft_particles = true
render_soft_particle_distance = 2.0
render_projectile_beams = true
render_particle_effects = true
render_jump_gate_effects = true

; Filter und Detailstufen
antialias = 0
anisotropy = 0
lod_multiplier = 1.3
```

Der Abschnitt ist optional und ändert die originale Freelancer-Konfiguration
nicht automatisch. Renderoptionen, die nur zur Laufzeit oder über die
Kommandozeile verfügbar sind (zum Beispiel Renderprofil-Aufzeichnung und
Start-up-Testparameter), sind nicht Teil dieses INI-Abschnitts.
