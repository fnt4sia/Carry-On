# Round and scoring

**Scripts:** `Game/Core/GameManager.cs`; `Game/Scoring/ScoreBoard.cs`, `ScoringRules.cs`, `RoundScoreContext.cs`, `GameResult.cs`

`GameManager` owns round state and input. It has no HUD or ambient-decoration references. Presentation subscribes to events, deterministic scoring lives in plain C#, and level values come from `LevelContext`.

## Round flow

```text
resolve LevelConfig -> reset ScoreBoard -> publish initial time
  -> 3/2/1 countdown on unscaled time (timeScale 0)
  -> RoundStarted (timeScale 1, SFX/music)
  -> timer and scoring
  -> EndRound at zero (timeScale 0)
  -> GameResult -> ProgressionService + RoundEnded
```

The shared `System/Pause` action toggles pause only after start and before end. Pause events drive the HUD. Scene transitions use `SceneLoader`, which also restores `Time.timeScale` after a load.

## ScoreBoard

`ScoreBoard` has no `MonoBehaviour`, scene, or UI dependency. It tracks:

- team score, clamped to zero;
- per-player scores, which retain signed deltas;
- real team delivery count;
- real per-player delivery counts.

`ApplyScore` handles non-delivery deltas such as expiry. `RecordDelivery` increments real counts and then applies the resolved score. Events publish score/count changes. Results no longer infer deliveries from `score / 10`.

`RoundScoreContext` binds the active board for the round. Gates call `TryRecordDelivery`; luggage expiry calls `TryApplyScore`. The context is unbound when the round ends, preventing late objects from mutating a finished result.

## Scoring rules

`ScoringRules.Resolve` takes a small immutable luggage state, the level config, and gate number. Priority is wrong gate, missing process, then correct delivery. **That priority order is not enforced by anything** — it lives only in the order of the `if` statements. Reordering them silently changes which penalty a player receives, and playtesting will not catch it.

Player attribution comes from `Luggage.lastGrabber`, whose index comes from the actual `PlayerInput` component.

## Events and result

`GameManager` publishes score, time, countdown, round start, pause, and round end events. `ScoreBoard` owns its more detailed score/delivery events and state. `GameHUD` consumes the presentation-facing events. `GameResult` contains level ID, score, deliveries, per-player score/deliveries, and earned stars; `ProgressionService` records it.

See [Game HUD](../ui/game-hud.md), [delivery gates](../gameplay/delivery-gates.md), and [save and progression](save-and-progression.md).
