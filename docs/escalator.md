# Moving escalator

**Prefab:** `Prefab/Decoration/Escalator.prefab` · **Shader:** `Shader/EscalatorSteps.shadergraph` ·
**Material:** `Material/World/EscalatorSteps.mat` · **Meshes:** `Prefab/Decoration/Escalator_Frame.asset`,
`Prefab/Decoration/Escalator_Steps.asset`

The twin escalator's steps run in a loop. Each step climbs (or descends) the incline, flattens out at
the landing, slides under the landing plate and comes back at the other end. All of it happens in the
vertex shader: no script, no per-frame CPU cost, and shadows follow the steps. The prefab owns it, so
every copy animates — 5 in MainMenu, 1 in Level1 (one twin over `level1.fbx`'s two static lanes,
since 19 Oct 2026), 8 in Level2, 2 in the archived Level1.

## How the prefab is built

- **Root** — `MeshFilter` = `Escalator_Frame` (balustrades and landing plates), `MeshRenderer` =
  `AIRPORT Menu` (white panels and indigo-blue rims like Level1's static escalator — see
  [look](look.md#post-processing-and-palette)). The `MeshCollider` still uses the full original `Escalator` mesh from
  `AirportBuildingTrial03.fbx`, so collision didn't change.
- **`Steps` child** — `MeshFilter` = `Escalator_Steps`, `MeshRenderer` = `EscalatorSteps.mat`, identity
  transform. **Keep it non-static.** Static batching bakes the mesh into world space, and the shader's
  path maths is in object space — the steps would fly off.

## The meshes

Both were generated once (October 2026) from Trial03's `Escalator` mesh by a throwaway editor script;
the script isn't in the repo. In the source mesh each lane's steps are already a separate island:
14 inclined step blocks plus a flat top, rise `0.4187`, run `0.9302`, climbing toward object **−z**.
Steps are the triangles that sample the dark `#2D0A22` and orange `#FD9404` palette swatches.

- `Escalator_Frame` is the source mesh minus every step triangle.
- `Escalator_Steps` is one complete step (tread, orange nosing, riser, underside — 22 triangles per
  lane, taken from the 9th step) copied 17 times per lane along the path. Each vertex carries its
  step's data in **UV3**: `x` = rest z, `y` = direction, `z` = rest y.
- The lane at object x < 0 rides up, the lane at x > 0 rides down, like a real twin escalator.
- The source mesh has a dark triangle on the same plane under every orange nosing strip, so its
  nosings z-fight — some show, some don't. The generated steps drop those duplicates; every nosing
  shows.

## The shader

Every vertex moves with its step:

```text
zNow = LoopStartZ + frac((restZ − direction·Speed·time − LoopStartZ) / LoopLength) · LoopLength
yNow = clamp(LineY − Slope·zNow, BottomY, TopY)          // flat → incline → flat
position += (0, yNow − restY, zNow − restZ)
```

A step wraps from one end of the loop to the other at `LoopStartZ` / `LoopStartZ + LoopLength`.
Both points sit under a landing plate, so the jump is never visible. The fragment side samples
`carry on color palate.png` with UV0 at smoothness 0.5, the same as `AIRPORT`.

| Property | Value | Meaning |
|---|---|---|
| `Speed` | 0.6 | object units per second along the run — about one step every 1.5 s. **The only value to tune.** |
| `Slope` | 0.4501 | rise / run of the staircase |
| `LineY` | 0.1937 | height of the incline line at z = 0 |
| `BottomY` / `TopY` | −3.5299 / 2.3762 | the flat levels at the bottom and top landings |
| `LoopStartZ` / `LoopLength` | −7.0766 / 15.8140 | the wrap window, 17 steps × run |

Everything except `Speed` is measured from the mesh: change those only together with a regenerated
`Escalator_Steps`. All of it is object space, so the scene copies' different scales (2 × 2 × 1,
2.5 × 1.63 × 1.25, and Level1's 3.33 × 2.61 × 1.82 under its 0.6 root) need nothing extra.

## Gotchas

- **Judge the motion in Play mode.** In edit mode the Scene view only animates with *Always Refresh*
  on, and the editor's shader time can be huge (≈ 6 × 10⁵ s when this was built), which quantises
  float time to 1/16 s — the steps stutter. In Play mode time starts at 0 and the motion is smooth.
- **The bottom end.** The incline starts under the bottom landing plate, so a step rises up through
  the plate's front edge instead of sliding out flat. The original model already had its first step
  sitting above the plate, so the silhouette matches.
