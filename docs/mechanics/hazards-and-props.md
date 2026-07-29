# Hazards and props

**Scripts:** `Game/World/{PressurePlate, Gateway, RotatingPlatform, OneWayDoor, PoolHazard}.cs`

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
