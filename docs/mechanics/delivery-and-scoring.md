# Delivery and scoring

**Scripts:** `Game/World/Gate.cs`, `Game/Core/GameManager.cs`,
`Game/Scoring/{ScoreBoard, ScoringRules, RoundScoreContext, GameResult}.cs`
**Prefab:** `Prefab/Decoration/Gate`

Delivering a bag through a gate is the only way to score. Everything below is deterministic plain
C# once the gate has resolved the bag.

## Gates

```text
trigger collider
  -> Luggage.TryGetFromCollider
  -> reject an already delivered item
  -> read LevelContext.CurrentConfig
  -> ScoringRules.Resolve(...)
  -> RoundScoreContext.TryRecordDelivery(last player, delta)
  -> mark delivered and recycle
```

The score is recorded *before* `IsDelivered` is set, so a missing round context can't silently
eat a bag. Attribution uses the persistent `lastGrabber`, which is what credits a thrown
delivery to the thrower.

**Multiple gates.** Each gate has a per-instance `gateNumber`. With one gate, luggage gets no
destination and that gate accepts everything. With two or more, the spawner assigns each bag a
random active gate number and the timer UI displays it. Duplicate numbers produce a warning, and
the validator treats uniqueness as an error — destination routing depends on it.

Gate number is a correct scene override: it identifies that placement. Never apply a configured
level instance's number back onto the prefab.

## Scoring priority

`ScoringRules.Resolve` takes an immutable `LuggageScoreState`, the level config, and the gate
number:

| Priority | Condition | Config value |
|---:|---|---|
| 1 | has a destination and this isn't it | `scoreWrongGateDelivery` |
| 2 | required wash or wrap missing | `scoreMissingProcess` |
| 3 | otherwise | `scoreCorrectDelivery` |

> **That priority order is not enforced by anything.** It lives only in the order of the `if`
> statements, and there are no tests in this project. Reordering them silently changes which
> penalty a player receives, and playtesting will not catch it — the game just feels unfair.
> Re-check the numbers by hand whenever you touch this file.

## ScoreBoard

`ScoreBoard` has no `MonoBehaviour`, scene, or UI dependency. It tracks team score (clamped at
zero), per-player scores (which keep signed deltas), and real team and per-player delivery
counts. `ApplyScore` handles non-delivery deltas such as expiry; `RecordDelivery` increments the
real count and then applies the resolved score. Delivery counts are tracked directly, never
inferred from `score / 10`.

`RoundScoreContext` binds the active board for the round — gates call `TryRecordDelivery`,
luggage expiry calls `TryApplyScore`. It unbinds when the round ends so a late object can't
mutate a finished result.

## Round flow

```text
resolve LevelConfig -> reset ScoreBoard -> publish initial time
  -> 3/2/1 countdown on unscaled time (timeScale 0)
  -> RoundStarted (timeScale 1, SFX + music)
  -> timer and scoring
  -> EndRound at zero (timeScale 0, Time's Up SFX, music fades out over 1.25 s)
  -> GameResult -> ProgressionService + RoundEnded
```

`GameManager` owns round state and input, and holds no HUD or ambient-decoration references. The
shared `System/Pause` action toggles pause only after start and before end. Scene transitions go
through `SceneLoader`, which restores `Time.timeScale` after a load.

## Events and result

`GameManager` publishes score, time, countdown, round start, pause, and round end.
`ScoreBoard` owns its finer-grained score and delivery events. `GameHUD` consumes only the
presentation-facing ones — presenters listen to gameplay state and never own rules, so visuals
can be swapped without touching a mechanic.

`GameResult` carries level ID, score, deliveries, per-player score and deliveries, and earned
stars. `ProgressionService` records it; see [services](../services.md#save-and-progression).
