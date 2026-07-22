# Architecture and services

## Assemblies

There are none. Every script compiles into Unity's single default assembly, `Assembly-CSharp`. The project previously split into `CarryOn.Core` / `CarryOn.Game` / `CarryOn.Menu` via `.asmdef` files; those were removed deliberately in July 2026 to keep the project simple. **Do not add them back without asking.**

Scripts under `Assets/Script/Editor/` are still editor-only and excluded from builds — that comes free from Unity's `Editor/` folder-name rule, not from any config file.

## Dependency direction

The old assembly split is gone, so the compiler no longer enforces this. It is now a convention, and it is on you to keep:

```text
Core (shared infra, no scene/UI deps)
  ^                    ^
Game (gameplay)     Menu (MainMenu, ChooseStage)
```

Code in `Game/Core/`, `Game/Config/`, `Game/Scoring/`, and `Game/Services/` should not reach into gameplay types like `Luggage` or `Gate`. Gameplay and menu code should not reach into each other. If a rule can be plain C#, keep it free of scene and UI dependencies.

## Service lifetimes

`SingletonBehaviour<T>` standardizes duplicate rejection, `Instance` cleanup, and optional persistence.

| Service | Lifetime | Creation |
|---|---|---|
| `PlayerSystem` | persistent | `Resources/Runtime/PlayerSystem.prefab` before scene load |
| `AudioManager` | persistent | `Resources/Runtime/AudioManager.prefab` before scene load |
| `SceneLoader` | persistent | `Resources/Runtime/SceneLoader.prefab`, with code-built fallback |
| `ProgressionService` | persistent | code bootstrap before scene load |
| `LevelContext` | one scene | authored once in each gameplay scene |
| `GameManager` | one round | reusable gameplay prefab instance |
| `LuggageSpawner` / `PlayerSpawner` | one level | reusable scene prefab/component instance |

Reach services through their `Instance`/static API. Do not serialize a drag-and-drop reference from one reusable prefab to a scene service.

## Level and round data flow

```text
LevelContext -> LevelConfig
      |             |
      +-> GameManager, LuggageSpawner, Gate, Luggage
                     |
              ScoreBoard + ScoringRules
                     |
                 GameResult
                /          \
         GameHUD       ProgressionService
```

`LevelContext` is the only intentionally scene-owned reference to level-wide rules and content. A reusable prefab does not contain its own `LevelConfig`; it reads `LevelContext.CurrentConfig`. Scene geometry, placement, identity, and unique object-to-object wiring remain authored in the scene. This prevents applying a prefab from overwriting every stage with one stage's rules.

`GameManager` owns round state and events but no UI references. `ScoreBoard`, `ScoringRules`, and `GameResult` isolate deterministic rules from scene objects. `RoundScoreContext` is the narrow bridge used by gates and luggage expiry to reach the active board.

`GameHUD` subscribes to `GameManager` events. Visual presenters follow this rule generally: they listen to gameplay state and never own rules, so visuals can be replaced without changing the mechanic.

## Reference rules

- Internal prefab references: serialize and wire once inside the prefab.
- Shared tuning: reference a ScriptableObject in `Assets/Config/Tuning/`.
- Per-level rules/content: put them in that scene's `LevelConfig`, selected by `LevelContext`.
- Per-instance geometry/identity: keep it on the scene instance only when it genuinely differs, such as a gate number, spawn transform, intentional turn direction, or connected door list. Internal references such as a turn prefab's pivot stay wired in the prefab.
- Services: use their stable access API.

See [asset and prefab guidelines](asset-and-prefab-guidelines.md) for the practical workflow.
