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
| Scoring | `scoreCorrectDelivery` |

`levelId` is a save key — renaming one after release orphans every existing save record and needs
an explicit migration. `OnValidate` enforces positive timings and ordered star thresholds.
`CalculateStars` has no test coverage (nothing here does), so check star thresholds by hand.

Each luggage prefab defines its own flight colour; `luggagePrefabs` is the colour cycle the belt
runs (see [luggage](mechanics/luggage.md#spawn-pacing)). Bags never expire and there are no
penalties — a flight bounces a bag it doesn't want instead.

## Current balance

| Config | Round | Stars | Spawn interval | Max active |
|---|---:|---|---:|---:|
| Stage 1 | 120s | 100 / 160 / 220 | 1.0s | 28 |
| Stage 2 | 150s | 80 / 130 / 180 | 1.0s | 28 |

`LevelConfig_Tutorial`, `_Stage3`, `_Stage4` and `_Design` still exist but belong to
[archived scenes](#archived-scenes) and are not balanced for anything.

**Level2 is Level1 plus wrapping.** Same camera rig, same belt and round robin; it adds
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

**Level1's belt is red / green / yellow on a round robin.** `luggagePrefabs` lists the three
colour variants in cycle order, and the gate's `palette` lists the same three. Those two must
match: a palette colour the pool never spawns makes a flight unfillable. Blue exists as an asset
but is used by nothing.

**Level1 and Level2 run a follow camera, with the fixed one kept beside it for comparison.** The
Main Camera carries both `ArenaFollowCamera` (enabled) and `ArenaCamera` (disabled) as scene
overrides in place of `MultiplayerCamera`; flip the two checkboxes to swap shots. The authored pose
— Level1 `(-11.84, 25.1, 7)`, Level2 `(-14.82, 25.1, 7)`, both rotated `(42.12, 0, 0)` — is the
fixed shot and is deliberately left untouched, so `ArenaCamera` still frames what it always did.

The rotation is shared ground: `PlayerMovement` derives its movement axes from it and the gate
manifest board is authored to face it, so changing the angle means re-aiming the world UI and
turning the controls too.

Under the **fixed** shot everything playable must sit inside one frustum — belt loop, all four
spawn points, gate and manifest board — and nothing enforces it. Under the **follow** shot that
constraint relaxes, but a new one appears: the manifest board is world-space at the gate, so it
leaves the frame while players are out at the belt. Each level also carries a `Clone` marker wired
into the camera's `extraTargets` for testing two-player framing solo; it renders, so deactivate it
before a build. See [services](services.md#camera).

Stage 1 scores +10 per bag accepted onto a flight and nothing else: no wrong-gate penalty, no
missing-process penalty. Its thresholds are therefore just bag counts — 10 / 16 / 22 delivered —
and they are a first guess that wants a playtest.

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

Build Settings is `MainMenu`, `ChooseStage`, `Level1`, `Level2` — nothing else.

Stage select still routes Node 1–4 at `LevelConfig_Stage1..4`. Nodes 3 and 4 point at archived
configs and stay locked, because Stage 2 no longer unlocks Stage 3. A save that unlocked them
before the archive can still click them; `SceneLoader` then refuses the load with
`Scene 'Level3' is not enabled in Build Settings.` rather than crashing.

`Test` is decoration staging with no `LevelContext`, and stays out of the build.

## Authoring a level

1. Create a `LevelConfig` with a unique stable `levelId`, the exact Build Settings scene name,
   display copy, balance, luggage pool, unlock state, and `nextLevel`.
2. Add one root `Level Context` and assign only that config.
3. Add a fresh `GameManager.prefab` instance. Its nested `GameHUD` already owns all UI refs.
4. Add a fresh `Spawner.prefab` instance — it reads the level context.
5. Add a `PlayerSpawner` with four ordered spawn points.
6. Add camera, delivery gates, sink/void coverage, conveyors, and a `WrapperStation` if any gate
   asks for wrapped lines.
7. Add the scene to Build Settings *before* routing a node or `nextLevel` to it.

Never place a per-level `AudioManager`, `PlayerSystem`, `SceneLoader`, or `ProgressionService` —
persistent services bootstrap themselves before any scene loads.

**Content matching:** every colour in a gate's `palette` must be in the config's luggage pool, or
a flight that asks for it can never be filled. A gate with `wrappedLinesPerFlight` above 0 needs a
`Wrapper` in the scene. **The validator checks neither yet** — check both by hand.

**Overrides:** keep scene overrides for transform, gate number, intentional turn direction, spawn
points, and connected interactables. Apply single intended properties from Unity's Overrides
panel; never Apply All from a configured instance. When a field moves from a prefab into a config
asset, revert the stale override explicitly — nothing detects override drift.

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
- results persist stars and unlock, and Next Stage loads asynchronously.

## Level validator

`Assets/Script/Editor/LevelValidator.cs`, namespace `CarryOn.EditorTools`. Editor-only because of
Unity's `Editor/` folder rule — there is no assembly definition (see [README](README.md)).

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
least one `Gate`; unique gate numbers.

**Stations** — each needs its `snapTransform`; either a `sliderTransform` or intake/output anchors,
never both; and an `Animator` when `animationDriven` (a station with no animator never finishes and
jams its slot forever).

**Conveyor seams** — non-zero `moveSpeed`; a turn has its `turnPivot`; uniform scale; the
`BeltSurface` physic material on every deck; and one deck height per scene
(`position.y + 4.0 × scale.y`).

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
maintained. Their configs (`LevelConfig_Tutorial`, `_Stage3`, `_Stage4`, `_Design`) are kept so
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
