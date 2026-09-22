# Carry On — technical documentation

Carry On is a chaotic 1–4 player local co-op airport baggage-handling game by Unbounded Souls.
The loop: luggage arrives in waves, players identify and process it, deliver it to the correct
gate before it expires, then earn a score and up to three stars.

These docs are organised by **mechanic** — one file per thing the game actually does. Open the
one that matches your task; don't load the folder. Code and serialized Unity assets are the
source of truth. If a doc disagrees with the code, the code wins and the doc gets fixed in the
same pass.

## Mechanics

| Doc | Covers |
|---|---|
| [Player](mechanics/player.md) | joining, spawning, movement, dash, grab / carry / throw, hand IK, the Annie animator |
| [Luggage](mechanics/luggage.md) | colour, wrapping, spawn pacing, pooling, what was removed |
| [Conveyors](mechanics/conveyors.md) | belt steering, straight and turn pieces, seam rules, the two-collider design |
| [Stations](mechanics/stations.md) | wrapper lifecycle, manual crank, placement, output clearance |
| [Delivery and scoring](mechanics/delivery-and-scoring.md) | gates, scoring priority, round flow, results |
| [Hazards and props](mechanics/hazards-and-props.md) | pressure plates, gateways, rotating platforms, one-way doors, water |

## Supporting

| Doc | Covers |
|---|---|
| [Levels](levels.md) | `LevelConfig` fields, current balance, authoring checklist, the level validator |
| [UI](ui.md) | main menu / lobby, stage select, in-game HUD |
| [Services](services.md) | audio, camera, scene loading, save and progression |

## Stack

- Unity `6000.3.14f1`, URP.
- New Input System, one shared `GameInput.inputactions`, `PlayerInputManager` for local join.
- Physics-driven luggage, a `ConfigurableJoint` carry, velocity-steering conveyors.
- `LevelConfig` ScriptableObjects for per-level rules. Feel and balance are serialized on the
  prefab that uses them.
- **No automated tests anywhere in the project.** The [level validator](levels.md#level-validator)
  and playtesting are the entire safety net.

## Repository layout

```text
Assets/
  Script/
    Game/Core/       shared runtime foundation, round controller, level context
    Game/Scoring/    plain C# score and result rules
    Game/Services/   scene loading, progression persistence
    Game/UI/         HUD presenter
    Game/{Player,Luggage,Stations,World}/
    MainMenu/        persistent player join and lobby
    ChooseStage/     map planes, nodes, level ticket
    Editor/          level validator (editor-only, excluded from builds)
  Config/            LevelConfig assets
  Prefab/            Character, Decoration, Environment, Luggage, Manager, Map, Station, UI
  Resources/Runtime/ persistent service bootstrap prefabs
  Scenes/{Menu,Stages,Archive}/   Archive = old-design scenes, reference only
```

Every script compiles into Unity's single default assembly, `Assembly-CSharp`. There are **no
`.asmdef` or `.asmref` files** — an earlier `CarryOn.Core` / `CarryOn.Game` / `CarryOn.Menu`
split was removed on purpose to keep the project simple. Don't add them back without asking.
Scripts under `Assets/Script/Editor/` are still excluded from builds, which comes free from
Unity's `Editor/` folder-name rule rather than from any config file.

## Scenes

Build Settings contains **four scenes**:

| Index | Scene |
|---:|---|
| 0 | `Menu/MainMenu` |
| 1 | `Menu/ChooseStage` |
| 2 | `Stages/Level1` |
| 3 | `Stages/Level2` |

`Level1` and `Level2` are the flight-manifest redesign and point their `LevelContext` at
`LevelConfig_Stage1` / `_Stage2`. `Tutorial`, `Level3`, `Level4` and `DesignScene` were built for
the old rules and moved to `Scenes/Archive/` in September 2026 — reference only, out of the build.
`Test` (decoration staging) also stays out. See [levels](levels.md#archived-scenes).

```text
MainMenu -> ChooseStage -> gameplay stage -> next stage or ChooseStage
    ^                              |
    +----------- lobby ------------+
```

All transitions go through the persistent async `SceneLoader`. Gameplay code should never call
`SceneManager.LoadScene` directly. Joined `PlayerInput` objects persist across the whole flow;
stage select deactivates them and each gameplay scene's `PlayerSpawner` repositions them.

## How the pieces own each other

Three ideas hold the project together:

1. **A prefab owns its own behaviour and its own children.** Edit the prefab once; every copy
   updates.
2. **Feel and balance live on the prefab that uses them**, as serialized fields with sane
   defaults in the script. `Assets/Config/Tuning/` and its five tuning ScriptableObject types
   were removed in July 2026 — the values moved onto the prefabs unchanged. Don't reintroduce
   them. Values are still never retyped per scene instance: edit the prefab, not the copy.
3. **Each gameplay scene has exactly one `LevelContext`**, and it picks that scene's
   `LevelConfig`. A prefab never carries its own level rules; it *asks* `LevelContext` for them.
   This is why editing one prefab can't overwrite every stage with Stage 1's values.

### Service lifetimes

`SingletonBehaviour<T>` standardises duplicate rejection, `Instance` cleanup, and persistence.

| Service | Lifetime | Created by |
|---|---|---|
| `PlayerSystem` | persistent | `Resources/Runtime/PlayerSystem.prefab`, before scene load |
| `AudioManager` | persistent | `Resources/Runtime/AudioManager.prefab`, before scene load |
| `SceneLoader` | persistent | `Resources/Runtime/SceneLoader.prefab`, code-built fallback |
| `ProgressionService` | persistent | code bootstrap, before scene load |
| `LevelContext` | one scene | authored once per gameplay scene |
| `GameManager` | one round | reusable gameplay prefab instance |
| `LuggageSpawner` / `PlayerSpawner` | one level | reusable scene prefab / component |

Persistent services are created **before any scene loads**, so they never appear in a hierarchy.
To edit one, open its prefab under `Assets/Resources/Runtime/`. Reach them through their
`Instance` / static API; never serialize a drag-and-drop reference from a reusable prefab to a
scene service.

### Dependency direction

Nothing enforces this now that the assembly split is gone — it's a convention you keep by hand:

```text
Core (shared infra, no scene/UI deps)
  ^                    ^
Game (gameplay)     Menu (MainMenu, ChooseStage)
```

`Game/Core/`, `Game/Scoring/`, and `Game/Services/` shouldn't reach into gameplay types like
`Luggage` or `Gate`. Gameplay and menu code shouldn't reach into each other. If a rule can be
plain C#, keep it free of scene and UI dependencies — that's what makes `ScoreBoard` and
`ScoringRules` reviewable by reading them.

### Where a value belongs

| Kind | Goes in | Examples |
|---|---|---|
| Shared logic and child wiring | base prefab | colliders, animator, HUD texts, machine children |
| Feel and balance | serialized field on the component, edited on its prefab | player speed, grab joint, belt speed, station shove |
| Per-level rules and content | `LevelConfig` | timer, stars, luggage pool, spawn pacing, scoring, next level |
| Which config a scene uses | scene `LevelContext` | one per gameplay scene |
| Placement and identity | scene override | transform, gate number, turn direction, connected gateways |
| Replaceable art | visual child or nested prefab | machine shell, wrap shell, loading screen |

Use a prefab variant for a deliberate reusable family (a fast conveyor, a two-door gateway).
Don't retype values on a scene instance to make one object different unless that difference is
intentional content. When a field moves from a prefab into a config asset, revert the stale
override explicitly — nothing detects override drift, including the validator.

## Tags and layers

Logic identifies luggage by its `Luggage` component, never by tag. Layers only narrow physics
queries. Custom layers: `Water` (4), `Luggage` (6), `Player` (7), `GrabbedLuggage` (8),
`Wall` (9), `WorldUI` (10). `WorldUI` is required for the player indicator pins and any other
world-space overlay.
