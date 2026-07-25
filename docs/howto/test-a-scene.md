# Test a scene in isolation

Play a gameplay scene straight from the editor — two players, no MainMenu, no lobby join.

## DesignScene (the sandbox)

Open `Assets/Scenes/Stages/DesignScene.unity` and press Play.

`DebugAutoJoin` (`Assets/Script/MainMenu/DebugAutoJoin.cs`) asks the persistent
`PlayerSystem` to create two keyboard players automatically. This is the **real** input
path — same joining, spawning, scoring, and stations as the shipped game — so what works
here works in a stage.

| Player | Scheme | Move | Dash | Grab | Use station |
|---|---|---|---|---|---|
| P1 | KeyboardLeft | WASD | Left Shift | E | F |
| P2 | KeyboardRight | Arrows | Right Shift | Right Ctrl | L |

`DebugAutoJoin` has a **Keyboard Player Count** field (1–2) if you want to test solo.

## Why this works from any scene

`PlayerSystem` boots automatically before the first scene loads, so it exists even when you
skip MainMenu. Normal lobby joining is off outside MainMenu — `DebugAutoJoin` is the
editor-only stand-in that force-joins players. See
[join and spawn](../player/join-and-spawn.md).

## Limits

- **Editor only.** `DebugAutoJoin` is wrapped in `#if UNITY_EDITOR`. A build launched
  straight into a sandbox scene would have no players. Fine — sandboxes aren't shipped.
- **Keyboard only.** Auto-join spawns keyboard players, no gamepad. To test a gamepad,
  start from MainMenu and press (A)/South to join.

## Playing a different scene the same way

To sandbox another scene, give it a `LevelContext` (with a `LevelConfig`), the game/spawner
prefab, four `SpawnPoint_1..4`, a `PlayerSpawner`, and a `DebugAutoJoin` object — mirror
what `DesignScene` has. Then always validate the real target stage too:
run **Carry On ▸ Validate All Build Scenes** and playtest for geometry, routes, and camera.

See also [design-scene testing](../development/design-scene-testing.md) for the full smoke-test checklist.
