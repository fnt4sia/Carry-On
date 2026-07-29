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
| Round | `gameTime`, `star1Score`, `star2Score`, `star3Score` |
| Content | `luggagePrefabs`, `luggageLifetime` |
| Waves | `waveDelay`, `luggagePerWave`, `intraWaveInterval` |
| Scoring | `scoreCorrectDelivery`, `scoreMissingProcess`, `scoreTimerExpired`, `scoreWrongGateDelivery` |

`levelId` is a save key — renaming one after release orphans every existing save record and needs
an explicit migration. `OnValidate` enforces positive timings and ordered star thresholds.
`CalculateStars` has no test coverage (nothing here does), so check star thresholds by hand.

Behaviour is defined by each luggage prefab; there is no behaviour list on `LevelConfig`. Change
`luggagePrefabs` to change which stations a level requires.

## Current balance

| Config | Round | Stars | Lifetime | Wave delay | Per wave | Interval |
|---|---:|---|---:|---:|---:|---:|
| Tutorial | 180s | 20 / 40 / 60 | 45s | 12s | 3 | 2.5s |
| Stage 1 | 120s | 30 / 60 / 90 | 28s | 11s | 4 | 2.0s |
| Stage 2 | 120s | 40 / 80 / 120 | 24s | 9s | 5 | 1.7s |
| Stage 3 | 120s | 50 / 100 / 150 | 22s | 8s | 5 | 1.4s |
| Stage 4 | 120s | 60 / 120 / 180 | 19s | 7s | 6 | 1.1s |
| Design | 999s | 10 / 30 / 60 | 30s | 10s | 5 | 3.0s |

Every config uses +10 correct, −5 missing process, −5 expiry, −5 wrong gate. Tutorial and Stage 1
are unlocked by default; the rest unlock through the `nextLevel` chain.

## Dangling configs

**Five of the six configs point at scenes that no longer exist.** `Stage_1..4` and
`Stage Tutorial` were deleted in the July 2026 DesignScene consolidation, but their config assets
were kept:

| Config | `sceneName` | Scene exists? |
|---|---|---|
| `LevelConfig_Design` | `DesignScene` | yes (editor-only, not in Build Settings) |
| `LevelConfig_Tutorial` | `Stage Tutorial` | **no** |
| `LevelConfig_Stage1..4` | `Stage_1` … `Stage_4` | **no** |

So no gameplay scene ships today, and the stage-select chain routes to scenes that can't load.
The configs are still the right place for the balance curve above — treat them as the surviving
design record, and fix `sceneName` when the stage scenes are rebuilt. `SceneLoader` validates a
target against Build Settings before loading, so a bad route fails loudly rather than hanging.

## Authoring a level

1. Create a `LevelConfig` with a unique stable `levelId`, the exact Build Settings scene name,
   display copy, balance, luggage pool, unlock state, and `nextLevel`.
2. Add one root `Level Context` and assign only that config.
3. Add a fresh `GameManager.prefab` instance. Its nested `GameHUD` already owns all UI refs.
4. Add a fresh `Spawner.prefab` instance — it reads the level context.
5. Add a `PlayerSpawner` with four ordered spawn points.
6. Add camera, delivery gates, sink/void coverage, conveyors, and the stations the config's
   luggage pool requires.
7. Add the scene to Build Settings *before* routing a node or `nextLevel` to it.

Never place a per-level `AudioManager`, `PlayerSystem`, `SceneLoader`, or `ProgressionService` —
persistent services bootstrap themselves before any scene loads.

**Content matching:** Sticky in the pool needs a washer, Fragile needs a wrapper, and with
multiple gates every gate number must be unique.

**Overrides:** keep scene overrides for transform, gate number, intentional turn direction, spawn
points, and connected interactables. Apply single intended properties from Unity's Overrides
panel; never Apply All from a configured instance. When a field moves from a prefab into a config
asset, revert the stale override explicitly — nothing detects override drift.

### Route QA

- Deck height and uniform scale at every seam.
- `BeltSurface.physicMaterial` on each solid deck.
- Triggers cover the visible route, and exit velocity lands on the next surface.
- Run Normal, Sticky, and Fragile through the applicable routes.
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

**Stations match content** — the check that prevents an unwinnable level. Sticky spawns require a
`WashingMachine`, Fragile require a `Wrapper`. Each station needs its `snapTransform`,
`sliderTransform`, and an `Animator` when `animationDriven` (a station with no animator never
finishes and jams its slot forever). A washer or wrapper with no replacement prefab warns — it
silently falls back to an in-place swap.

**Conveyor seams** — non-zero `moveSpeed`; a turn has its `turnPivot`; uniform scale; the
`BeltSurface` physic material on every deck; and one deck height per scene
(`position.y + 4.0 × scale.y`).

Because Build Settings holds only the two menu scenes, **Validate All Build Scenes currently
validates nothing** — it skips both for having no `LevelContext`. Use Validate Open Scene on
`DesignScene` until a gameplay scene is back in the build.

Add a check whenever you catch yourself saying "I forgot to…". Rule of thumb: if the mistake can
be described numerically or as a missing reference, it belongs here. If it needs eyes — does the
route feel good, is the camera framing nice — it stays manual QA.

## DesignScene

`Assets/Scenes/Stages/DesignScene.unity`, editor-only, outside Build Settings. It holds a normal
`LevelContext` using `LevelConfig_Design`, the reusable game and spawner prefabs, four player
spawn points, a washer, a wrapper, and `DebugAutoJoin`.

On Play, `DebugAutoJoin` (execution order `-200`, editor-only) asks the persistent `PlayerSystem`
to join two real `PlayerInputManager` players:

| Player | Scheme | Move | Dash | Grab | Use station |
|---|---|---|---|---|---|
| P1 | `KeyboardLeft` | WASD | Left Shift | E | F |
| P2 | `KeyboardRight` | arrows | Right Shift | Right Ctrl | L |

This is the production input path — no `InjectInput`, no placed character clones, no special
pause branch. Player index, scoring attribution, actions, spawning, station use, and stealing all
reproduce the shipped setup.

**Smoke test**

1. Enter Play Mode, wait for the 3–2–1 countdown.
2. Confirm two players with distinct schemes spawn at distinct points.
3. Move, dash, grab, steal, drop, and throw.
4. Use every station.
5. Confirm the luggage timer pauses in a station and resumes on output.
6. Deliver with each player and verify separate attribution.
7. Pause and resume; confirm `Time.timeScale` returns to 1.
8. Check the Console for new warnings and errors.

DesignScene validates mechanics fast, but its geometry and overrides are its own — always
validate the real target scene afterwards.
