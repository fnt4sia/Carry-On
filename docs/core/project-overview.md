# Project overview

Carry On is a chaotic 1–4 player local co-op airport baggage-handling game by Unbounded Souls. Its loop is: luggage arrives in waves, players identify and process it, deliver it to the correct gate before expiry, then earn a score and up to three stars.

## Stack

- Unity `6000.3.14f1` with URP.
- New Input System with `PlayerInputManager` and one shared `GameInput.inputactions` asset.
- Physics-driven luggage, a `ConfigurableJoint` carry system, and velocity-steering conveyors.
- ScriptableObjects for level data and shared feel/balance tuning.
- No automated tests. The Level Validator (`Carry On ▸ Validate…`) and playtesting are the safety net.

## Repository layout

```text
Assets/
  Script/
    Game/Core/       shared runtime foundation and round controller
    Game/Config/     reusable tuning ScriptableObject types
    Game/Scoring/    plain score/result rules
    Game/Services/   scene loading and progression persistence
    Game/UI/         HUD and world-feedback presenters
    Game/{Player,Luggage,Stations,World}/
    MainMenu/        persistent player join and lobby UI
    ChooseStage/     map token, nodes, and popup
    Editor/          Level Validator (editor-only, excluded from builds)
  Config/            LevelConfig assets and Config/Tuning assets
  Prefab/            reusable gameplay, service, UI, and visual prefabs
  Resources/Runtime/ persistent service bootstrap prefabs
  Scenes/{Menu,Level}/
```

Every script compiles into Unity's single default assembly. There are no `.asmdef` files. See [architecture and services](architecture-and-services.md).

## Scene flow

Enabled build scenes are:

| Build index | Scene |
|---:|---|
| 0 | `Menu/MainMenu` |
| 1 | `Level/Stage Tutorial` |
| 2–5 | `Level/Stage_1` through `Level/Stage_4` |
| 6 | `Menu/ChooseStage` |

`Level/DesignScene` is an editor sandbox and is not in Build Settings.

```text
MainMenu -> ChooseStage -> gameplay stage -> next stage or ChooseStage
    ^                              |
    +----------- lobby ------------+
```

All transitions use the persistent asynchronous `SceneLoader`; direct gameplay calls should not use `SceneManager.LoadScene`. Joined `PlayerInput` objects persist through the flow. The stage-select map temporarily deactivates them, and each gameplay scene's `PlayerSpawner` restores their positions.

## Runtime ownership

- Persistent services: `PlayerSystem`, `AudioManager`, `SceneLoader`, `ProgressionService`.
- Per-level context: one `LevelContext` selecting one `LevelConfig`.
- Per-round logic: one `GameManager`, with scoring in a plain `ScoreBoard`.
- Presentation: `GameHUD` and the luggage timer prefab subscribe to or read gameplay state.

Read [asset and prefab guidelines](asset-and-prefab-guidelines.md) before applying prefab changes or adding per-level data.

## Tags and layers

Logic identifies luggage by its `Luggage` component; layers still narrow physics queries. Custom layers are `Water` (4), `Luggage` (6), `Player` (7), `GrabbedLuggage` (8), `Wall` (9), and `WorldUI` (10). `WorldUI` is required for world-space timer and station overlays.
