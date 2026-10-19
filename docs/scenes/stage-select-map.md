# Stage-select map

**Scene:** `Assets/Scenes/Menu/ChooseStage.unity` · **Scripts:** `ChooseStage/{WaterSeabed, IslandShallows,
CloudShadowDrift}.cs` · **Shader:** `Shader/StylizedWater.shader`

The map's art and environment: terrain, water, palette, clouds and the scene's roots. How the map
plays is in [stage select](stage-select.md).

| Piece | What it is |
|---|---|
| `Terrain` | Unity Terrain, `Terrain/ChooseStage TerrainData.asset` — Carstenz's terrain from 8 Oct 2026 (400 m square with a 600 m height range, heightmap 513, at `(−203.5, −14.3, −286.2)`): a mountainous coastline around the bay. Its flat base at y −14.3 is the sea floor. The airport and lighthouse islands have **no** shelves in it — they carry their own (`IslandShallows`, below). It replaced the 30 Sep terrain (400 × 40 × 400, heightmap 1025, sea floor −5.8, terraced in 2.4 m steps), which was deleted on 9 Oct 2026 when Fitra kept Carstenz's map. Sculpt it by hand with Unity's terrain tools — the water follows live — and commit the terrain first, so a bad stroke can be undone from git |
| `Water` | `Material/World/Water.mat` on `Shader/StylizedWater.shader`, a 600 m plane (scale 60, widened from 400 m on 8–9 Oct). `WaterSeabed` feeds it the terrain's live heightmap and every island's shelf each frame, so shallows, foam and shore waves follow any sculpting or moved island with no bake |
| `Clouds` | the cloud prefabs as **shadow-only** casters 45–55 m up, moved and wrapped by `CloudShadowDrift`; the sun is at `(50, 330)`, so shadows fall up and to the left on screen |
| `Global Volume` | the same `Settings/PostProcessing Profile` as MainMenu and the levels |

**The map uses game-palette swatches that still read as land** (18 Oct 2026, Fitra's ask). It was
recoloured to indigo / lavender on 6 Oct, went natural with Carstenz's 8 Oct terrain, whose pale
greens and khakis washed out to mint-grey under the grade, and on 18 Oct took palette cells
r3c2, r0c2 and r1c4. The islands' own materials match the terrain; the water was left alone:

| Piece | Colour | Lives in |
|---|---|---|
| deep sea / shallows / foam | steel blue `#4D75AB` / cyan `#9ADEF0` (alpha 0.7) / pink-white `#FFF0F6` | `Material/World/Water.mat` |
| beaches, land, cliffs (terrain) | warm sand, teal-green, lavender-grey — tints below; the Rock layer also uses `CaveMountain1_Merged0_LOD0_Bake_normal.jpg` as its normal map | each Terrain Layer's **Color Tint** over the neutral `Texture/Terrain/Terrain_{Sand,Grass,Rock}.png` |
| island grass / island cliffs | teal-green `#259562` / warm sand `#F2C077` | `Model/Environment/StageSelect/Materials/Island {Grass,Rock}.mat` |
| tree leaves | teal `#3FAE96` / `#23807A` | `…/Materials/Tree Leaves {Light,Dark}.mat` |

**Terrain colours are tints, so they're editable by hand** (6 Oct 2026, Fitra's choice). The three
terrain textures are neutral near-white greys (mean 250) that only carry the faint noise; each
layer's colour is its **Color Tint**: select `Environment/Terrain` → *Paint Terrain* → *Paint
Texture* → pick a layer → *Color Tint* under Diffuse (it's `TerrainLayer.diffuseRemapMax`, stored on
the `Assets/Terrain/ChooseStage {Sand,Grass,Rock}.terrainlayer` assets). **The swatch renders
lighter than it looks**: URP multiplies the linear texture by the tint's raw value, and the picker
shows that value as if it were sRGB — so tune by eye in the Scene view, or type the tint from the
table (tint = the colour's linear value ÷ 0.956, the neutral texture's linear level):

| Layer | In use (18 Oct): tint → renders | 8 Oct natural | 6 Oct indigo / lavender | Old green |
|---|---|---|---|---|
| Sand | `#ED8D31` → `#F2C077` | `#9F9B7B` → ~`#CBC9B5` | `#DE808D` → `#EBB8C0` | `#F4B762` → `#F5D8A3` |
| Grass | `#055021` → `#259562` | `#62E25D` → ~`#A3ED9F` | `#5640B7` → `#9A86D8` | `#398919` → `#7FBE57` |
| Rock | `#2C2353` → `#716597` | `#4C4C4C` → ~`#919191` | `#261A80` → `#6A58B8` | `#CF6A40` → `#E4A986` |

`Terrain_{Sand,Grass,Rock}_Green.png` are the old coloured originals, restored from git for the
comparison; with tints in charge they're no longer needed (use one only with its layer's tint set to
white, or the two colours multiply).

Buildings, runways (`AIRPORT.003` palette), the lighthouses' red and white, the rings and the routes
kept their colours. The island and tree materials were embedded in `island.fbx`,
`lighthouse 1.fbx` and `LP_TREE_PACK.fbx`; each FBX's import now **remaps** them to the external
materials above (`lushgrass` → Island Grass and `island` → Island Rock in both island FBXs,
`Material.006` / `.014` → the leaves). The island prefabs had stored direct references to the old
embedded leaf materials, which went empty on reimport — they were refilled from the FBX's own slot
order. If a future FBX remap leaves pink/missing material, look for the same thing.

Planes cruise at the `Plane Spawn` height, **17.5 m**, clear of the control towers (13.3 m) — but
not of the 8 Oct terrain: its mountains peak at **27.9 m inside the planes' flying area**
(`mapCenter` ± `mapHalfExtents`, worst at x −73, z −69); 1.7 % of that area rises above the cruise
and 4.1 % above the plane's belly (13.9 m), so planes fly through the peaks there. The old terrain
topped out at 8.3 m. Open: lower those peaks, pull the flying area in, or raise the cruise (which
brings back the parallax offset). The cloud-sea fog of war that covered every locked region (1 Oct 2026)
was removed on 5 Oct 2026 at Fitra's request, with its script, shaders, materials and meshes —
stage progress now lives on the landing rings. Don't rebuild it.

The water is written to avoid the outline pass's ghosting: that pass draws edges from the depth
and normals textures, so anything under a surface that doesn't write them (the old water) had its
silhouette traced across the sea. `StylizedWater` sits at `Geometry+450` — inside the opaque range
the depth-normals prepass renders — with its own `DepthOnly` / `DepthNormals` passes writing one
flat up-normal, while its colour pass still alpha-blends so the shallows show the sand below. Since
the depth texture then holds the water itself, depth-based colour comes from the terrain heightmap
instead.

**Island shallows move with the islands** (5 Oct 2026, Fitra's ask). Each island prefab
(`Island_airport`, `Island_LightHouse`, `Island_Rock`) has an `IslandShallows`: a shelf centre
(`centerOffset`, the footprint's middle — 2.96 m off the airport's pivot), a flat `platformRadius`
just under the surface (airport 8.2 m, islets 4.8 m) and a `slopeWidth` down to the sea floor
(6 / 6.5 m). `WaterSeabed` hands every enabled one to the water each frame (in the editor too) and
`StylizedWater` takes the higher of the terrain and the shelves, so an island dragged anywhere
brings its foam rim, turquoise shallows and shore waves with it, and shelves that overlap merge
into one. The platform sits `_ShelfDepth` (0.26 m) under the surface — inside the foam depth, so
it reads as a white rim — and the edge wobbles so no shelf is a clean circle. The water takes 32
shelves at most (26 now) and warns once if there are more; raise `MaxIslands` in `WaterSeabed` and
`MAX_ISLAND_SHALLOWS` in the shader together. The old terrain-sculpted platforms were flattened to
the sea floor the same day; the mesas' and coast's own shores were left alone.

The scene's roots, as cleaned up on 5 Oct 2026:

| Root | Holds |
|---|---|
| `Main Camera`, `Directional Light`, `Global Volume` | the shot, the sun (`(50, 330)`), post-processing |
| `Map System` | `MapController` |
| `Plane Spawn` | where the planes start |
| `HUD` | the screen-space overlay: the [star total](stage-select.md#star-total) and the [boarding pass](stage-select.md#the-boarding-pass) |
| `Level Islands` | `Level 1 Island` … `Level 4 Island`: Lighthouse Bay, Solo, Rock and Solo templates on the four northern airport islands, about 34 m apart, left to right across the top of the bay |
| `Decorations` | everything without a stage: `Island Group 1`–`6` (each airport island with no node, plus its own islets, pivoted on the airport island so a group moves as one) and three loose lighthouse islets |
| `Environment` | `Terrain`, `Water`, `Clouds` (the shadow casters) |

(A move of the nodes to the spread-out southern islands was planned on 1 Oct 2026 but never
applied.) `mapCenter` / `mapHalfExtents` cover every island, so planes can fly anywhere on the map.
The islands and props come from `Prefab/StageSelect/` with their FBX files in
`Model/Environment/StageSelect/`. The clean-up deleted scene objects nobody could see — the
inactive `Map Board` sprite of the old flat map, Carstenz's `plane1`–`plane4` model options parked
under the sea (two `plane1` copies alone were over 600k triangles) and the `Corals` — but not their
prefabs or models.

`plane1.fbx` and `could2.fbx` came out of Blender with the scene's camera and lights, and
`plane1` used to bring its `Camera` into the map: a full-screen Base camera at depth 0 that drew
after the Main Camera (−1) and covered it with a fixed view of the sea. Both models now import with
**Cameras and Lights unticked** (1 Oct 2026). If the map ever shows a still view that ignores the
planes, list `Camera.allCameras` in Play mode. Carstenz's hand-made
`Shader/Water.shadergraph` is no longer used. `Prefab/StageSelect/vfx_Tornado_01` isn't placed
anywhere, and its particle material was never committed, so it renders magenta.

> Nodes 3 and 4 still point at the archived `LevelConfig_Stage3/4` and stay locked. See
> [levels](levels.md#config--scene-mapping).
