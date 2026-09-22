# Machine stations

**Scripts:** `Game/Stations/{MachineStation, Wrapper, StationLamps}.cs`,
`Game/UI/StationProgressDisplay.cs`
**Prefabs:** `WrapperStation.prefab` (live), `WashingStation.prefab` (model only — see below)

The pre-July-2026 `WashingMachine`/`WrapperMachine` prefabs, their controllers and their
door/slider clips were deleted in the same pass that shipped these; `Animation/Machine/` now holds
only `MachineShake`. Two old shells sit orphaned in `Model/Environment/Essential/`:
`AnimationWrapperMachine.fbx`, and `WashingWrapperAnim.fbx` (the previous art, retired when the
stations moved to `WashingWrapperModel01.fbx`). Nothing references either — but the shake curve
was *copied out of* `WashingWrapperAnim.fbx` before it was retired, so deleting it is safe while
re-deriving the curve from it is no longer possible.

The shell is 9.0 × 6.5 × 8.3.

Every station shares one explicit `Idle -> Processing -> Ready -> Idle` lifecycle.

| Station | Accepts | Completion |
|---|---|---|
| `Wrapper` | any bag not already wrapped | `MarkWrapped` — wraps in place, the bag keeps its colour |

A level needs a wrapper when any of its gates asks for wrapped lines
(`Gate.wrappedLinesPerFlight` > 0). Nothing checks this yet — see
[levels](../levels.md#authoring-a-level).

**`WashingStation.prefab` has no machine component.** Its `WashingMachine` script existed only to
wash Sticky luggage and was deleted with it in September 2026, taking the `StationLamps` that
required it. The prefab keeps the model, animator, curtains and every anchor child
(`snap`, intake, release, output), so a future washer is a new `MachineStation` subclass on the
root with those anchors dragged back in. Only the archived scenes place it.

> A `Scanner` station existed until 2026-07-14 and was removed with the bomb mechanic. Adding a
> third type later means subclassing `MachineStation` and overriding `CanAccept` plus the
> completion step — the lifecycle needs no changes.

## Placement and lifecycle

1. `PlayerGrab` finds a nearby station, checks `CanAccept`, and calls `TryPlace`.
2. The station clears its output area, force-drops the held bag, zeros its body, makes it
   kinematic, and snaps it to the station.
3. Phase becomes `Processing`; progress runs 0 → 1.
4. Completion runs the station operation and phase becomes `Ready`.
5. Output completion restores a dynamic rigidbody, resets the animator, and returns the station
   to `Idle`.

Docking and release are both `Luggage.SetInStation`. `PlayerGrab` and `Conveyor` both ignore a
docked bag — one flag, so don't add a second one.

An occupied station rejects further items. The bag follows `sliderTransform` in `LateUpdate`, so
animated doors and trays can move it safely.

## The shipping machines

Both stations are the same model from `Model/Environment/Essential/WashingWrapperModel01.fbx`: a
tall arch with **two curtained lanes**, fronted by a low open trough. Standing on the trough side
(local +X) looking at the arch, **the left lane is the entry and the right lane is the exit**.
The two domes on the arch roof are the indicator lamps.

```text
WashingStation                Animator; pivot = floor level, centre of the shell
├── Shake                     kinematic Rigidbody — the clip animates THIS node
│   ├── Model                 offsets the FBX-space meshes onto the pivot
│   │   ├── Machine           MeshFilter / MeshRenderer — the shell
│   │   └── TV                the arch-mounted display, new in Model01
│   ├── Collision             MeshCollider on the shell mesh
│   ├── EntryCurtain / ExitCurtain   4 × BaggageDoorCurtain each
│   └── Lamps                 EntryLamp / ExitLamp + a point light each
├── SnapPoint                 outer trough, where a placed bag lands
├── IntakePoint               entry lane, inside the arch
├── ReleasePoint              exit lane, inside the arch
└── OutputPoint               outer trough, where the finished bag is handed back
```

`Model01` also ships `Table` and `Washing_Button` / `Wrapper_Button` nodes. They are deliberately
**not** in the prefabs — they are loose decor, and the button sits on the table rather than on the
machine. Add them under `Model` at local zero if you want them; they will then shake with the
shell.

The shell's local extents, worth knowing before moving any anchor:

| local x | what is there |
|---|---|
| −7.34 … −4.5 | the arch — full height, roof at y 5.6–6.5. Curtains hang at x −4.86 |
| −4.5 … 1.19 | open lane, roofless |
| 1.19 … 1.67 | the end lip that closes the trough (washer 1.263, wrapper 1.191) |

Lanes are at `z = ∓1.8`. **The lane floor is continuous at y = 1.44 for the whole run**, arch
included — a downward raycast from all four anchors on both machines hits `Collision` at 1.44.
Don't try to confirm that by reading mesh vertices: the floor is a small number of large
polygons, so a vertex slice through a lane shows a gap where solid floor actually is. Raycast
instead.

That floor is what sets `y = 2.41865` on every anchor. A luggage rigidbody rests with its pivot
**0.979** above whatever it lands on (BoxCollider 4.0 tall at 0.8 scale, centre +0.776), and
1.44 + 0.979 = 2.419. The bag therefore sits flush rather than hovering or sinking, and it does
not fall when `AnimEvent_OnOutputComplete` hands it back to physics.

### Why the shake lives on a wrapper node

`Animation/Machine/MachineShake.anim` animates the local position of one node, `Shake`, so the
shell, its collider, both curtains, the TV and the lamps wobble as a single rigid unit. The z
curve was copied key-for-key out of the retired `WashingWrapperAnim.fbx`'s `Washing_MachineAnim`
clip and now lives entirely in our own asset. **`WashingWrapperModel01.fbx` contains no animation
at all** (`clipAnimations: []`) — which costs us nothing, precisely because the shake was never
driven by the model's own clips. One clip and one controller, `MachineShake.controller`
(`Idle` / `Shake`), serve both machines.

**Both ends of the curve are flattened to zero velocity.** The artist's version arrives at rest
still moving (in-tangent `-0.516`) and the state loops back to an out-tangent of `+0.665`, so the
machine kicked as it stopped and again on every loop. The first and last keys now have zero
tangents and the three keys before the last are smoothed, which eases the settle out over the
final ~0.15 s. Re-import the FBX curve and you re-import the kick.

Three things make this hold together, and all three are load-bearing:

- **`Shake` carries a kinematic `Rigidbody`** and the Animator runs in `UpdateMode.Fixed`. A
  non-convex `MeshCollider` may only move if it belongs to a kinematic body, and animating a
  collider outside `FixedUpdate` makes contacts jitter.
- **Every curtain `HingeJoint` has `connectedBody` pointing at that Rigidbody.** With the shipped
  `connectedBody = null` the panels anchor to *world* space, so a shaking parent would drag the
  panels while the joints hauled them back — the curtain would tear itself apart.
- That same joint link inherits `enableCollision = false`, which is what stops the panels
  grinding against the shell's own `MeshCollider`. The strips fill the opening exactly, so a
  0.5-unit wobble would otherwise bury the outer strip in the frame every cycle.

The anchor points deliberately sit **outside** `Shake`: the bag rides a steady path while the
machine rattles around it.

Both prefabs use the **same anchor values** — `WashingStation` is the reference and
`WrapperStation` copies it. Only `Model`/`Collision`'s offset differs, because each machine's mesh
sits at a different Z in the shared FBX (`z −8.42938` washer, `z +6.26045` wrapper; x and y match).

| anchor | local position |
|---|---|
| `SnapPoint` | `(0.2, 2.41865, -1.7995)` |
| `IntakePoint` | `(-6, 2.41865, -1.8)` |
| `ReleasePoint` | `(-6, 2.41865, 1.8)` |
| `OutputPoint` | `(0.2, 2.41865, 1.8)` |

All four are `rotY 90`. The x values are pinned to geometry, not taste. The bag's collider is
1.739 long in x, so a bag centred at `0.2` spans −0.67 … 1.07 and clears the end lip — by 0.19 on
the washer and 0.12 on the wrapper, which is the tighter of the two and therefore what sets the
number. The old art cleared its lip by 0.13, so this matches. And `-6` is the only band both
behind the curtain (x −4.86) and inside the back of the arch (x −7.34).

## Manual crank (the redesign's machine mechanic)

`requiresPlayerCrank` turns a machine Overcooked-style: loading a bag does **nothing** on its own.
The bag slides to `intakeTransform` and stops there until a player stands at the machine and holds
the grab button.

- `AddCrankProgress(deltaTime)` is called once per frame by `PlayerGrab` while the button is held.
- `processDuration` stops being wall-clock time and becomes **seconds of held button**.
- Letting go **pauses**. Progress is banked and never decays, so a second player can take over
  half-way through and the first player can wander off to fetch the next bag.
- At 100% the normal `OnProcessComplete` → eject path runs, shared with the timed path as
  `EjectSequence`.

`IsAwaitingCrank` is the "there is work here" flag — it drives both the progress bar and the
player's detection of a machine worth pressing. `CrankProgress01` is the 0..1 bar fill.

The machine animates **only on the frames someone is actually cranking**: `AddCrankProgress` sets
`crankedThisFrame` and `LateUpdate` feeds that straight to the animator. Nothing has to notice that
a player walked away, died, or was disconnected.

> The progress bar itself is `StationProgressDisplay` — a scene-authored world-space canvas that
> only fills and hides, per the UI rule. It shows up when a bag is waiting and disappears the
> moment the machine finishes, so an idle machine carries no UI at all.
>
> **Make it loud.** This is the one thing a player has to read across the arena while being
> shouted at. Level2's bars are 10 x 3 world units, centred over the machine footprint at y 7.6,
> built as white frame → dark track → bright fill so the bar reads as a bar even when nearly
> empty. A small flat bar disappears against the art — the first attempt was half this size and
> was reported as invisible.

## Animation-driven or timed

Production machines set `animationDriven` and call:

- `AnimEvent_OnDoorClosed` when processing completes;
- `AnimEvent_OnOutputComplete` when the output can return to physics.

Prototype machines turn that off (or omit an animator) and use `processDuration` plus
`readyDuration`. Art and animation can therefore be replaced without touching the gameplay state
machine. Animator parameters use cached `AnimId` hashes, including `AnimId.IsTriggered`.

The shipping stations run **timed** (`animationDriven` off) while still playing the shake, because
the new art has no tray for the bag to ride. `MachineStation` walks it through instead:

| Step | Anchor | Time | Shake |
|---|---|---|---|
| placed | `snapTransform` | — | off |
| slides in through the entry curtain | `intakeTransform` | `intakeDuration` 1.1 | off |
| shakes, then swaps prefab | — | `processDuration` 1.65 | **on** |
| cuts to the exit lane | `releaseTransform` | instant, hidden under the roof | off |
| slides out through the exit curtain | `outputTransform` | `readyDuration` 1.4 | off |

**`isTriggered` goes up when the bag is inside, not when it is placed.** `TryPlace` only raises it
for `animationDriven` machines, where the clip chain *is* the lifecycle. Timed machines raise it
in `AutomaticProcess` after the intake ride and drop it before the output ride, so the shell only
works while the bag is behind the curtain. The controller's 0.25 s Shake → Idle blend means the
machine settles rather than snapping still.

The bag is kinematic for the whole trip, so it shoves the curtain panels aside on the way through.
Leave `sliderTransform` empty on these prefabs — a machine uses either the slider follow or the
anchor walk, never both.

The two rides are now **the same distance** — 6.2 units each way, because `SnapPoint`/`OutputPoint`
share an x and so do `IntakePoint`/`ReleasePoint`. They are still not the same *speed*, only
because the durations differ: intake 6.2 u in 1.1 s (5.6 u/s) against output 6.2 u in 1.4 s
(4.4 u/s). Making the two durations equal now matches them exactly; under the pre-`Model01` art
the distances themselves were lopsided (11.39 vs 8.01) and no pair of durations could.

> The lane change is a hard cut, not a lerp. A lerp from the entry lane to the exit lane would
> clip straight through the divider between them.

## Indicator lamps

`StationLamps` recolours the two domes and their point lights from `MachineStation.Phase`:
entry green while `Idle`, exit green while `Ready`, red otherwise. Colour goes through a
`MaterialPropertyBlock` on the shared `Material/Machine/StationLamp.mat`, so no per-instance
material is created. The domes are authored on the prefab; the script only tints them.

## Curtains

Each opening is filled by **four `BaggageDoorCurtain` instances** (eight hinged panels), scaled
to the 3.0 × 4.0 opening and spaced so no two panels start overlapping. They hang just clear of
the lintel and the trough floor. The `Gorden1`/`Gorden2` meshes in the FBX are unused; the
physics curtain replaces them.

Every panel's `HingeJoint.connectedBody` is retargeted to the `Shake` Rigidbody — see
[above](#why-the-shake-lives-on-a-wrapper-node). If you rebuild the curtains, redo that link or
they will hang from world space and fight the shake.

### Why panels used to freeze open

A panel held wide by a passing bag stops moving, and PhysX puts its rigidbody to sleep. **A
`HingeJoint` spring cannot wake a sleeping body** — only a fresh collision can — so once the bag
left, the panel stayed frozen at whatever angle it was pinned at and left a visible hole. The
same thing happened part-way through a slow return, when the swing dipped under the sleep
threshold. Measured in `MainMenu`: 8 of 28 strips asleep at 77–81° after ~100 s of belt traffic;
`WakeUp()` restored every one of them, then they re-froze on the next bag.

`Game/World/BaggageDoorCurtain.cs` on the curtain root sets `sleepThreshold = 0` on every strip
below it (and wakes them in `OnEnable`, since re-enabling restores the sleeping state). The
strips are 0.5 kg boxes, so never sleeping costs nothing. `sleepThreshold` is a runtime-only
property — it cannot be authored on the prefab, which is why this needs a script at all.

> ### Never put Has Exit Time on the idle → open transition
>
> The controller's `Default State` is an empty state, so its `normalizedTime` climbs forever and
> crosses any given exit time exactly once — a few tenths of a second after the scene loads,
> while `isTriggered` is still false. After that the window never reopens and the machine is dead
> for the whole round no matter how many times `TryPlace` sets the bool.
>
> Both machine controllers shipped this way until 2026-07-26. The idle → open transition must be
> condition-only (`isTriggered == true`, Has Exit Time **off**). The rest of the chain is
> exit-time driven, which is fine because those states are entered fresh each cycle.

## Output clearance

Before placement, an overlap box finds other free luggage in the output zone and shoves it away.
Half extents, impulse, upward impulse, and randomness are serialized on the station prefab under
the `Output Clearance` header — the old `Station_Default.asset` went with `Assets/Config/Tuning/`
in July 2026. Snap and slider transforms, animator, and process timing are likewise per-prefab
references.

Edit `WashingStation.prefab` / `WrapperStation.prefab`. Never the scene copy.

## Feedback

Two things: the [lamps](#indicator-lamps), and on cranked machines the `StationProgressDisplay`
bar described under [manual crank](#manual-crank-the-redesigns-machine-mechanic). Anything added
later should read `MachineStation.Phase` or `CrankProgress01` the way those do, rather than touch
the state machine.
