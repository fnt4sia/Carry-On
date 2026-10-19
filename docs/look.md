# Look and rendering

What every scene shares on screen, and the rendering rules that decide where flat UI can go.

## Post-processing and palette

Every scene uses `Settings/PostProcessing Profile`. Since Carstenz's 15 Oct 2026 "Color correction"
commit that is colour adjustments (exposure 0.24, contrast −20, saturation −19, filter), lift/gamma/gain
and colour curves. Tonemapping, vignette and bloom are **off**. The tone curve itself is
an AgX look-up table applied by the `AGX` `FullScreenPassRendererFeature` on `URP-HighFidelity-Renderer`
(`Shader/FullScreen LUT.shadergraph`, `AGX LUT Template.mat`, LUT `AgX - Hue`) after post-processing.
The filter was blue `#E6EFFF` until the 8 Oct commit turned it pink; text colours measured before
these regrades (the menu banner's) are stale.

> **Renderer features run once per camera that uses the renderer.** Until 17 Oct 2026 the
> runtime `World UI Camera` (below) used the main renderer, so AgX graded every scene twice in Play,
> making it about 16% brighter and 15% more saturated than the Scene view. It now has its own
> feature-free renderer, so Play matches the Scene view. Any new overlay camera needs the same
> treatment.

| Scenes | Sun |
|---|---|
| Level1, ChooseStage | `(50, 330)`, `#FFF4D6`, 1.0 |
| MainMenu | `(40, 305)`, warmer, 1.45 |

The game's palette is Level1's: royal blue, lavender-white and near-black, with orange accents,
red seats and teal plants. **No pink** — it fights the grade (Fitra, 18 Oct 2026). That day
MainMenu's pink structure, which was nearly all the palette's pink swatch `#FFAFB1` (cell r0c4 of
`carry on color palate.png`), was recoloured on the prefabs:

| What | Material (`Material/World/`) |
|---|---|
| `PillarKaca` window frames | `Structure Black` — flat `#2D0A22`, the palette's near-black (Level1's frames and escalator steps) |
| `WallBelakang` back walls | `Structure White` — flat `#E7E7E7`, Level1's pillar tint |
| `Escalator` frames, `StandingBanner` (and the scene's `MenuBanner`), `Telephones` | `AIRPORT Menu` — `AIRPORT` sampling `Texture/World/Palette Menu.png`, the old palette with two swatches changed: r0c4 pink → white `#ECDCD7` (escalator panels, board frames, phone hoods) and r1c2 purple → `palate3`'s indigo-blue `#372688` (escalator rims, phone bodies) |

The aim is Level1's balance, which is about half blue with real black and white (its static
escalator: white panels, indigo-blue rims, black steps), not a blue-only scene. These meshes mix
several swatches, so they can't take a flat colour. Don't give them `palate3` itself either: it
changes other swatches too — teal r3c0 turns red on the boards' ad.
The stage-select map, natural since Carstenz's 8 Oct terrain, moved into the palette the same day
([stage-select map](scenes/stage-select-map.md)). Keep new art inside the palette.

## Fonts

| Asset | Used for |
|---|---|
| `TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF` | default UI text: the menu banner, team panel, ticket |
| `Font/LiberationSans SDF - Board Bright.mat` | banner text you act on — face colour 1.35 (HDR), because post-processing greys plain white |
| `Font/Knewave_Regular/Knewave-Regular SDF` | HUD numbers (timer, score) and the pause buttons |
| `Font/Knewave-Regular SDF - Navy Outline.mat` | white numbers with a navy edge — the stage-select star total |
| `Font/Londrina_Shadow/LondrinaShadow-Regular SDF` | the 3-2-1 countdown and Time's Up |

## The WorldUI layer

Player pins, node badges and other world-space overlays sit on layer `WorldUI`
(10). At runtime `WorldUIOverlayCamera` strips that layer from the main camera and stacks an
overlay camera that renders only it, with no post-processing and a cleared depth buffer: WorldUI
draws over all geometry and nothing occludes it. The overlay renders with
`Settings/URP-WorldUI-Renderer`, a copy of the main renderer with **no renderer features**, so the
`Outline` and `AGX` passes never touch WorldUI or run twice. The script picks it by index (1), so
every URP asset (Performant, Balanced, High Fidelity) must keep that renderer at index 1. A
`Camera.Render` capture skips the overlay stack, so WorldUI is missing from those — sign off with
`ScreenCapture` in Play.

## Overlay vs world space, and the outline pass
The lobby's team panel is a **Screen Space - Overlay** canvas, as the old logo canvas was before
it, and that mode is not a style choice. The logo started as World Space and the outlines of scene
objects behind it drew straight over the artwork. The cause is render order, not parenting:

1. `URP-HighFidelity-Renderer` runs a `FullScreenPassRendererFeature` with the
   `OutlinePostProcessing` material at injection point **`AfterRenderingPostProcessing`** — the
   last thing in the frame. That pass is where the game's whole outlined look comes from.
2. A World Space canvas is ordinary transparent geometry (`UI/Default`, render queue 3000) drawn
   during the camera's normal loop, well before that pass.
3. `UI/Default` has **`ZWrite Off`**, so the logo never appears in the depth/normals buffers.

So the outline pass edge-detects only the geometry *behind* the logo and paints those edges over
the finished image. Overlay canvases are composited to the backbuffer after the whole SRP loop,
renderer features included, so they are immune.

If anything flat ever needs to sit *over* the world rather than in it, Overlay is the right mode;
in World Space the outline feature has to be dealt with too, and the artifact is not fixable by
moving, reparenting, or re-sorting the canvas.

**`MenuCanvas` is unaffected**, and that is not luck — it is diegetic. It hangs on the banner in
world space precisely so the terminal's outlines and lighting apply to it; there is no artwork
underneath for stray edges to spoil, only the flat `Screen` quad (or the `Backdrop` fill).
