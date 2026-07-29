# UI

**Scripts:** `MainMenu/{MainMenuManager, InputModeManager}.cs`,
`ChooseStage/{MapMover, LevelNode, LevelInfoPopup}.cs`, `Game/UI/GameHUD.cs`

**Every UI element is authored in the scene or in a prefab — never built in code.** No
`new GameObject()`, no `AddComponent<Image>()`, no `Instantiate` for UI. The only exception is a
runtime copy of a prefab that already exists as a project asset, and even then that prefab is
authored, not generated. Lists use layout groups; alignment uses anchors and pivots, never pixel
offsets that die on an aspect-ratio change.

## Main menu

**Scene:** `Assets/Scenes/Menu/MainMenu.unity`

The lobby opens on a join prompt. The first successful join reveals the menu and eases the camera
from the start view to the lineup view. Coming back with persisted players skips the intro and
rebuilds the lineup immediately.

Joined characters are arranged in a centred row, frozen with kinematic rigidbodies, coloured by
player index, and paired with one `PlayerUI.prefab` card each. A shared join card lists only the
keyboard halves and gamepads still available. Starting with zero players is rejected with the
wrong-action SFX. The Options handler is still a placeholder.

`InputModeManager` switches between pointer mode (mouse movement and clicks, nothing selected)
and navigation mode (gamepad activity, a default button selected). Keyboard players use the mouse
for menu UI; gamepad players navigate. The `LastJoinFrame` guard stops a join press from
immediately activating whatever button is selected.

Start unfreezes persisted players and calls `SceneLoader.LoadStageSelect`. Device ownership is in
[player](mechanics/player.md#joining).

## Stage select

**Scene:** `Assets/Scenes/Menu/ChooseStage.unity`

An airplane token moves over `LevelNode` instances. Parking inside a node's detection radius
shows its card; Confirm loads an unlocked level, Back returns to the lobby.

`MapMover` reads `Map/Move`, `Map/Confirm`, and `Map/Back` from the shared input asset. Input is
shared, so any joined device can drive the token. Movement is constant-speed and snaps to face
direction. Persisted player GameObjects are deactivated while the map is open and reactivated
before either transition, so the target scene's `PlayerSpawner` can position them.

Each `LevelNode` references one `LevelConfig` — display strings, scene name, default unlock, save
key, and next-stage relationship all come from that asset. The node asks `ProgressionService` for
unlocked state and best stars, and owns only its locked/unlocked visuals, refreshing them on
`ProgressChanged`.

Confirming a locked node plays the wrong-action SFX; confirming an invalid config logs an error.
Loading is delegated to `SceneLoader` and guarded against double submission.

One reusable `LevelInfoPopup` card follows the currently detected node. It fills title and
description from the config, slides and fades in, and hides when the token leaves. Unlocked cards
show the description and best saved stars; locked cards append a locked label and explain that
the previous stage must be completed. Re-showing the same node is ignored so the animation
doesn't restart every frame.

> Because every stage config currently names a deleted scene, confirming a stage node will fail
> the `SceneLoader` Build Settings check. See [levels](levels.md#dangling-configs).

## Game HUD

**Prefab:** `Assets/Prefab/UI/GameHUD.prefab`, nested inside `GameManager.prefab`

`GameHUD` is presentation only. Every panel, text, image, and button reference is wired once
inside the prefab; it subscribes to the scene `GameManager` and stores no level rules.

| `GameManager` event | HUD response |
|---|---|
| `ScoreChanged` | update score text |
| `TimeChanged` | update `m:ss` timer |
| `CountdownChanged` | dim and show 3–2–1 |
| `RoundStarted` | reveal and animate timer and score |
| `PauseChanged` | show pause panel, select Resume |
| `RoundEnded(GameResult)` | play the result sequence |

The result sequence runs on unscaled time, because the round ends with `Time.timeScale = 0`. It
shows real total and per-player delivery counts from `GameResult`, reveals earned stars, stamps
approval, and selects Next Stage. P1–P4 rows are all prefab-wired.

Buttons call the `SceneLoader` API. Next uses `GameManager.Config.nextLevel` when present and
otherwise returns to stage select.

To redesign the HUD, edit or variant `GameHUD.prefab`. Don't push its child references back onto
`GameManager` or wire each level scene separately.
