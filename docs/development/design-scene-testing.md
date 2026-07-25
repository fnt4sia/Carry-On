# DesignScene testing

**Script:** `MainMenu/DebugAutoJoin.cs` — **Scene:** `Assets/Scenes/Stages/DesignScene.unity`

DesignScene is an editor-only sandbox outside Build Settings. It contains a normal `LevelContext` using `LevelConfig_Design`, the reusable game/spawner prefabs, four player spawn points, a washer, a wrapper, and `DebugAutoJoin`.

On Play, `DebugAutoJoin` asks the persistent `PlayerSystem` to create two normal `PlayerInputManager` players:

| Player | Scheme | Move | Dash | Grab | Use station |
|---|---|---|---|---|---|
| P1 | `KeyboardLeft` | WASD | Left Shift | E | F |
| P2 | `KeyboardRight` | arrows | Right Shift | Right Ctrl | L |

This is the production input path. There are no `InjectInput` methods, placed character clones, or special pause branch. Consequently, player index, scoring attribution, input actions, spawning, station use, and stealing reproduce the shipped setup.

## Smoke test

1. Enter Play Mode and wait for the 3–2–1 countdown.
2. Confirm two players with distinct keyboard schemes spawn at distinct points.
3. Move, dash, grab/steal, drop/throw, and use every station.
4. Confirm the luggage timer pauses while the bag is in a station and resumes on output.
5. Deliver with each player and verify separate delivery attribution.
7. Pause/resume and confirm `Time.timeScale` returns to 1.
8. Check the Console for new warnings/errors.

DesignScene validates mechanics quickly, but always validate the target stage afterward for geometry, routes, camera, and scene overrides.
