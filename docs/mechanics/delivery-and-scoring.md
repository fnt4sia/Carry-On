# Delivery and scoring

**Scripts:** `Game/World/{Gate, DispatchGate, DispatchButton}.cs`,
`Game/UI/{GateManifestDisplay, FlightOrderScreen}.cs`, `Game/Core/GameManager.cs`,
`Game/Scoring/{ScoreBoard, RoundScoreContext, GameResult}.cs`
**Prefabs:** `Prefab/Decoration/Gate` (gate + hallway art, the instant-delivery `Gate` with its
`Gate Point` trigger and `Gate Barrier`), `Gate Cylinder` (the round glass booth and its sign board
from `Model/Environment/Individual Assets/stagedoor.fbx`, a `DispatchGate`) and
`Gate Manifest Signage` (its order screen). `Gate.prefab`'s `palette` still lists Blue, so scenes
using it override it. `Gate Cylinder` defaults to the live pool, Red / Green / Yellow

**Two kinds of gate, never in one scene.** Level2 and the main menu run `Gate`: a bag scores the
moment it enters, and a wrong one is bounced back out. The new Level1 runs `DispatchGate`: bags are
loaded into a booth and a button sends the whole flight
([below](#dispatch-gates-level1)).

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
  -> FindOpenLine(luggage)                // colour + wrapped must match; null => bounced
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

**Rejection, not punishment.** A bag the flight doesn't want — wrong colour, or wrapped when the
line wants plain (or the reverse) — is not scored and not destroyed. `RejectLuggage` drops whoever was holding it and
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

`GateManifestDisplay` draws the current flight on a world-space canvas floating above a `Gate`:
one slot per line, each a tinted swatch over a `delivered/required` count. (Level1's board was one
too until 19 Oct 2026; it is now a `FlightOrderScreen`, [below](#the-order-screen).)

`Gate Cylinder`'s `Model` has a `MeshCollider`, so bags bounce off the glass instead of leaking
into the drum. `Model` and the `Sign` above the entrance use `stagedoor.fbx`'s `Cylinder` and `Cube`
meshes (18 Oct 2026; the booth is the same size as the `level1.fbx` one it replaced). The FBX's third
mesh, `Plane.051`, is a raised walkway 100 m long — level floor art, so it stays out of the prefab.
Its old `Gate Point` trigger and solid `Gate Barrier` box (which is what kept bags out) were removed
when it became a dispatch gate.

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

## Dispatch gates (Level1)

The new Level1's booth (19 Oct 2026, Fitra: "like Crashout Crew"). One order at a time:

```text
order appears: 2–3 colours, at most 4 bags; timer = LevelConfig.orderTime (20 s)
  -> players carry bags into the booth; wrong ones are let in
  -> someone presses the dispatch button (Grab / E, empty-handed) — or the timer hits 0
  -> door rolls down (0.4 s) -> booth judged -> trapdoor opens, every bag in the booth sinks away
  -> paid only if the booth held exactly the order and time hadn't run out
  -> door rolls up on the next order (about 3 s for the whole dispatch)
```

**In the booth** means a bag nobody is holding, within `holdRadius` (6 m) of `Hold Centre` and below
`holdHeight` (7 m), both world units. `ScanBooth` recounts every `FixedUpdate` with an `OverlapBox`,
so a bag carried back out comes off the count. Each line shows `InBooth/Required`; a bag no line
wants (wrong colour, or wrapped — Level1 has no wrapper) counts as `WrongBags`.

**Exact or nothing.** A good flight has every line at exactly its count and no wrong bag. Anything
else fails: a spare bag, a wrong one, a missing one, or the timer running out. The flight still
leaves and the bags still drop, but it pays nothing and the streak resets. Nothing is bounced, and
the button works at any time.

**Coins** (`PayOut`):

```text
coins = round((scoreCorrectDelivery × bags + coinsPerSecondLeft × whole seconds left)
              × comboMultipliers[streak − 1])
```

Level1 pays 10 per bag and 1 per second, at ×1 / ×1.5 / ×2 for the 1st / 2nd / 3rd+ good flight in
a row (the last entry holds). Each bag's share (10 × multiplier) is a delivery for whoever last
carried it. The rest (time bonus plus rounding) goes to whoever pressed, through
`RoundScoreContext.TryApplyScore`. Checked in Play: 2 red + 1 yellow + 1 green pressed with 16 s
left paid 56; the next flight, 3 bags with 18 s left at ×1.5, paid 72; one spare green on the third
paid 0 and reset the streak.

**Door and trapdoor** are children of the `Gate Cylinder` prefab. `Door` is a pivot at the top of
the 65° entrance gap with a `Panel` cube hanging below; the gate scales its Y from 0.001 (rolled
up, as authored) to 1. The panel's collider excludes the Player and GrabbedLuggage layers, so it
stops loose bags but can never shut a player inside it. `Trapdoor` is a black unlit disc
(`Material/World/Trapdoor Hole.mat`) on the booth floor, authored inactive, opened like an iris.
The bags don't fall through physics: they turn kinematic, sink under gravity for `dropTime`, and go
back to the pool, which turns physics back on.

**The button** (`Dispatch Button`, right of the entrance as you face it) is a white pedestal with a
cap: green when pressing would pay, amber otherwise, grey during a dispatch. A 1.6 m trigger sphere
gives PlayerGrab's reach check something to find. `PlayerGrab` presses it only empty-handed, before
it checks machines, so a player carrying a bag beside it can still drop or throw.

**The booth's roof hides its far side** from Level1's 42° camera: a bag in the west ~40% of the booth
sits under the roof. The camera-facing half shows through the glass.

### The order screen

`Gate Manifest Signage/Order Screen` is a `FlightOrderScreen`: world space, on the `WorldUI` layer,
6.4 × 3.4 m. An orange bezel frames a navy screen showing:

- **Top row:** `GATE 1`, and the combo ("COMBO x1.5", only on a streak).
- **One slot per colour:** the `UI/Gameplay/ListItem` bag sprite over `in booth/needed` — white,
  green when exact, red when over.
- **Warning:** `WRONG BAG INSIDE!` while the booth holds a bag nobody asked for.
- **Timer bar** along the bottom: a Filled image that empties toward the left, shading green →
  amber → red.

During a dispatch it swaps to the verdict: the `Approved Stamp` sprite and `+coins`, or a red
`MISSED`. Three slots, one per palette colour. Its `gate` is a scene override; wire it to the gate
it stands beside.

## Scoring

A gate only ever applies `LevelConfig.scoreCorrectDelivery`. There is no wrong-gate penalty and
no missing-process penalty — both cases are bounced instead of charged, and
`ScoringRules`/`LuggageScoreState` were deleted with the flight rewrite. The expiry penalty went
with bag lifetime in September 2026, so **nothing in the game subtracts points**.

> **Scoring has no safety net.** There are no tests in this project, and a scoring bug is
> invisible during playtesting — the game just feels unfair. Re-check the numbers by hand
> whenever you touch this file, and re-check the star thresholds in the level's config: with
> penalties gone, a `Gate` scores simply `scoreCorrectDelivery x bags delivered`, and a
> `DispatchGate` adds the time bonus and combo above.

## ScoreBoard

`ScoreBoard` has no `MonoBehaviour`, scene, or UI dependency. It tracks team score (clamped at
zero), per-player scores (which keep signed deltas), and real team and per-player delivery
counts. `RecordDelivery` increments the real count and then applies the score through
`ApplyScore`. Delivery counts are tracked directly, never
inferred from `score / 10`.

`RoundScoreContext` binds the active board for the round — gates call `TryRecordDelivery`, the
only way score changes. It unbinds when the round ends so a late object can't
mutate a finished result.

## Round flow

```text
resolve LevelConfig -> reset ScoreBoard -> publish initial time
  -> 3/2/1 countdown on unscaled time (timeScale 0); "3" holds until the loading transition clears
  -> RoundStarted (timeScale 1, SFX + music)
  -> timer and scoring
  -> EndRound at zero (timeScale 0, Time's Up SFX, music fades out over 1.25 s)
  -> GameResult -> ProgressionService + RoundEnded
```

`GameManager` owns round state and input, and holds no HUD or ambient-decoration references. The
shared `System/Pause` action toggles pause only after start and before end. Scene transitions go
through `SceneLoader`, which resets `Time.timeScale` to 1 just *before* activating the new scene,
so the round's `StartRound` freezing time in `Start` sticks. Since the cloud transition (9 Oct
2026) keeps covering the level for about 1.4 s after it activates, the countdown publishes "3"
and then waits for `SceneLoader.IsLoading` to clear before timing anything — otherwise most of it
ran behind the clouds ([services](../services/scene-loading.md#the-cloud-transition)).

## Events and result

`GameManager` publishes score, time, countdown, round start, pause, and round end.
`ScoreBoard` owns its finer-grained score and delivery events. `GameHUD` consumes only the
presentation-facing ones — presenters listen to gameplay state and never own rules, so visuals
can be swapped without touching a mechanic.

`GameResult` carries level ID, score, deliveries, per-player score and deliveries, and earned
stars. `ProgressionService` records it; see [services](../services/save.md).
