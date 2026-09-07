# Delivery and scoring

**Scripts:** `Game/World/Gate.cs`, `Game/UI/GateManifestDisplay.cs`, `Game/Core/GameManager.cs`,
`Game/Scoring/{ScoreBoard, RoundScoreContext, GameResult}.cs`
**Prefab:** `Prefab/Decoration/Gate`

Delivering a bag onto a flight is the only way to score. Everything below is deterministic plain
C# once the gate has resolved the bag.

## Gates are flights

A gate is a **departing flight**. Its manifest lists how many bags of each colour the plane still
needs; players carry matching luggage into the trigger until every line is filled, at which point
the flight departs and `GenerateFlight` builds a fresh one in its place.

```text
trigger collider
  -> Luggage.TryGetFromCollider
  -> reject an already delivered item
  -> read LevelContext.CurrentConfig
  -> FindOpenLine(luggage.color)          // null => this flight doesn't want it
  -> IsProcessed(luggage)                 // unwashed / unwrapped => turned away
  -> RoundScoreContext.TryRecordDelivery(last player, scoreCorrectDelivery)
  -> count the line, mark delivered, recycle
  -> manifest full? CompleteFlight() -> GenerateFlight()
```

The score is recorded *before* `IsDelivered` is set, so a missing round context can't silently
eat a bag. Attribution uses the persistent `lastGrabber`, which is what credits a thrown
delivery to the thrower.

**Every line names a colour; "wrapped" is a second axis on top of it.** A plain line wants a
colour unprocessed ("3 red"); a wrapped line wants that same colour *after* a wrapper ("1 red,
wrapped"). A wrapped green bag will not fill a wrapped-red line. `ManifestLine.Accepts`:

```csharp
if (luggage.IsWrapped != NeedsWrapped) return false;      // exact, both ways
return luggage.color == Color;
```

The wrapped test is **exact in both directions** — a wrapped bag does not fill a plain colour line
either. Over-processing is as wrong as under-processing, which is what keeps wrapping a decision
rather than a free upgrade.

`wrappedLinesPerFlight` is a fixed count, not a chance: a level that teaches wrapping asks for it
every flight. Level1 leaves it at 0, Level2 sets it to 1.

Wrapped lines draw their colour from the full palette **independently** of the plain lines, so a
flight can legitimately want both "2 red" and "1 red wrapped" — two different jobs on the same
colour, which is what makes the wrapper worth queueing for.

**Rejection, not punishment.** A bag the flight doesn't want — wrong colour, or still filthy or
bursting — is not scored and not destroyed. `RejectLuggage` drops whoever was holding it and
throws it back the way it came (`rejectSpeed`, `rejectLift`). A wrong delivery costs the players
*time and mess*, never points.

### Flight generation

Serialized per gate instance:

| Field | Meaning |
|---|---|
| `palette` | colours this gate's flights may ask for |
| `minColorsPerFlight` / `maxColorsPerFlight` | how many different colours one flight wants |
| `minBagsPerColor` / `maxBagsPerColor` | how many bags of each colour it wants |
| `wrappedLinesPerFlight` | how many lines want wrapped bags of any colour |
| `minBagsPerWrappedLine` / `maxBagsPerWrappedLine` | how many wrapped bags such a line wants |

Colours are drawn **without replacement**, so one flight never lists the same colour twice, and
the count is clamped to the palette size.

> Every colour in `palette` must also be spawnable by the level's `luggagePrefabs`, or a flight
> that asks for it can never be filled. Nothing enforces this — check it by hand.

`gateNumber` still identifies a placement and stays a correct scene override. Never apply a
configured level instance's number back onto the prefab.

### The manifest board

`GateManifestDisplay` draws the current flight on a world-space canvas above the gate: one slot
per line, each a tinted swatch over a `delivered/required` count.

A wrapped line shows its colour swatch *plus* `wrappedBadge` — a cross-hatch sprite laid over the
swatch at 70% alpha, so the colour still reads through and the slot says "this colour, under
plastic". The badge is a per-slot scene object; a level with no wrapped lines simply leaves the
reference empty. It subscribes to
`Gate.ManifestChanged` and redraws — it never polls.

**Every slot is authored in the scene**, per the UI rule in `CLAUDE.md`. The component only shows,
hides and fills them, so a flight with fewer colours than there are slots leaves the spares
hidden. Author more slots than `maxColorsPerFlight` or you get a warning and a truncated board.

The board sits on the `WorldUI` layer, so the overlay camera draws it over the scene geometry
rather than letting the gate art occlude it.

## Scoring

A gate only ever applies `LevelConfig.scoreCorrectDelivery`. There is no wrong-gate penalty and
no missing-process penalty at the gate any more — both cases are bounced instead of charged, and
`ScoringRules`/`LuggageScoreState` were deleted with the flight rewrite.

`scoreTimerExpired` still exists and still fires, but only in levels that set a non-zero
`luggageLifetime`.

> **Scoring has no safety net.** There are no tests in this project, and a scoring bug is
> invisible during playtesting — the game just feels unfair. Re-check the numbers by hand
> whenever you touch this file, and re-check the star thresholds in the level's config: with
> penalties gone, score is now simply `scoreCorrectDelivery x bags delivered`.

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
