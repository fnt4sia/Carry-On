# Join and spawn

**Scripts:** `MainMenu/PlayerSystem.cs`, `Game/Player/PlayerSpawner.cs`, `MainMenu/DebugAutoJoin.cs`

One to four players share a screen. `PlayerSystem` owns a persistent `PlayerInputManager`; joined player objects, paired devices, control schemes, and `playerIndex` values survive menu and scene transitions.

## Joining

Joining is polled only in `MainMenu`, because one physical keyboard is shared by two schemes:

| Join input | Scheme | Gameplay half |
|---|---|---|
| Space | `KeyboardLeft` | WASD, Left Shift, E, F |
| Right Ctrl | `KeyboardRight` | arrows, Right Shift, Right Ctrl, L |
| gamepad South | `Gamepad` | one unpaired gamepad |

A keyboard scheme is free when no current player uses its scheme name; a gamepad is free when no player has paired that device. `LastJoinFrame` prevents a join press from also submitting a menu button in the same frame.

`PlayerSystem` bootstraps from `Assets/Resources/Runtime/PlayerSystem.prefab`, rejects duplicate instances through `SingletonBehaviour`, and persists across scenes. Always use `PlayerSystem.Instance.Manager` rather than finding a manager in the scene.

## Shared input asset

`Assets/InputAction/GameInput.inputactions` is the single action source:

- `Player`: Move, Dash, Grab, UseStation;
- `System`: Pause;
- `Map`: Move, Confirm, Back.

Joining remains raw-device polling because of the two keyboard schemes; gameplay, pause, and map navigation do not construct independent actions in code.

## Gameplay spawning

Each gameplay scene has a `PlayerSpawner` with up to four ordered spawn transforms. On scene start it sorts active `PlayerInput` objects by `playerIndex`, restores them, and moves each to the matching point. `MovePlayerToSpawn` also supports hazard respawn and zeros velocity before teleporting.

If an index has no point, the spawner uses point 0 and warns. A production level should author four valid points even if most tests use two players.

## DesignScene auto-join

`DebugAutoJoin` is editor-only. At execution order `-200`, it uses the real `PlayerInputManager` to join `KeyboardLeft` and `KeyboardRight`. There is no injected input path: DesignScene exercises the same player prefabs, actions, schemes, and indices as the shipped flow. See [design-scene testing](../development/design-scene-testing.md).

Lobby presentation is documented separately in [main menu](../ui/main-menu.md).
