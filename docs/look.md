# Look and rendering

What every scene shares on screen, and the rendering rules that decide where flat UI can go.

## Post-processing and palette

Every scene uses `Settings/PostProcessing Profile` — colour filter, Neutral tonemapping, purple
vignette, bloom above 1.05. The filter was blue `#E6EFFF` until Carstenz's 8 Oct 2026 commit turned it
pink `#FFEFF6`; it regrades every scene, so text colours measured before then (the menu banner's)
are stale.

| Scenes | Sun |
|---|---|
| Level1, ChooseStage | `(50, 330)`, `#FFF4D6`, 1.0 |
| MainMenu | `(40, 305)`, warmer, 1.45 |

The game's palette is indigo, lavender, dusty pink and deep navy with orange-yellow accents, and
almost no green (measured off MainMenu and Level1, 6 Oct 2026). **The stage-select map is the
exception:** it was recoloured into the palette on 6 Oct 2026, then Carstenz's 8 Oct terrain brought
back a natural look — steel-blue sea, green land, khaki beaches — and Fitra kept it on 9 Oct
([stage-select map](scenes/stage-select-map.md)). Keep other new art inside the palette.

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
draws over all geometry, nothing occludes it, and the outline pass never touches it. A
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
