# Game HUD

**Script:** `Game/UI/GameHUD.cs` — **Prefab:** `Assets/Prefab/UI/GameHUD.prefab`

`GameHUD` is presentation only. All panel, text, image, and button references are wired once inside its prefab; it subscribes to the scene `GameManager` instead of storing level rules.

## Event bindings

| `GameManager` event | HUD response |
|---|---|
| `ScoreChanged` | update score text |
| `TimeChanged` | update `m:ss` timer |
| `CountdownChanged` | dim and show 3–2–1 |
| `RoundStarted` | reveal and animate timer/score |
| `PauseChanged` | show pause panel and select Resume |
| `RoundEnded(GameResult)` | play result sequence |

The result sequence uses unscaled time because the round ends with `Time.timeScale = 0`. It displays real total/per-player delivery counts from `GameResult`, reveals earned stars, stamps approval, and selects Next Stage. P1–P4 rows are all prefab-wired.

Buttons call the `SceneLoader` API. Next uses `GameManager.Config.nextLevel` when present and otherwise returns to stage select.

To replace the HUD design, edit or variant `GameHUD.prefab`; do not add its child references back onto `GameManager` or wire every level scene separately.
