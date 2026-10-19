# Sun sphere asset and renderer path (2026-10-18)

## Asset evidence

`Data/NAP/Models/solar/suns/sun.sph` and the Retail `DATA/SOLAR/SUNS/sun.sph`
are byte-identical (SHA-256
`1ff6bfd88508e0127d38082d8ea65eab614a68c2c24b11c36e755537cdd216c0`). The
client UTF parser reports `radius=1000`, one side material, and the name
`none`. `SphFile.CreateRigidModel` only creates cube-sphere draw calls when at
least six side materials exist, so this asset contributes no visible sphere
geometry through that path. This is the file's shipped state in both datasets,
not a recent NAP packaging omission.

## Visible sun and Lens Glow

`StarArch.ini` defines the star sprites and `lens_glow` entries separately.
`GameItemDb.InitStars` resolves those entries into `Sun` render data,
`GameObject` creates `SunRenderer`, and `SunRenderer.Draw` submits the glow and
optional lens-glow billboard independently of `sun.sph`. A prior NAP-only A/B
with the same M01A trigger showed a visible change around the sun when
`--render-sun-lens-glow` was switched; see
[`Sun Lens Glow A/B`](sun-lens-glow-ab-20261015/README.md).

The `sun.sph` warning therefore does not establish that the visible sun or Lens
Glow is missing. It only reports that the placeholder has no six-face sphere
texture set. The earlier FP7 startup log did not itself capture a sun image, so
that run cannot establish the appearance of the SunRenderer billboards.
