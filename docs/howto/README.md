# How-to — designer recipes

Task-first cards for changing the game **without reading code**. Start here: the table
says *where every knob lives*. The cards go step-by-step.

If a card and the code disagree, the code wins — fix the card in the same pass.

## Where things live

| Want to change | Where | How |
|---|---|---|
| Character body (all players) | `Assets/Prefab/Manager/PlayerSystem.prefab` → **Character Prefab** field | drag a character prefab → [change-character](change-character.md) |
| Move / dash feel | `Assets/Config/Tuning/Player_Default.asset` | edit numbers → [tune-player-feel](tune-player-feel.md) |
| Grab / carry / throw feel | `Assets/Config/Tuning/Grab_Default.asset` | edit numbers → [tune-player-feel](tune-player-feel.md) |
| Conveyor speed | `Assets/Config/Tuning/Conveyor_Straight.asset`, `Conveyor_Turn.asset` | edit numbers |
| Luggage physics defaults | `Assets/Config/Tuning/Luggage_Default.asset` | edit numbers |
| Station timings | `Assets/Config/Tuning/Station_Default.asset` | edit numbers |
| Level timer, waves, scoring, star goals, luggage pool | `Assets/Config/LevelConfig_*.asset` (the scene's `LevelContext` picks one) | edit → [tune-a-level](tune-a-level.md) |
| Test a scene fast (2 players, no menu) | `Assets/Scenes/Stages/DesignScene.unity` + `DebugAutoJoin` | press Play → [test-a-scene](test-a-scene.md) |

## Two things that are invisible in the scene (by design)

1. **Core services don't appear in any scene.** `PlayerSystem`, `AudioManager`,
   `SceneLoader`, `ProgressionService` are created automatically *before the scene loads*
   from prefabs in `Assets/Resources/Runtime/`. To edit one, open its prefab there — not
   the hierarchy. See [architecture and services](../core/architecture-and-services.md).

2. **The character prefab used to be a hidden field.** The `PlayerInputManager`'s own
   *Player Prefab* field is hidden while Join Behavior = *Manual* (which this game uses).
   That's why we added the visible **Character Prefab** field on `PlayerSystem` — always
   use that one.

## Cards

- [Change the character body](change-character.md)
- [Tune player feel (move / dash / grab / throw)](tune-player-feel.md)
- [Tune a level (timer / waves / scoring / luggage)](tune-a-level.md)
- [Test a scene in isolation](test-a-scene.md)
