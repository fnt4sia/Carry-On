# Hazards

**Scripts:** `Game/World/{PressurePlate, Lever, Gateway, SlidingPanel, Elevator, RotatingPlatform,
OneWayDoor, PoolHazard}.cs`

Reusable logic prefabs. Shared behaviour (colliders, visuals, animation) belongs in Prefab Mode;
what each instance is *connected to* is per-level scene data.

> **These are out of the gameplay loop on the `redesign` branch.** The plates and gateways were
> removed from `Level1` — a gate that idles one player while the other fetches turned out to be
> the boring half of the playtest. The scripts and prefabs are kept because they still describe
> working props, but no redesigned level wires them — the only scenes that do are in
> `Assets/Scenes/Archive/` ([levels](../scenes/levels.md#archived-scenes)). Don't add them back to a level
> without a reason that survives a playtest.

## Pressure plate
One `PressurePlate` drives every "step here to actuate something" prop. The plate owns its
connections — `connectedGateways`, `connectedPanels`, `connectedElevators` and
`connectedPlatforms`, all multi-target. The targets never point back, so wiring lives in exactly
one place — the plate.

On press (first valid contact) it toggles or opens its gateways, panels and elevators, and
reverses its platforms. On release (last contact leaves) it closes gateways, panels and elevators
in momentary mode; toggle mode keeps their state. It counts valid contacts, so a multi-collider
object can't release it early. `isToggleMode` does not reach platforms — they just reverse on
each press. Player and luggage filters decide what can trigger it. Tinting uses a
`MaterialPropertyBlock`, so no per-instance material is cloned.

Gateway, `SlidingPanel` and `Elevator` all expose the same `Open` / `Close` / `Toggle` trio, which
is what lets one plate drive three unrelated props with no special casing. They are still four
separate typed lists rather than one interface list: Unity cannot serialize an interface reference
in the inspector without falling back to `List<MonoBehaviour>` and casting, which costs the drag
-and-drop type safety the plate depends on.

Connection lists are per-level wiring and must stay scene-instance overrides. **Never use Apply
All from a configured instance** — that pushes one level's wiring into the prefab.

## Lever
**Script:** `Game/World/Lever.cs` — **Prefab:** `Prefab/Environment/Lever.prefab`

The pressure plate's manual twin: a player walks up and presses **UseStation** (P1 `F`,
P2 `L`, gamepad West) to flip it. Wiring is identical to the plate — the lever owns
`connectedGateways`, `connectedPanels`, `connectedElevators` and `connectedPlatforms`, the targets
never point back, and the lists stay scene-instance overrides. Every pull toggles gateways, panels
and elevators and reverses platforms; there is no momentary mode, because a lever has no
"released" state.

`Lever` has no detection of its own. `PlayerGrab.TryUseLever` reuses the same
`Physics.OverlapSphereNonAlloc(grabPoint.position, grabRadius, …)` sweep that finds a station,
so **a lever only needs a collider to be reachable** and its range is `grabRadius` on the
character prefab. One `UseStation` press covers both: `TryUseStation` runs first and returns
`false` when the player is empty-handed or no station accepts the bag, and only then does the
lever fire — so placing a bag in a machine can never also pull a lever standing next to it.

`E` is **not** available for this: it is already `Grab` on the `KeyboardLeft` scheme.

The prefab is a placeholder — a 0.5 × 1.8 × 0.5 cube with a solid `BoxCollider`, tinted red when
off and green when on through a `MaterialPropertyBlock`, exactly as the plate does. Swap the mesh
when the real model exists; nothing in the script reads the geometry.

## Gateway
The sliding double door. `Gateway` exposes `Open`, `Close`, and `Toggle`, driving the cached
`AnimId.IsOpen` animator parameter. Normally driven by a `PressurePlate`; in the archived
`Tutorial` it is driven by `TutorialRoom` instead, which opens a room's doors once its luggage is
cleared. Both drivers just call `Open` — the door never knows which.
`Gateway_Open` /
`Gateway_Closed` are constant-pose clips that slide `MainLeftDoor` / `MainRightDoor` along local
Z; the transition blend is the slide.

**`MainDoorBody` must use a `MeshCollider`, not a `BoxCollider`.** The frame mesh is a portal
with an opening through it, so an auto-fitted box collider spans the whole bounding volume and
bricks up the doorway — the door then reads as permanently shut no matter what the leaves do.
Only the two leaves should carry box colliders.

## Sliding panel
**Script:** `Game/World/SlidingPanel.cs` · **Prefab:** `Prefab/Environment/Sliding Glass.prefab`

A panel that slides aside when actuated and slides back when closed — the moving half of the
Sliding Glass prop.

| Field | On `Sliding Glass` | Does |
|---|---|---|
| `slidingBody` | empty | the transform that moves. Empty = this object |
| `slideDirection` | `Right` | the sliding body's **own** local axis, so it follows the prop's rotation |
| `slideDistance` | 3.05 | how far it travels, in the sliding body's local units |
| `slideSpeed` | 3 | local units per second. `0` snaps |
| `startOpen` | off | on = already slid aside at level start |

**The authored pose is always the closed pose.** `Awake` captures `localPosition` as closed and
derives open from it, then snaps to whichever `startOpen` asks for. A door that begins open is
still placed in the scene where it *blocks* — that is the pose the level designer can see and
align, and it is what the slide is measured from.

`Sliding Glass.prefab` is a holder with the existing `Glass Panel.prefab` nested under it:

```text
Sliding Glass      Rigidbody (kinematic), SlidingPanel      <- moves
  Glass Panel      nested prefab, scale (3, 3.5, 1)         <- mesh + collider
```

The root moves and the panel rides along, so the mesh and material stay a plain instance of
`Glass Panel` and any edit to that prefab still propagates. Scale the **child** to resize the
glass; `slideDistance` is measured in the root's parent space, so resizing never rescales the
travel.

The kinematic `Rigidbody` is the reason the panel *sweeps* players and luggage aside instead of
letting PhysX push them out of an interpenetration after the fact. `SlidingPanel` uses
`MovePosition` when it finds one and falls back to setting `localPosition` when it does not, so a
panel without a Rigidbody still works — it just shoves less cleanly.

## Elevator
**Script:** `Game/World/Elevator.cs` · **Prefab:** `Prefab/Environment/Elevator.prefab`

A cab that closes its door, travels to another height, then opens the door again. Driven by a
plate or a lever through the same `Open` / `Close` / `Toggle` trio Gateway uses: `Open` goes to the
raised floor, `Close` returns to the start floor.

| Field | On the prefab | Does |
|---|---|---|
| `liftBody` | empty | the transform that travels. Empty = this object |
| `travelHeight` | 6 | how far above the authored position the raised floor sits |
| `travelSpeed` | 3 | local units per second. `0` snaps |
| `doorPause` | 0.25 | beat held after the door shuts and again on arrival |
| `startRaised` | off | on = the cab starts at the raised floor |
| `door` | the `Door` child | optional. Without one the cab just travels |
| `doorTravelTime` | 0.35 | how long to wait for the door animation. Match the controller's transition |

### The door is a Gateway
The cab door is the **same `Gateway` component the level gates use** — a two-leaf sliding door
driven by an animator, not by `SlidingPanel`. `Elevator` never moves the leaves itself; it calls
`Close()`, waits `doorTravelTime`, travels, then calls `Open()`.

```text
Door           Animator (ElevatorDoor.controller), Gateway (isOpen = true)
  Door_Left    cube, closed (-1, 0, 0)  ->  open (-3, 0, 0)
  Door_Right   cube, closed ( 1, 0, 0)  ->  open ( 3, 0, 0)
```

`Assets/Animation/ElevatorDoor/` holds the controller and two constant-pose clips,
`ElevatorDoor_Closed` and `ElevatorDoor_Open`, exactly like `Gateway.controller`: one `IsOpen`
bool, no keyframed motion, and a **0.35s transition that *is* the slide**. To retime the door,
change the transition duration and `doorTravelTime` together — there is nothing else to retime.

Unlike the gates, the default state is **Open** and `IsOpen` defaults to `true`, because an
elevator idles with its door open. That is what stops the door playing a close-then-open blend the
moment the level loads.

The clips bind by path to `Door_Left` and `Door_Right`. **Renaming either leaf silently breaks the
animation** — the doors simply stop moving, with no error.

The rest of the prefab is placeholder geometry: cubes on the `Wall` layer, pivot at the centre of
the cab floor so the floor's top surface is exactly `y = 0`.

| Child | localPosition | localScale |
|---|---|---|
| `Floor` | `(0, -0.1, 0)` | `(4.4, 0.2, 4.4)` |
| `Ceiling` | `(0, 3.6, 0)` | `(4.4, 0.2, 4.4)` |
| `Wall_Back` | `(0, 1.75, 2.1)` | `(4.4, 3.5, 0.2)` |
| `Wall_Left` | `(-2.1, 1.75, 0)` | `(0.2, 3.5, 4)` |
| `Wall_Right` | `(2.1, 1.75, 0)` | `(0.2, 3.5, 4)` |
| `Door` | `(0, 1.75, -2.1)` | — (empty holder) |
| `Door/Door_Left` | `(-1, 0, 0)` | `(2, 3.5, 0.2)` |
| `Door/Door_Right` | `(1, 0, 0)` | `(2, 3.5, 0.2)` |

The entrance is local **−Z**, and the two leaves cover `x −2 … 2` when shut. Open, each has slid 2
outward and sits past the side walls — but at `z −2.1` they pass in *front* of those walls rather
than through them, so nothing interpenetrates. Follow the [Gateway](#gateway) rule and keep box
colliders on the leaves only.

The cab carries a kinematic `Rigidbody`; that is what carries riders. Measured over a 6-unit
ascent, a 5 kg box resting on the floor stayed exactly 0.500 above it, with no slip and no
re-parenting.

**The pivot is the floor's top surface**, so an instance placed at `y 0` sits flush with a floor at
`y 0`. Placing the cab any higher leaves a lip players cannot step over.

**Reversing mid-travel is supported.** `SetRaised` restarts the sequence, so a plate pressed while
the cab is climbing sends it back down from wherever it is — the door is already shut, so the
restarted routine skips straight to the travel leg.

## Rotating platform
`Prefab/Environment/Rotating Platform.prefab` — rebuilt from scratch in August 2026. Three
objects, no more:

```text
Rotating Platform      pivot; kinematic Rigidbody + the RotatingPlatform script; scale (1,1,1)
  Deck                 RotatablePlatform mesh + RotatingPlatform.mat + BoxCollider — 44 x 0.51 x 4.4
  Rider Zone           BoxCollider trigger, 44 x 1.5 x 4.4, sitting on the deck's top face
```

The deck's renderer and collider are the same box, so what you see is what you stand on. Bake
size into `Deck`'s localScale and leave the **root scale uniform** — the root is what spins, and
the old prefab's non-uniform `(1, 1, 1.4)` root made every dimension in the inspector a lie. The
version before this one carried a second, colliderless copy of the mesh scaled ~100x (a 3 km
slab) next to an invisible collider proxy whose renderer was switched off and whose material was
`water`; if something looks wrong here, check `MeshRenderer.enabled` before trusting bounds.

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

**One platform carries a rider, never two.** Ownership is global and sticky: `s_Owners` maps
each rider to the platform holding it, and a platform skips any rider another *active* platform
still lists. The holder keeps a rider until the rider leaves its zone, so crossing a seam is a
one-step handoff rather than a fight. Without this, two platforms placed to meet tip-to-tip both
carried the same rider in the same `FixedUpdate` and the two carries **added**. Measured on the
Level 2 diamond (pivots 16.04 apart, half-length sum 16.28, 25 °/s counter-rotating): a rider at
the seam moved at **6.997 u/s instead of 3.50** — 70% of walk speed, sideways — while the two
opposed rotations cancelled its yaw to **0.000°**, so the character slid without turning. The
double-claim window is **49° of phase**, about a second per crossing. Same-direction neighbours
would instead double the spin. Execution order between platforms is undefined, so this is not
something the level author can dodge by ordering the hierarchy.

**The player is carried by a transform write, luggage by `MovePosition`.** `PlayerMovement`
walks by assigning `transform.position` directly, and the project runs with **Auto Sync
Transforms off**, so `rigidbody.position` goes stale the instant it does. Carrying the player
through `MovePosition` read that stale origin and then lost the race with the transform write —
the platform rotated out from under the rider and they slid off. `RiderState.TransformDriven`
(set when the rider has a `PlayerMovement`) picks the matching path, so the platform's write and
the player's own write add up instead of cancelling. Luggage is genuinely physics-driven and
still uses `MovePosition`.

Stepping off calls `ReleaseRider`, which subtracts the carry velocity the last step imparted.
`MovePosition` bakes the carry motion into a dynamic rider's velocity, so without this the
rider keeps the platform's speed as a shove when they walk off. The correction only ever slows
a rider — never speeds one up. It is **skipped for transform-driven riders**: nothing was ever
pushed through their rigidbody, so there is no baked velocity to hand back and subtracting one
would be a shove of its own.

**Deck width is a hard constraint.** Annie's capsule radius is 0.726, so she is ~1.45 wide. The
deck must clear that with room to stand — the Pool Bridge is 4.34 wide. A deck narrower than the
player reads in game as "the platform throws me off", and no amount of carry maths fixes it.

**The deck must be frictionless, or riders move at double speed.** The deck carries
`BeltSurface.physicMaterial` (friction 0, combine **Minimum**) for the same reason a conveyor
deck does: the script owns the carry, so contact must not add a second one. `Player Physics` is
friction 0 but combines by **Average**, so a deck with *no* material lands on Unity's default 0.6
and the pair averages to **0.3** — enough for the rotating kinematic collider to physically drag
the rider around on top of the scripted carry. Measured on a 10.9 s ride at radius 15: with the
bare deck the player orbited at **19.6 °/s** against the platform's 10 °/s (ratio 1.96); with
`BeltSurface` it tracks at **10.0019 °/s** (ratio 1.0002) and holds its radius to five decimals.
The doubled carry also jitters the rider hard enough to pop them off a thin deck, which reads as
falling straight through it. Combine mode is the trap here — checking only `dynamicFriction: 0`
on the player's material tells you nothing about the pair.

**Speed is a tip-speed problem, not an rpm problem.** Tangential speed is
`rotationSpeed(deg) * Deg2Rad * radius`. The Pool Bridge beam is 44 units long, so radius 22:
at the old 35 °/s the ends moved at 13.5 u/s, faster than the player's 10 u/s walk, and riders
simply could not stand. It now runs at **10 °/s** — 3.9 u/s at the tip, 36 s per revolution.
Re-check this number whenever the beam's length changes.

**Placement.** Neighbouring platforms are *meant* to overlap slightly — the Level 2 diamond
spaces pivots at 16.04 against a 16.28 half-length sum, so the tips meet and you can cross. Do
not try to fix rider problems by pulling them apart; a gap leaves the rider uncarried mid-stride.
Ownership arbitration is what makes the seam safe.

The deck's top surface should sit flush with the surrounding floor so players can
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
