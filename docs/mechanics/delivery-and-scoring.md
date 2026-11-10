# Delivery and scoring

**Scripts:** `Game/World/{Gate, DispatchGate, DispatchButton}.cs`,
`Game/UI/{GateManifestDisplay, FlightOrderScreen}.cs`, `Game/Core/GameManager.cs`,
`Game/Scoring/{ScoreBoard, RoundScoreContext, GameResult}.cs`
**Prefabs:** `Prefab/Decoration/Gate` (gate + hallway art, the instant-delivery `Gate` with its
`Gate Point` trigger and `Gate Barrier`), `Gate Cylinder` (the round glass booth, its sliding doors
and sign board from `Model/Environment/Individual Assets/Gate_FullAsset.fbx`, the dispatch button
from `LuggageButton.fbx`, a `DispatchGate`) and `Gate Manifest Signage` (its order screen). `Gate.prefab`'s `palette` still lists Blue, so scenes
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

`Gate Cylinder`'s `Model` has a `MeshCollider`, so bags bounce off the glass from outside. `Model`,
the `Sign` above the entrance and the two doors use `Gate_FullAsset.fbx`'s `Gate`, `GateSign`,
`LeftDoor` and `RightDoor.004` meshes (20 Oct 2026). It replaced `stagedoor.fbx` at the same size
and spot, and adds the doors. The renderers use the shared `Model/Level` materials, like the rest of
the level. The FBX has no floor piece, so the booth's floor is the level's own. Its old `Gate Point`
trigger and solid `Gate Barrier` box (which is what kept bags out) were removed when it became a
dispatch gate.

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
round starts (GO): the belt runs at once; the gate stays shut and its screen blank for
  firstOrderDelay (3 s), then its doors slide open on the first order
order appears: 2–3 colours, at most 4 bags; timer = LevelConfig.orderTime (35 s)
  -> players carry bags into the booth; anything is let in
  -> someone presses the dispatch button (Grab / E, empty-handed) — or the timer hits 0
  -> doors slide shut (0.6 s) -> booth judged -> the floor opens: every bag in the booth drops
     away, and so does any player standing in it (back at their spawn point 3 s later)
  -> paid if every line of the order was in the booth and time hadn't run out
  -> doors slide open on the next order (about 3.5 s for the whole dispatch)
```

The opening delay is scaled time. The countdown runs at time scale 0, so it only starts counting
at GO. The button is grey and dead until the first order (Fitra, 20 Oct: the gate gets a 3 s delay
"till it can be pressed and open a new flight").

**In the booth** means a bag whose *collider centre* is within `holdRadius` of `Hold Centre` and
below `holdHeight` (7 m), both world units. A bag a player is still carrying counts too, so a
player holding a bag inside when the floor opens loads it (it drops with them). `holdRadius` is
7.3 m, the glass's inner radius, so a bag pushed against the glass counts, and one on the
threshold counts once its centre is inside. Until 20 Oct 2026 it was 6 m measured to the bag's
pivot, and held bags were skipped. Bags are 1.9 m wide with the pivot 0.66 m off-centre, so one
lying against the glass sat outside the count, and the trapdoor left it behind (Fitra: "sometimes
even when the luggage is inside the gateway it isn't detected"). `ScanBooth` recounts every
`FixedUpdate` with an `OverlapBox`, so a bag carried back out comes off the count. Each line shows
`InBooth/Required`. A bag no line wants (wrong colour, or wrapped — Level1 has no wrapper) isn't
counted, but it still drops with the rest.

**Filled is enough.** A good flight has every line at or over its count. Spare and wrong-colour bags
don't matter (Fitra, 20 Oct: "doesn't even matter what are inside, as long as they fulfill the
thing"). Until then it was exact-or-nothing, with a `WRONG BAG INSIDE!` warning. A flight fails
only when a line is short or the timer runs out. The flight still leaves and the bags still drop,
but it pays nothing and the streak resets. Nothing is bounced, and the button works at any time.

**Coins** (`PayOut`):

```text
coins = round((scoreCorrectDelivery × bags the order lists + coinsPerSecondLeft × whole seconds left)
              × comboMultipliers[streak − 1])
```

Level1 pays 10 per bag and 1 per second, at ×1 / ×1.5 / ×2 for the 1st / 2nd / 3rd+ good flight in
a row (the last entry holds). Only the bags the order lists pay, so stuffing the booth with spares
earns nothing extra. Each ordered bag is a delivery worth 10 × multiplier. The rest (time bonus plus
rounding) is plain score through `RoundScoreContext.TryApplyScore`. The score is the team's; there
is no per-player tally. Checked in Play (before the spare rule changed): 2 red + 1 yellow + 1 green
pressed with 16 s left paid 56; the next flight, 3 bags with 18 s left at ×1.5, paid 72.

**Doors and hole** are children of the `Gate Cylinder` prefab. `Left Door` and `Right Door` are
pivots on the booth's axis, authored open: at rotation 0 the FBX's door meshes sit over the fixed
glass and leave the entrance clear. The gate turns them by `leftDoorShutAngle` (−28.5°) and
`rightDoorShutAngle` (33.7°), the closed pose they have in the FBX, so they slide round the drum
and meet in the doorway. Each door carries three `Edge` boxes over the stretch that ends up in the
doorway. Open, those boxes sit inside the wall ring; shut, they close the gap. Their colliders
exclude the Player and GrabbedLuggage layers, so they stop loose bags but can never shut a player
inside.

`Trapdoor` is the hole: a 64-sided black unlit disc (`Prefab/Decoration/Gate Cylinder_Hole.asset`,
`Material/World/Trapdoor Hole.mat`) 7.26 m in radius, just inside the glass. Open, it covers the
whole booth floor, so no floor shows around it. It is authored inactive and opens like an iris. The
first version was a 6 m disc, which left a ring of floor showing (Fitra: "we r still able to see
the floor"). Nothing falls through physics: bags and players turn kinematic and drop under gravity
for `dropTime` (the disc hides everything below the floor line, so they vanish into it). Bags then
go back to the pool, which turns physics back on.

**Players on the floor fall too** (Fitra: "including if there is a player inside it"). Whoever
stands within `holdRadius` when the floor opens drops their bag, falls with the rest, and is
deactivated for `playerRespawnDelay` (3 s), then reappears at their `PlayerSpawner` point. This is
PoolHazard's routine, with two fixes. An inactive `PlayerInput` reads `playerIndex` −1, so a player
moved to their spawn while still inactive went to spawn 0 (PoolHazard still does this). The gate
moves each player only once they're active again. Re-enabling also hands a player the lowest free
index, so a drop's players come back together, in index order, and each gets its own index back.
Checked in Play: both players fell, came back on their own spawns with their own index and
keyboard half. Players outlive the scene, so `OnDisable` re-activates anyone still hidden when the
level unloads.

**The button** (`Dispatch Button`, right of the entrance as you face it) is `LuggageButton.fbx`: an
orange pillar with a red cap, at the size and spot it has in the FBX's layout (3.4 m tall, chest
height on a player). The mesh is one piece, so it is split by palette colour into
`Prefab/Decoration/LuggageButton_Body.asset` and `LuggageButton_Cap.asset` (the cap is the red-UV
triangles). If the FBX is re-exported, re-split it. The cap uses `Dispatch Button Cap.mat` so it can
be tinted: green when pressing would pay, the model's red otherwise, grey during a dispatch. It
sinks 0.18 on a press. A 1.6 m trigger sphere (the old button's reach) gives PlayerGrab's reach
check something to find. `PlayerGrab` presses it only empty-handed, before it checks machines, so a
player carrying a bag beside it can still drop or throw.

**The booth's walls are a ring of boxes** (`Booth Walls`: 24 `BoxCollider`s, 1 m thick, inner face
7.3 m from the centre, 8.4 m tall, the 66° entrance left open for the doors' `Edge` boxes). The `Model`'s `MeshCollider` only
collides properly from one side. Its east glass faces outward, so a bag hitting it from *inside* was
pushed out along the face normal, straight through the glass (Fitra: hard throws "glitch to go
out"). In a test, 2 of 12 hard throws at 30 m/s escaped this way. CCD didn't help, because Discrete,
Speculative and ContinuousDynamic all escaped the same 2. With the ring: 0 of 16 through a wall and
0 of 7 thrown in from outside, and a bag glancing off the wall right beside the doorway can still
bounce out of the open entrance. Re-checked on the new model (20 Oct): 11 of 11 bags thrown at
30 m/s from inside stayed in and were counted; with the doors shut, a ring of 180 rays from the
centre hit a wall or an `Edge` box every time.

**The booth's roof hides its far side** from Level1's 42° camera: a bag in the west ~40% of the booth
sits under the roof. The camera-facing half shows through the glass.

### The order screen

`Gate Manifest Signage/Order Screen` is a `FlightOrderScreen`: the signage model's actual screen.
It is world space on the **`UI` layer**, so the main camera draws it with depth like any other
object. The gate roof hides part of it from some angles, and that is intended (Fitra: "let it be if
there is a part that's not visible"). The `WorldUI` overlay camera clears depth and drew it over
everything, so it read as floating. That layer stays for UI that should float, like player pins.

The `Model` (a monitor on a post) is 1.3× its FBX size. Its screen face is a flat plane yawed about
6° off the mesh's X axis, so the canvas was fitted to that plane, not to the model's rotation. The
face's normal is at yaw 108.76° in the prefab, so the canvas yaw is 288.76°. The canvas sits 0.02
in front of the face, at 1000 × 642 and 0.012695: 1% larger than the 12.57 × 8.08 face, so the
face's lilac never peeks out. Re-fit it if the model changes (the face is the lilac, front-facing
triangles). An orange bezel frames the screen, which is `#121633`, the main menu departures board's
own colour. Both now go through the same post-processing, so they render alike; the old `#0D1330`
read as black. Laid out top to bottom:

- **One slot per colour** (250 × 392): the `UI/Gameplay/ListItem` bag sprite (200 × 260) over
  `in booth/needed` (100 pt) — white until the line is filled, then green (over is fine).
- **Timer row:** a bar that empties toward the left, shading green → amber → red. On a streak,
  "COMBO x1.5" sits at its right end; the row's `HorizontalLayoutGroup` gives the bar the full
  width when the combo is hidden.

History (Fitra, 19–20 Oct 2026): the first board (6.4 × 3.4 m) was too compact to read. The second
(9.6 × 6.7 m, floating above the stand) was "literally too big". The third sat on the monitor but
was still drawn on top of everything, which made it look misaligned. The `GATE 1` header and the
stopwatch icon went with it, to give the bags and counts more room ("the line itself is pretty
self explanatory"). The `WRONG BAG INSIDE!` row went when spare and wrong bags stopped mattering.
Before the first order the screen is blank, with both groups hidden.

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

`ScoreBoard` has no `MonoBehaviour`, scene, or UI dependency. It tracks the team score (clamped at
zero) and the team's delivery count. `RecordDelivery` increments the count and then applies the
score through `ApplyScore`. Delivery counts are tracked directly, never inferred from
`score / 10`. There is no per-player score: it only fed the end card's player rows, and Fitra cut
both on 20 Oct 2026 ("no need to show that kind of information").

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

`GameResult` carries level ID, team score, deliveries, and earned stars. `ProgressionService`
records it; see [services](../services/save.md).
