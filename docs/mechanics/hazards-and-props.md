# Hazards and props

**Scripts:** `Game/World/{PressurePlate, Gateway, RotatingPlatform, OneWayDoor, PoolHazard,
WindSway, MarqueeText, SignFlicker, AmbientTrafficSpawner}.cs`,
`Game/Core/AmbientAirplaneSpawner.cs`

Reusable logic prefabs. Shared behaviour (colliders, visuals, animation) belongs in Prefab Mode;
what each instance is *connected to* is per-level scene data.

## Pressure plate

One `PressurePlate` drives every "step here to actuate something" prop. The plate owns its
connections: a `connectedGateways` list and a `connectedPlatforms` list, both multi-target. The
targets never point back, so wiring lives in exactly one place — the plate.

On press (first valid contact) it toggles or opens its gateways and reverses its platforms. On
release (last contact leaves) it closes gateways in momentary mode; toggle mode keeps the door
state. It counts valid contacts, so a multi-collider object can't release it early.
`isToggleMode` applies to gateways only — platforms just reverse on each press. Player and
luggage filters decide what can trigger it. Tinting uses a `MaterialPropertyBlock`, so no
per-instance material is cloned.

Connection lists are per-level wiring and must stay scene-instance overrides. **Never use Apply
All from a configured instance** — that pushes one level's wiring into the prefab.

## Gateway

The sliding double door. `Gateway` exposes `Open`, `Close`, and `Toggle`, driving the cached
`AnimId.IsOpen` animator parameter. Normally driven by a `PressurePlate`. `Gateway_Open` /
`Gateway_Closed` are constant-pose clips that slide `MainLeftDoor` / `MainRightDoor` along local
Z; the transition blend is the slide.

**`MainDoorBody` must use a `MeshCollider`, not a `BoxCollider`.** The frame mesh is a portal
with an opening through it, so an auto-fitted box collider spans the whole bounding volume and
bricks up the doorway — the door then reads as permanently shut no matter what the leaves do.
Only the two leaves should carry box colliders.

## Rotating platform

Rotates its body in `FixedUpdate` and carries player and luggage riders around its pivot.
`carryRiders` enables positional transport; `rotateRiders` also rotates rider orientation. It
exposes `ReverseDirection` / `SetClockwise` for a plate to call — the plate owns that connection,
so the platform keeps no reference back. Rider identity uses the relevant component, with tags
only as an optional fast path.

**Riders are re-queried, not tracked.** Every `FixedUpdate` the rider set is rebuilt from a
`Physics.OverlapBox` of the rider zone. It used to count `OnTriggerEnter`/`Exit` pairs, which
drifts: any missed exit — a collider disabled mid-ride, a teleport, a grab that swaps colliders —
left a rider registered forever. Because the carry maths is `pivot + delta * (riderPos - pivot)`,
a ghost rider gets flung harder the further away they walk, so the bug read as "the platform
throws me across the map while I'm nowhere near it". An overlap query cannot drift.

Stepping off calls `ReleaseRider`, which subtracts the carry velocity the last step imparted.
`MovePosition` bakes the carry motion into a dynamic rider's velocity, so without this the
player keeps the platform's speed as a shove when they walk off. The correction only ever slows
a rider — never speeds one up.

**Speed is a tip-speed problem, not an rpm problem.** Tangential speed is
`rotationSpeed(deg) * Deg2Rad * radius`. The Pool Bridge beam is 44 units long, so radius 22:
at the old 35 °/s the ends moved at 13.5 u/s, faster than the player's 10 u/s walk, and riders
simply could not stand. It now runs at **10 °/s** — 3.9 u/s at the tip, 36 s per revolution.
Re-check this number whenever the beam's length changes.

**Placement.** The deck's top surface should sit flush with the surrounding floor so players can
walk on and off, with the floor under the sweep carved away. If the floor is left solid beneath
the beam, anyone standing in the beam's path is briefly a rider and gets dragged as it passes —
the rider zone is the deck footprint and cannot tell "on the deck" from "on the floor the deck
is sweeping through".

## One-way door

`Prefab/Environment/OneWayDoor.prefab` — a glass double door that only opens from one side.

The root carries the trigger, the `Animator`, and the script. The trigger box spans *both*
approaches; the one-way rule is the `IsOnAllowedSide` dot product, not the collider shape. A
player must stand at least `allowedSideCenterOffset` along `allowedEntryLocalDirection` (local
space) to be accepted — anyone walking up from the far side is ignored and the leaves stay shut
in their face. `OnDrawGizmosSelected` draws the allowed side green and the blocked side red, so
the facing is checked in the Scene view rather than guessed.

Accepted colliders are tracked in a set, so passing *through* the door keeps it open even once
the player crosses to the blocked half; the door closes `closeDelay` after the last one leaves
and re-entry cancels the pending close. `OnTriggerStay` re-tests anyone not yet accepted, which
catches a player already past the door plane on the frame they enter the trigger.

**Geometry.** Each leaf hangs off a `Hinge_Left` / `Hinge_Right` empty placed on the leaf's
*outer* edge — the imported mesh pivots sit near the middle of the doorway, where the handles
meet, so rotating the leaves directly would swing them about the wrong edge. `OneWayDoor.controller`
holds a single `IsOpen` bool over two constant-pose clips (`OneWayDoor_Closed`, `OneWayDoor_Open`
at ±90° hinge yaw); the 0.3s crossfade *is* the swing, so there is no keyframed motion to retime.
The leaves swing toward local −X, away from the allowed side. Leaf colliders ride the hinges, so a
closed door physically blocks the doorway.

When placing an instance, set `allowedEntryLocalDirection` along the **passage** axis (local X at
identity rotation), not across the doorway.

## Pool hazard

Water force-drops the player, disables it, waits the configured delay, then calls
`PlayerSpawner.MovePlayerToSpawn` and re-enables it. A fallback respawn point is used only when
no spawner exists. Put a `LuggageSink` beneath water and void areas so dropped bags are recycled
too — the hazard handles players, not cargo.

## Ambient airplanes

**Script:** `Game/Core/AmbientAirplaneSpawner.cs`

Pure set dressing — decorative traffic that flies a prefab from one anchor to another and owns
no round rules, so the lobby and gameplay scenes share one component.

Each cycle it waits `initialDelay`, then loops: wait a random `spawnDelayRange` cooldown, pick a
random entry from `airplanePrefabs` (null slots are skipped, so a half-filled array still works),
and fly it from `spawnPoint` to `destinationPoint` at a random `speedRange` speed. Up to
`maxConcurrent` flights overlap; each runs its own coroutine. Instances are pooled in one idle
stack **per prefab**, so a Pesawat1 is never reused as a Pesawat2.

`faceTravelDirection` turns the plane down the path with `LookRotation`; off keeps the spawn
anchor's rotation, which is what the GameManager copy relies on.

`requireActiveRound` is the one thing that differs between the two users:

| Where | Setting | Why |
|---|---|---|
| `GameManager.prefab` | **on** | planes only fly while a round is running |
| `MainMenu` → `AmbientPlanes` | **off** | the lobby has no `GameManager` at all |

Leaving it on in a menu is the failure mode to watch for — `RoundRunning` is false forever, so
nothing ever spawns and there is no error to tell you why.

### MainMenu setup

`AmbientPlanes` holds the component plus its two anchors. The path runs down `Runway2`
(centre `x 248.6`, 122.85 wide, 1212 long, top surface `y −2.94`):

| | |
|---|---|
| `PlaneSpawnPoint` | `(248.6, 1.24, 420)` |
| `PlaneExitPoint` | `(248.6, 1.24, −520)` |

`y 1.24` puts the wheels on the runway — `Airplane.prefab`'s pivot sits 4.18 above its lowest
point. Both anchors are off-screen (the view's edges are about `z +248` and `z −358`), so planes
enter and leave cleanly. 940 units at 70–130 u/s is a 7–13 s crossing.

Only `Airplane.prefab` is in the fleet. `Pesawat1` and `Pesawat2` are 4–5× larger at scale 1
(262 and 169 units long against Airplane's 53, and Pesawat1's 206-unit wingspan is wider than the
runway) — they need scaled prefab variants before they can be mixed in.

## Wind sway

**Script:** `Game/World/WindSway.cs` · **Prefab:** `Assets/Prefab/Decoration/HangingPlants2.prefab`

Ambient wind for hanging decoration. Composes a swing *onto* the transform's authored
`localRotation` — captured once at `Awake`, never replaced — so an FBX's imported orientation
survives. The swing axes are taken in **parent space**, which is why the plant reads as a
pendulum even though its own mesh is tilted 75°.

| Field | On the prefab | Does |
|---|---|---|
| `swayAngle` | 3 | swing along the wind, in degrees. True peak is `swayAngle * (1 + gustStrength)` |
| `crossSwayAngle` | 1.2 | swing across the wind; keep it under `swayAngle` |
| `swaySpeed` | 0.35 | swings per second |
| `windDirection` | 90 | degrees around Y; `0` = wind along world `+Z` |
| `randomness` | 1 | `0` = every instance in lockstep; `1` = own phase and own rate (±35%) |
| `gustStrength` | 0.6 | how much slow noise swells and stills the swing; `0` = a metronome |
| `gustSpeed` | 0.12 | gusts per second |

Desync is seeded from `GetInstanceID()`, so a row of identical prefabs never swings as one
block; the global `Random.state` is borrowed and restored rather than left reseeded. The cross
swing runs at `0.63×` the main rate on purpose — the two never resync, so the pot traces a
wandering ellipse instead of a flat back-and-forth.

### The pivot is the whole trick

`WindSway` swings about its **own transform origin**, so where that origin sits decides what the
motion looks like. `HangingPlants2.prefab` is therefore rigged in two parts:

```text
HangingPlants2      root at the top of the rope, identity rotation, scale 1   <- WindSway
  Model             localPosition (0, -5.00, -1.34), rotation (75, 180, 0), scale 2.74
```

The FBX's own pivot sits at mesh-local `z 0`, which is exactly where the rope meets the pot —
put `WindSway` there and the rope's *mounting point* sweeps a wider arc than the pot does, so the
plant looks like it is detaching from the wall. The rope runs from mesh-local `z −1.888` to
`−0.39` (cross-section radius ~0.09) and the pot occupies `−0.39 … 0.686` (radius ~0.66), so the
top of the rope is `(0, +5.00, +1.34)` above the FBX pivot. The root is placed there and `Model`
carries the offset back, which leaves the rendered rest pose bit-for-bit identical while pinning
the mount: at the peak of a 4.8° swing the rope top moves **0.000** units and the pot moves
**0.570**.

`Model` keeps the authored `(75, 180, 0)` / `2.74` from the FBX untouched — the root is what
rotates. Because the root is identity, `WindSway`'s parent-space axes are world axes, which is
what makes `windDirection` mean what it says.

The longer lever arm is why the prefab runs at `swayAngle 3` rather than the script's default 5:
the pot now hangs ~6.8 units under the anchor instead of ~1.8, so the same angle throws it about
3.5× further.

`MainMenu` has four instances along the interior wall, anchors at `y 11.25`, `z 116.28`, with
`windDirection 90` so the swing is sideways along the wall, the most visible axis from the fixed
menu camera.

## Signage text

**Scripts:** `Game/World/{MarqueeText, SignFlicker}.cs` ·
**Prefabs:** `Assets/Prefab/Decoration/{WallSignage, WallSignage2, StandingSignage2}.prefab`

Three signage meshes carry a world-space canvas as a **child of the prefab**, so the text is
authored once and every instance gets it. Nothing is built in code at runtime — the scripts only
move and fade what is already there.

All the canvases share one setup, matching `MenuBanner/MenuCanvas`:

| | |
|---|---|
| localRotation | `(0, 90, 0)` — puts canvas `+Z` into the sign, so `+X` reads screen-right |
| localScale | `0.001`, so `sizeDelta` is in thousandths of a sign-local unit |
| Canvas | World Space, `worldCamera` empty, **no `GraphicRaycaster`** — decoration must not swallow clicks |
| CanvasScaler | ConstantPixelSize, `dynamicPixelsPerUnit 4` |
| Layer | `UI` (5) |
| Font | `LiberationSans SDF`, same as the board rows |

> The font atlas is **Static / ASCII-only**. `·`, `—`, arrows and other non-ASCII glyphs render
> as missing-character boxes. Keep signage copy to plain ASCII.

Every sign's visible face is local **−X**. These rects were measured off the meshes'
front-facing vertices, not guessed:

| Prefab | Canvas | localPosition | sizeDelta | Content |
|---|---|---|---|---|
| `WallSignage` | `SignText` | `(-0.020, -0.064, -0.4745)` | `1862 × 386` | ticker, font 170 |
| `StandingSignage2` | `SignText` | `(-0.020, -0.0455, -0.435)` | `1862 × 385` | ticker, font 210 |
| `WallSignage2` | `SignTextUpper` | `(-0.100, 0, 0.279)` | `940 × 237` | `BAGGAGE CLAIM`, font 105 |
| `WallSignage2` | `SignTextLower` | `(-0.100, -0.4995, 0.279)` | `940 × 237` | `GATES 1 - 4`, font 105 |

`WallSignage` and `StandingSignage2` share the same board mesh, so their rects are nearly
identical. `x −0.020` sits in front of the flat panel but **behind** the header block that
protrudes to `x −0.054` — that is what makes the text look recessed into the sign instead of
pasted onto it. `WallSignage2`'s two rects sit inside the painted blue field of each plaque, left
of the baked arrow icon; that field is texture, not geometry, so those numbers came off a head-on
render rather than the mesh.

`MarqueeText` slides its label right-to-left across the viewport and wraps once the tail clears.
The viewport needs a `RectMask2D` or the text spills past the sign's edge, and the label needs
word-wrap off with a left-middle pivot so `preferredWidth` is its true length. `startDelay`
staggers signs so a row of them is not in step.

`SignFlicker` drives the canvas's `CanvasGroup` — mostly steady, with short random bursts of
dipping to `dimAlpha`, covering every graphic under the canvas at once. Keep `dimAlpha` above 0
so a sign never fully blanks.

Colours are deliberately low-contrast against the dark boards: amber `(0.72, 0.58, 0.36)` for
the tickers, pale steel `(0.58, 0.67, 0.76)` for the plaques. Same reasoning as the board rows'
bronze — the signs should sit in the environment, not glare out of it.

### Don't confuse these with the older prefabs

`StandingSignage.prefab` is a **different sign** — mesh `Signage03`, scale 2.9, facing `+90` — and
is not what `MainMenu` uses. The menu's standing sign was a loose FBX drop of `Signage03.001`, so
it became `StandingSignage2.prefab` rather than overwriting the existing asset. Same story for
`HangingPlants.prefab` (mesh `HangingPlants`, scale 1.75) versus the menu's
`HangingPlants2.prefab` (mesh `HangingPlants.001`, scale 2.74).

## Ambient traffic

**Script:** `Game/World/AmbientTrafficSpawner.cs` · **Scene object:** `MainMenu` → `AmbientTraffic`

The general version of [ambient airplanes](#ambient-airplanes): decorative vehicles that cross the
view, get pooled, and own no round rules. One component holds a list of **groups**, and each group
is one kind of traffic with its own fleet, endpoints and timing — so planes and cars are two
entries, not two scripts.

| Field | Does |
|---|---|
| `name` | inspector label only |
| `fleet` | prefab + `yawOffset` pairs, one picked at random per trip. **Empty means the group never spawns** |
| `sideOne` / `sideTwo` | the endpoints, split into two sides |
| `faceTravelDirection` | `LookRotation` down the path; off keeps the start anchor's rotation |
| `initialDelay` | quiet period before this group's first trip |
| `spawnDelayRange` | random cooldown between trips |
| `speedRange` | random world units per second per trip |
| `maxConcurrent` | how many of this group travel at once |

**A trip always crosses sides.** The start side is picked at random, then the destination is taken
from the *other* list — never within one side. Which point on each side is used is also random, so
three points per side give `3 × 3 × 2 = 18` distinct routes from one group.

Empty inspector slots are skipped everywhere (`HasAny` / `PickRandom`), so a half-filled array
still works and an all-null array simply yields nothing rather than throwing. A group with prefabs
but a missing side logs a warning once — that is a wiring mistake, unlike an empty fleet, which is
a legitimate off switch. Vehicles are pooled in one idle stack **per prefab**, so a bus is never
reused as a plane.

### Which way is the nose?

`LookRotation` puts an object's **+Z** down the path. Nothing in Unity knows where a model's nose
actually is, so a prefab authored facing any other axis travels backwards or sideways. That is what
`Vehicle.yawOffset` is for — it is folded in after the look rotation:

```csharp
Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(0f, yawOffset, 0f)
```

| Nose authored along | `yawOffset` |
|---|---|
| `+Z` | 0 |
| `+X` | 270 |
| `-X` | 90 |
| `-Z` | 180 |

**It is per prefab, not per group** — the two menu planes disagree with each other, so a
group-level setting could not express it.

There is no reliable way to detect this at runtime; it is measured once by eye. Drop the prefab in
at **identity rotation** and look at it from above and from the side — mesh bounds alone will not
tell you, because a nose and a tail-fin end look identical to a bounding box. Measured values for
the models currently in `MainMenu`:

| Prefab | Nose | `yawOffset` |
|---|---|---|
| `Pesawat1` | `+Z` | 0 |
| `Pesawat2` | `+X` | 270 |
| `BaggageTruck1` | `-X` | 90 |
| `Firetruck` | `-X` | 90 |
| `PassengerBus` | `-X` | 90 |

All three road vehicles checked so far are authored nose-along `-X`, so `90` is the likely starting
guess for others from that pack — but confirm it, since `Pesawat1` and `Pesawat2` prove one pack
can be inconsistent. A useful corroborating hint: if the artist gave the **prefab root** a non-zero
Y rotation, that value is usually the offset already (`Pesawat2.prefab` is authored at `(0, 270, 0)`).

With `faceTravelDirection` off, `yawOffset` is ignored entirely and the start anchor's rotation is
used instead.

### MainMenu setup

The anchors are placed and named to match the `1A 1B 1C` / `2A 2B 2C` convention:

| Group | Side one (deep end) | Side two (off right) | Trip | Speed |
|---|---|---|---|---|
| `Planes` | `1A/1B/1C` at `y 60`, 280–590 out, all off-frustum | `2A/2B/2C` at `y 60`, viewport `x 1.10` | ~571 u | 60–110 u/s (5–10 s) |
| `Cars` | `1A/1B/1C` on the apron at `y −3.8`, viewport `x 0.14` | `2A/2B/2C`, viewport `x 1.06` | ~764 u | 45–75 u/s (10–17 s) |

Two placement constraints, both learned the hard way:

- **The camera's far clip is 1000.** Anchors past that spawn a vehicle that is invisible until it
  flies into range. Every anchor above is inside 740.
- **Ground anchors must clear the terminal.** The glass wall sits at `x −24.7 … 24.9` spanning
  `z 30.9 … 330.9`, so a ground point with `x < 30` is *inside the building* unless `z > 345`. The
  car `1x` anchors sit at `x ≈ −80` and get away with it only because they are at `z 461 … 665`,
  out past the end of the wall.

Unlike the planes, the cars' deep end cannot be pushed fully off-frustum: on the ground the view's
left edge is asymptotic, so marching that way leaves the far clip before it leaves the screen. The
`1x` car anchors sit at viewport `x 0.14` instead — far enough to be small and behind the window
mullion. If a car ever reads as popping into existence, that anchor is the one to nudge.

> `MainMenu` still has the older `AmbientPlanes` object running `AmbientAirplaneSpawner` down the
> runway. The two do not conflict while `AmbientTraffic`'s plane fleet is empty, but once it is
> filled, pick one — otherwise two systems fly planes past the same window.
