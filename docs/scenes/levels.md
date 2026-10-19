# Levels

**Scripts:** `Game/Core/LevelConfig.cs`, `LevelContext.cs`, `Assets/Script/Editor/LevelValidator.cs`
**Assets:** `Assets/Config/LevelConfig_*.asset`

`LevelConfig` is the single source of level-wide rules and content. Each gameplay scene has
exactly one `LevelContext` selecting its asset; reusable prefabs read that context rather than
serializing their own copy. The scene still owns geometry, placement, identity, and unique
object-to-object wiring.

## Fields

| Group | Fields |
|---|---|
| Identity | `levelId`, `sceneName`, `displayName`, `description` |
| Progression | `unlockedByDefault`, `nextLevel` |
| Stage select ticket | `flightCode`, `originCode`, `originName`, `destinationCode`, `destinationName`, `previewImage` |
| Round | `gameTime`, `star1Score`, `star2Score`, `star3Score` |
| Content | `luggagePrefabs` (cycled in order) |
| Spawner pacing | `spawnInterval`, `maxActiveLuggage` |
| Scoring | `scoreCorrectDelivery` (coins per bag) |
| Orders (dispatch gates) | `orderTime`, `coinsPerSecondLeft`, `comboMultipliers` |

`levelId` is a save key — renaming one after release orphans every existing save record and needs
an explicit migration. `OnValidate` enforces positive timings and ordered star thresholds.
`CalculateStars` has no test coverage (nothing here does), so check star thresholds by hand.

Each luggage prefab defines its own flight colour; `luggagePrefabs` is the colour cycle the belt
runs (see [luggage](../mechanics/luggage.md#spawn-pacing)). Bags never expire and nothing subtracts
points: Level2's gate bounces a bag it doesn't want, and Level1's dispatch gate fails the whole
flight instead.

## Current balance

| Config | Round | Stars | Spawn interval | Max active |
|---|---:|---|---:|---:|
| Stage 1 | 120s | 90 / 180 / 300 | 2.5s | 20 |
| Stage 2 | 150s | 80 / 130 / 180 | 1.0s | 28 |

`LevelConfig_Tutorial`, `_Stage3`, `_Stage4` and `_Design` still exist but belong to
[archived scenes](#archived-scenes) and are not balanced for anything.

**Level2 is the old Level1 plus wrapping.** Same camera rig, and the looping belt with a sink that
Level1 ran before it switched to a pile (see below); it adds
`WrapperStation` prefabs with `requiresPlayerCrank` on,
and its gate sets `wrappedLinesPerFlight` to 1 so every flight needs wrapped bags of a **named
colour** — "1 red, wrapped", not "1 anything, wrapped". The round is longer (150s) and the star
bar lower, because wrapping roughly halves throughput.

> ⚠️ **Level2's station layout is currently broken and the level is not playable as intended.**
> The scene holds a single `WrapperStation B` at `(-26.3, 0, 54.6)`, yawed 90°, sitting north of
> the belt loop — 42 units from the gate and outside the play corridor. The intended pair
> flanking the corridor at `(-5.5, 0, 22.5)` and `(-5.5, 0, 41.5)` is not what got committed in
> `2f4f9b7`. Since the gate demands a wrapped bag on every flight, one badly placed wrapper makes
> the round a long walk. Re-place the stations before balancing anything else in this level.

> **Only Level1 and Level2 exist in the redesign.** Tutorial, Level3, Level4 and DesignScene
> were built for the old rules and moved to `Assets/Scenes/Archive/` in September 2026 — see
> [Archived scenes](#archived-scenes).

**Level1's belt runs red / green / yellow, topped up scarcest-first.** `luggagePrefabs` lists the
three colour variants, and the gate's `palette` lists the same three. Those two must match: a
palette colour the pool never spawns makes a flight unfillable. Blue exists as an asset but is
used by nothing.

**The old Level1 was a pile, not a loop** (September 2026, following a layout sketch from Fitra).
That layout is now `Scenes/Archive/Level1.unity`, out of the build. West to east:

| Piece | Where | Notes |
|---|---|---|
| `Environment/Functional/Baggage Feed` | through the west wall, belt end x −35.7 | the old loop prefab instance, unpacked and cut down to its five feed straights, spawner and door. The door is now the `ConveyorDoorOut` prefab, whose curtain strips carry `BaggageDoorCurtain`; the old copies froze open |
| `Environment/Functional/LuggageDropPen` | at the belt end | the pile — see [luggage](../mechanics/luggage.md#the-pile) |
| `Obstacles/Fountain (Centre)` | (−15.3, 32.8), 7 m across | splits the walk between pile and gate into a north and a south lane |
| `Obstacles/Palm (North)` | (−12, 43.5) | |
| `Obstacles/Bench (South)` | (−19.2, 21) | a bench, not a second palm: from the 42° camera a palm's canopy hides players behind it |
| `Environment/Floor Zones/Gate Runway` | x −11.5…6, z 27.6…35.6 | red carpet from the fountain to the gate |

There is no belt `LuggageSink` any more; `Gameplay Stations/Luggage Sink` is only the void catcher
under the map, which is what the validator's sink check finds.

### The new Level1 environment

`Scenes/Stages/Level1.unity` rebuilds Level1 inside `Model/Level/level1.fbx`. Since 18 Oct 2026
it is the build's `Level1`, so stage select's Level 1 island (`LevelConfig_Stage1`) opens it.

| Piece | What |
|---|---|
| `Level Colliders` | the FBX ships **no colliders**: a floor box (top y −4.05) and `Wall`-layer boxes around the room. The three `Door Gap` boxes block **only the Player layer**. See [conveyors](../mechanics/conveyors.md#authoring-rules). Also three `Tree Pot` capsules (see props below) |
| Props | since 19 Oct 2026 every floor-level prop inside the room is solid: a non-convex `MeshCollider` (added component) on 41 `level1` children — the centre island `Cube.008`, chairs, box piles, trolleys, planter rows, pot plants, pillars, kiosks, rope barriers, fire extinguisher. The three indoor trees get a capsule round the pot only (`Level Colliders/Tree Pot - *`), so the canopy doesn't catch thrown bags. Left walk-through on purpose: the belt railings (`ConveyorPemisah`, they'd snag bags at the belt end), overhead signs and screens, and everything outside the walls. All on `Default`, so they block walking and bags but not grabbing (`grabBlockLayer` is `Wall`) |
| `PlayerSpawner` | four points in a 2×2 grid at x −57.5 / −53.9, z −5 / −0.5, south of the island. They used to sit on the planter row and behind it, which the new colliders walled off |
| `level1/Escalator (Moving)` | one twin `Prefab/Decoration/Escalator` at world scale (2, 1.56, 1.09) over the footprint of the FBX's two static lanes (`Escalator`, `Escalator.002`, now deactivated), so the steps move ([escalator](../escalator.md)) |
| `level1/Ambient Traffic (Cars)` / `(Planes)` | two `AmbientTraffic` prefab instances. Cars: one lane at x −95.5 between the windows and the cone line, every 3–7 s, at world scale 0.6 (the parked bus's size). Planes: `Pesawat1` / `Pesawat2` rolling along the runway at x −142, every 10–18 s, at scale 0.22 so their wings clear the parked bus. The fixed shot only sees ground level through the windows, so only a plane's lower half ever shows. Each instance empties the other group's fleet. The GameManager prefab's own `AmbientAirplaneSpawner` is switched off here: it flew at x +115, behind the camera |
| `Functional/Baggage Loop` | out of the east door, north, two left turns, south into the west door, and `Sink In` behind it. Built on the old environment's U-loop footprint (legs 11.5 m apart, so the top connector overlaps each turn by 0.38 m) |
| `Functional/Baggage Feed` | flat, out of the big door, ending where the art's slope began. Its six straights are 1.5× wider than stock (19 Oct 2026; deck 2.96 m) so bags ride off-centre, and the art's railings either side were moved out 0.70 m to match. The spawner drops bags up to 0.5 m either side of centre (`lateralSpread`). Bags pile on the floor with no pen. A `Luggage Drop Scatter` at the end throws each bag once as it leaves the belt, out onto the heap, so the belt rarely backs up ([luggage](../mechanics/luggage.md#the-pile)) |
| `Baggage Feed/Spawner` | the only spawner; the loop's second one (which doubled the cap to 24) is gone |
| `Gameplay Stations` | `Gate Cylinder` (a `DispatchGate`: booth, rolling door, trapdoor and the dispatch button on the entrance's right) and `Gate Manifest Signage` (its `Order Screen`), plus the void sink — see [delivery](../mechanics/delivery-and-scoring.md#dispatch-gates-level1) |

The environment's own belts, doors, curtain strips, booth and sign are removed from the `level1`
instance as *removed-GameObject* overrides; revert them to get the art back.

**The whole level is built at 0.6 scale** (18 Oct 2026, Fitra: the hall was too big for the
players and bags). Players and bags keep their size; everything else was shrunk around
`(−54.75, −4.05, −33.5)`, the belts' centre at floor height, so the floor stayed at y −4.05:

- `level1`, `Functional`, `Gameplay Stations` and `Level Colliders` carry scale **0.6** on their
  roots. **Put new level pieces under one of those roots**, or they come in 1.67× too big.
- World-unit fields don't follow the transform, so they were scaled by hand: the two turns'
  `centerRadius` (2.89 → 1.734, a scene override), `ArenaCamera.driftAmplitude` and
  `ArenaFollowCamera`'s distances (13.2 / 20.4, margin 4.2).
- The three belt doors are at 0.8 of their old size, not 0.6. A bag rides upright and is 3.2 m
  tall; at 0.6 the headers would cut through it. Now there's 3.44 m under the loop doors' headers
  and 4.15 m under the feed door's.
- Belt speed and the round's star thresholds are unchanged. Walks are 0.6× as long, so the star bar
  wants a playtest.

**Level1 runs the fixed `ArenaCamera`; Level2 runs the follow camera.** Both Main Cameras carry
`ArenaFollowCamera` and `ArenaCamera` as scene overrides in place of `MultiplayerCamera`; flip the
two checkboxes to swap shots. Level1 switched to the fixed shot on 18 Oct 2026, with its pose at
`(−38.6, 19.89, 7.05)` rotated `(42.2, 270, 0)` (the old 45° shot reframed for the 0.6 hall).
Level2's authored pose is `(-14.82, 25.1, 7)`, rotated `(42.12, 0, 0)`.

The rotation is shared ground: `PlayerMovement` derives its movement axes from it and the gate
manifest board is authored to face it, so changing the angle means re-aiming the world UI and
turning the controls too.

Under the **fixed** shot everything playable must sit inside one frustum — belt and pile, all four
spawn points, gate and manifest board — and nothing enforces it. Under the **follow** shot that
constraint relaxes, but a new one appears: the manifest board is world-space at the gate, so it
leaves the frame while players are out at the belt. Each level also carries a `Clone` marker wired
into the camera's `extraTargets` for testing two-player framing solo; it renders, so deactivate it
before a build. See [services](../services/camera.md).

Stage 1 pays per dispatched flight ([delivery](../mechanics/delivery-and-scoring.md#dispatch-gates-level1)):
(10 per bag + 1 per second left) × combo. Its thresholds (90 / 180 / 300) assume about two, three
and five good flights; they are a first guess that wants a playtest. So is the 20 s order timer: a
pile-to-gate round trip is about 45 m each way, so a solo player needs ~10 s per bag.

Stage 1 is unlocked by default and unlocks Stage 2. Stage 2's `nextLevel` is deliberately empty
while Level3 is archived.

## Config ↔ scene mapping

Every live config names a real scene, the scene's `LevelContext` points back at it, and the scene
is in Build Settings — `SceneLoader` checks Build Settings before loading, so the config alone is
never enough.

| Config | `sceneName` | Build index | Luggage pool |
|---|---|---:|---|
| `LevelConfig_Stage1` | `Level1` | 2 | Red, Green, Yellow |
| `LevelConfig_Stage2` | `Level2` | 3 | Red, Green, Yellow |

Build Settings is `MainMenu`, `ChooseStage`, `Stages/Level1`, `Archive/Level2` — nothing else.
Level2's file is `Assets/Scenes/Archive/Level2.unity`; it is live all the same.
`Archive/Level1.unity` (the old pile) is also named `Level1`, and `SceneLoader` loads by name, so
it must stay out of the build.

Stage select still routes Node 1–4 at `LevelConfig_Stage1..4`. Nodes 3 and 4 point at archived
configs and stay locked, because Stage 2 no longer unlocks Stage 3. A save that unlocked them
before the archive still can't start them: a node whose scene isn't in Build Settings never fills
its boarding border, and the group landing on it gets the wrong-action SFX plus an error log.

The old flat stage-select scene was deleted on 30 Sep 2026; Carstenz's 3D map took its name. The
old `Test` decoration-staging scene was deleted in `0f39a36`.

## Adding a stage

A stage is three things that point at each other — a scene, a `LevelConfig`, and an island on the
map — plus one line in Build Settings. Do them in this order:

1. **The scene.** Duplicate `Scenes/Stages/Level1.unity` — it already has the `Level Context`,
   `GameManager.prefab` (with its `GameHUD`), the spawner, the `PlayerSpawner` and its four spawn
   points, the camera rig, gates and belts — and rebuild the layout. Building one from empty
   instead: one root `Level Context`; a fresh `GameManager.prefab`; a fresh `Prefab/Luggage/Spawner.prefab`; a
   `PlayerSpawner` with four ordered spawn points; the camera, delivery gates, void coverage,
   conveyors, and a `WrapperStation` if any gate asks for wrapped lines. A feed belt ends in a
   `LuggageDropPen` with its root at the belt's end and +X along it; a looping belt needs a
   `LuggageSink` across it instead.
2. **The config.** Duplicate a `Config/LevelConfig_Stage*.asset` and fill every group in
   [Fields](#fields): a unique `levelId` (a save key — never rename it once shipped), `sceneName` =
   the scene's file name exactly, the map's ticket copy (`flightCode`, origin and destination
   codes and names, `previewImage`), the round (`gameTime`, star scores), the luggage pool and
   pacing. Leave `unlockedByDefault` off unless it is the first stage. Point the scene's
   `Level Context` at it.
3. **Build Settings.** Add the scene. A stage whose scene isn't in Build Settings never fills its
   ring — the group gets the wrong-action sound and an error instead.
4. **The chain.** Set the *previous* stage's `nextLevel` to the new config. That one field unlocks
   the stage when the previous one is finished and draws the dashed route to it.
5. **The island.** In `ChooseStage`, drag one of the five templates in
   `Prefab/StageSelect/Level Islands/` under `Level Islands` (y 5.2 — landed planes and the
   sea-level landing ring both hang off that) and set its `Level` to the new config. That one
   field is the whole wiring: the badge, ring, route, ticket and star total all read the config.
   The island carries its own art, badge, ring, route and shallows, so moving it moves the whole
   stage. Keep `Plane Spawn` outside every ring, keep the island inside `MapController`'s
   `mapCenter` ± `mapHalfExtents`, and re-tune the previous island's `Route` bends if the new route
   crosses an islet ([stage select](stage-select.md#routes)).
6. **Check it.** Run **Carry On ▸ Validate Open Scene** in the new scene, then play it from
   `MainMenu` → stage select, so the save, unlock, ticket and route are exercised too.

The map already has islands for Stages 3 and 4 pointing at the archived `LevelConfig_Stage3/4`
(`levelId` `stage-3` / `stage-4`). To make a real Stage 3, reuse `LevelConfig_Stage3`: set its
`sceneName` to the new scene, add that scene to Build Settings, and set Stage 2's `nextLevel` to
it — no new island needed.

### While authoring

Never place a per-level `AudioManager`, `PlayerSystem`, `SceneLoader`, or `ProgressionService` —
persistent services bootstrap themselves before any scene loads.

**Content matching:** every colour in a gate's `palette` must be in the config's luggage pool, or
a flight that asks for it can never be filled. A gate with `wrappedLinesPerFlight` above 0 needs a
`Wrapper` in the scene. **The validator checks neither yet** — check both by hand.

**Overrides:** keep scene overrides for transform, gate number, intentional turn direction, spawn
points, and connected interactables. Apply single intended properties from Unity's Overrides
panel; never Apply All from a configured instance. When a field moves from a prefab into a config
asset, revert the stale override explicitly — nothing detects override drift.

### Floors

`Assets/Prefab/Decoration/FloorTile.prefab` is a 1 × 1 m floor piece: the root's X/Z scale is its
size in metres (default 10 × 10), the walkable surface sits at the root's Y, and a 0.5 m
`BoxCollider` hangs below it. Change the look by dragging any `Assets/Material/Map/Floor_*.mat`
onto it (it lands on the `Surface` child). The artist's source images are in
`Assets/Texture/Floor/`.

The `Floor_*` materials use `Shader Graphs/FloorTexture`, which maps the texture in **world
space** (`_TileSize` = metres per texture repeat). Scaling a piece never stretches the pattern,
neighbouring pieces line up, and the materials also work on the older `Floor.prefab` meshes, whose
UVs are collapsed onto one palette texel. Because the mapping ignores the object's rotation, use
`_PatternRotation` to turn a pattern, and `_BaseColor` (Tint) to pull a texture toward the palette.
Checkers default to a 24 m repeat (2 m squares, like `FloorChecker`); carpets to 8 m.

Keep the `Floor_*` catalog untinted. For a place-specific look, make a **Material Variant** of a
catalog material and override only what differs — `MainMenu` uses
`Assets/Material/Map/Floor_MainMenu.mat`, a variant of `Floor_Checkered3_BiggerBlue` with a
lavender Tint. There the floor is `MenuEnvironment/Interior/Floor_Terminal` plus a blue-carpet
`Rug_Lineup` under the lobby slots, both with their collider switched off (the menu's physics
ground is a separate mesh at y −3.80). The old checker slab, `Floor_Terminal_Indoor`, is kept
disabled next to them.

`Level1` zones its floor by job, under `Environment/Floor Zones`. The hall is
`Assets/Material/Map/Floor_Level1.mat`, a variant of `Floor_Checkered3_BiggerBlue` with a purple
Tint (0.62, 0.52, 0.85). The gate approach is `Floor_RedCarpet`, and the pile is navy carpet inside
the pen prefab. The hall is two tiles, `Hall West` and `Hall East`, split at x = −21, because the
old floor steps from y 0.00 to 0.10 there. Every tile sits 2–5 cm above the old `Floor` meshes with
its collider off; physics stays on the old meshes. Stack zones by height — hall 0.02 / 0.12, pen
0.05, runway 0.15 — or they z-fight. Pick zone colours against the bags, not in isolation: a colour
that swallows red, green or yellow luggage makes the level harder to read.

### Route QA

- Deck height and uniform scale at every seam.
- `BeltSurface.physicMaterial` on each solid deck.
- Triggers cover the visible route, and exit velocity lands on the next surface.
- Run every pool colour through the route, and a wrapped bag where the level has a wrapper.
- Station output clearance, and that a processed bag can rejoin the route.
- Sink volumes recycle missed luggage without catching valid deliveries.

### Play Mode acceptance

- zero missing-script or missing-reference errors on load;
- correct level name, timer, and pool, with exactly one active HUD;
- 1–4 real players spawn at distinct points;
- pause and resume restore `Time.timeScale`;
- each required station accepts a bag, processes it, and releases a grabbable result;
- score and real per-player delivery counts update;
- results persist stars and unlock, and Continue returns to stage select.

## Level validator

`Assets/Script/Editor/LevelValidator.cs`, namespace `CarryOn.EditorTools`. Editor-only because of
Unity's `Editor/` folder rule — there is no assembly definition (see [README](../README.md)).

| Menu | Does |
|---|---|
| **Carry On ▸ Validate Open Scene** (`Ctrl/Cmd + Shift + V`) | validates the scene you're looking at |
| **Carry On ▸ Validate All Build Scenes** | opens each enabled build scene, validates, restores yours |

Findings log with the offending object as context, so clicking the console entry selects it.
Scenes with no `LevelContext` (MainMenu, ChooseStage) are skipped. Errors mean broken; warnings
mean it runs but is probably wrong.

**Level wiring** — exactly one `LevelContext` with a config; the config's `sceneName` matches the
scene it's actually used in; at least one luggage prefab, no empty slots, every entry has a
`Luggage`; a `GameManager`, `LuggageSpawner`, `LuggageSink`, `PlayerSpawner` with points, and at
least one `Gate` or `DispatchGate`; unique gate numbers; a `DispatchButton` beside any dispatch gate.

**Stations** — each needs its `snapTransform`; either a `sliderTransform` or intake/output anchors,
never both; and an `Animator` when `animationDriven` (a station with no animator never finishes and
jams its slot forever).

**Conveyor seams** — non-zero `moveSpeed`; a turn has its `turnPivot`; uniform scale (a straight
may be widened along its own X); the
`BeltSurface` physic material on every deck; and one deck height per scene
(`position.y + 4.0 × scale.y`). The seam check groups pieces by height, so it can't tell a stepped
seam from a deliberately separate run — the archived `DesignScene`'s "Service Spur" was the known
example.

**Known findings:** none. After the September 2026 cleanup, Validate All Build Scenes reports no
problems for `Level1` and `Level2`. Archived scenes are not validated.

Add a check whenever you catch yourself saying "I forgot to…". Rule of thumb: if the mistake can
be described numerically or as a missing reference, it belongs here. If it needs eyes — does the
route feel good, is the camera framing nice — it stays manual QA.

## Testing with DebugAutoJoin

`Level1` and `Level2` carry `DebugAutoJoin`. On Play (execution order `-200`, editor-only) it asks
the persistent `PlayerSystem` to join two real `PlayerInputManager` players:

| Player | Scheme | Move | Dash | Grab | Use station |
|---|---|---|---|---|---|
| P1 | `KeyboardLeft` | WASD | Left Shift | E | F |
| P2 | `KeyboardRight` | arrows | Right Shift | `/` | L |

This is the production input path — no `InjectInput`, no placed character clones, no special
pause branch. Player index, scoring attribution, actions, spawning, station use, and stealing all
reproduce the shipped setup.

**Smoke test**

1. Enter Play Mode, wait for the 3–2–1 countdown.
2. Confirm two players with distinct schemes spawn at distinct points.
3. Move, dash, grab, steal, drop, and throw.
4. Use every station.
5. Deliver with each player and verify separate attribution; deliver a wrong colour and confirm it
   bounces back.
6. Pause and resume; confirm `Time.timeScale` returns to 1.
7. Check the Console for new warnings and errors.

## Archived scenes

`Assets/Scenes/Archive/` holds `Tutorial`, `Level3`, `Level4` and `DesignScene`, moved there in
September 2026. They are **reference only** — out of Build Settings, not validated, not
maintained. (`Level2` sits in the same folder but is live and in the build; none of this applies
to it. `Level1` there is the September pile layout, archived on 18 Oct 2026 when the new
environment took the build slot; it plays under the current rules.) Their configs (`LevelConfig_Tutorial`, `_Stage3`, `_Stage4`, `_Design`) are kept so
the scenes still open wired.

They were built for the old rules — bag lifetimes, waves, Sticky/Fragile luggage, washers,
pressure-plate doors, numbered gates — and no longer play correctly:

- Every bag in them counts as Red, while their gates ask for Red, Blue, Green and Yellow, so most
  flights can never be filled.
- Their Sticky and Fragile bags behave as plain bags; the behaviour code is gone.
- Their `WashingStation`s are inert: the prefab keeps its model and animator but has no machine
  component.

Mine them for layout, belt routing and decoration; don't expect them to run. `TutorialRoom.cs`
(rooms that open their doors once their bags are cleared) is used only by the archived Tutorial
and is kept for the rebuild. The last commit of the old design is `bc233e5`.
