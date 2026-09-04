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
| Tutorial | 180s | 20 / 40 / 60 | 35s | 12s | 3 | 2.5s |
| Stage 1 | 120s | 30 / 60 / 90 | 28s | 11s | 4 | 2.0s |
| Stage 2 | 120s | 40 / 80 / 120 | 24s | 9s | 5 | 1.7s |
| Stage 3 | 120s | 50 / 100 / 150 | 22s | 8s | 5 | 1.4s |
| Stage 4 | 120s | 60 / 120 / 180 | 19s | 7s | 6 | 1.1s |
| Design | 999s | 10 / 30 / 60 | 30s | 10s | 5 | 3.0s |

Every config uses +10 correct, −5 missing process, −5 expiry, −5 wrong gate. Tutorial and Stage 1
are unlocked by default; the rest unlock through the `nextLevel` chain.

## Config ↔ scene mapping

Wired September 2026. Every config names a real scene, every scene's `LevelContext` points at its
own config, and all of them are in Build Settings — `SceneLoader` validates a target against Build
Settings before loading, so the config alone is never enough.

| Config | `sceneName` | Build index | Luggage pool |
|---|---|---:|---|
| `LevelConfig_Tutorial` | `Tutorial` | 3 | Normal |
| `LevelConfig_Stage1` | `Level1` | 4 | Normal |
| `LevelConfig_Stage2` | `Level2` | 5 | Normal |
| `LevelConfig_Stage3` | `Level3` | 6 | Normal, Sticky, Fragile |
| `LevelConfig_Stage4` | `Level4` | 7 | Normal, Sticky, Fragile |
| `LevelConfig_Design` | `DesignScene` | 2 | Normal, Sticky, Fragile |

**The pool follows the stations the scene actually has.** Tutorial, Level1 and Level2 contain no
washer and no wrapper, so they can only run Normal — a Sticky or Fragile bag there can never be
processed and every delivery is a penalty. Level3 (2 washers, 2 wrappers) and Level4 (2 washers,
3 wrappers) carry the anomalies. If you add a station to a scene, widen that config's pool to
match; if you widen a pool, add the station first. The validator enforces exactly this pairing.

`Stage 1 is no longer an alias for DesignScene` — it was one from August 2026 until this wiring,
which meant it ran Design's 999-second timer instead of its own numbers. It now loads `Level1`
and plays by `LevelConfig_Stage1`.

Stage select routes Node 1–4 at `LevelConfig_Stage1..4`, and `nextLevel` chains
Tutorial → Stage 1 → Stage 2 → Stage 3 → Stage 4. Tutorial and Stage 1 are unlocked by default.

### Still unwired

- **`LevelConfig_Tutorial` has no map node.** The chain reaches Stage 1 *from* it, but nothing on
  `ChooseStage` routes *to* it — the map only has four nodes.
- **`Level3 1`** is a duplicate of `Level3` (same 2 washers / 2 wrappers) and is deliberately left
  pointing at `LevelConfig_Design` and out of the build. Two scenes cannot claim one config's
  `sceneName`. Delete it or give it its own config.
- **`Test`** is decoration staging with no `LevelContext`, and stays out of the build.
- **No stage scene has a `LuggageSink`.** Only `DesignScene` does. Missed luggage is never
  recycled in any real level — see the validator errors below.

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

**Validate All Build Scenes now covers every stage** — `DesignScene`, `Tutorial`, and `Level1..4`
are all in Build Settings. It still skips MainMenu and ChooseStage for having no `LevelContext`.
`Level3 1` and `Test` are out of the build, so validate those with Validate Open Scene.

### Known outstanding findings

Config wiring is clean; what remains is level geometry and content, unchanged by that pass:

| Scene | Finding |
|---|---|
| all five stages | **no `LuggageSink`** — missed luggage never recycles |
| `Tutorial` | **no `LuggageSpawner` — deliberate**, see [Tutorial](#tutorial); all 9 gates are numbered `1`; 8 non-uniform `Conveyor Straight` scales; stepped deck seam |
| `Level1` | stepped deck seam (y 3.99 vs 4.6) |
| `Level4` | stepped deck seam (y 14.62 vs 25.35); 3 decks missing `BeltSurface` |
| `DesignScene` | stepped deck seam — the standing "Service Spur" false positive |

Add a check whenever you catch yourself saying "I forgot to…". Rule of thumb: if the mistake can
be described numerically or as a missing reference, it belongs here. If it needs eyes — does the
route feel good, is the camera framing nice — it stays manual QA.

## Tutorial

**Script:** `Game/World/TutorialRoom.cs`

Tutorial is the one stage that does not spawn luggage. It is three rooms in a line, each holding
its own authored bags and the doors out of it, wired with a `TutorialRoom` under `Tutorial Rooms`:

| Room | Volume centre | Luggage | Behaviour | On completion |
|---|---|---:|---|---|
| `Room 1 - Arrivals` | `(-68, 12, 56)` | 9 on the floor | tutorial, no timer | opens `GateWay`, `GateWay (2)` |
| `Room 2 - Carousel` | `(6, 12, 40)` | 6 on `FullsetConveyor` | tutorial, no timer | opens `GateWay (3)`, `GateWay (4)` |
| `Room 3 - Departures` | `(3, 12, 155)` | 6 on `FullsetConveyor (1)` | **timed**, 35 s | ends the round |

A room switches its `Luggage` child on when a player enters its `BoxCollider`, and completes when
every bag under that child is gone. Room 1's root is left on because the players spawn inside it;
rooms 2 and 3 are authored **off**. Completion opens doors, so the room order is scene wiring —
the component never knows where it sits in the chain.

**Corridor doors come in pairs.** `GateWay`/`GateWay (2)` sit at the two ends of the room 1 → 2
corridor and `GateWay (3)`/`GateWay (4)` at the ends of room 2 → 3. Both ends must be listed on
the same room or players walk into a corridor and are sealed in. All four are authored **closed**;
they were authored open before this wiring existed, so a room had nothing to unlock.

**Timer freedom comes from the bag, not the room.** `isTutorialLuggage` is a serialized field on
each `Luggage`, so rooms 1 and 2 are silent and room 3 counts down with no code deciding it. Only
room 3 uses `LevelConfig_Tutorial.luggageLifetime`; the config's wave fields (`waveDelay`,
`luggagePerWave`, `intraWaveInterval`) are dead here because nothing reads them without a spawner.

**Falling bags are put back, not recycled.** With no spawner and no sink, a bag knocked off the
floor is gone for good and its room could never complete. `TutorialRoom` returns anything that
drops below `resetBelowY` (default −20) to its start pose. An *expired* bag still counts as
finished — the player already took the penalty, and keeping it on the tally would deadlock the
room.

Both belts are closed loops, so bags circulate until someone grabs them. Delivering a scene-placed
bag destroys it rather than pooling it; that is the normal path here and no longer warns (see
[luggage](mechanics/luggage.md#pooling)).

## DesignScene

`Assets/Scenes/Stages/DesignScene.unity`, build index 2 and doubling as Stage 1. It holds a normal
`LevelContext` using `LevelConfig_Design`, the reusable game and spawner prefabs, four player
spawn points, a washer, a wrapper, and `DebugAutoJoin`.

On Play, `DebugAutoJoin` (execution order `-200`, editor-only) asks the persistent `PlayerSystem`
to join two real `PlayerInputManager` players:

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
5. Confirm the luggage timer pauses in a station and resumes on output.
6. Deliver with each player and verify separate attribution.
7. Pause and resume; confirm `Time.timeScale` returns to 1.
8. Check the Console for new warnings and errors.

DesignScene validates mechanics fast, but its geometry and overrides are its own — always
validate the real target scene afterwards.
