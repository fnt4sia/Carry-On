# World interactables and hazards

**Scripts:** `Game/World/Gateway.cs`, `PressurePlate.cs`, `RotatingPlatform.cs`, `OneWayDoor.cs`, `PoolHazard.cs`

## Pressure plate

One `PressurePlate` drives every "step here to actuate something" prop. The plate owns its connections: it holds a `connectedGateways` list and a `connectedPlatforms` list, both multi-target. The targets never point back at the plate, so wiring lives in one place — the plate.

On press (first valid contact) it toggles/opens its gateways and reverses its platforms; on release (last contact leaves) it closes gateways in momentary mode (toggle mode keeps the door state). The plate counts valid contacts so multi-collider objects do not release it early. `isToggleMode` applies to gateways only; platforms just reverse each press. Player/luggage filters determine what can trigger it. Plate tinting uses a `MaterialPropertyBlock`, avoiding per-instance material clones.

Connection lists are per-level wiring and should remain scene-instance overrides. Edit shared plate values (colors, filters, collider, mesh) in Prefab Mode, not from a scene instance, and never use "Apply All" from an instance — that would push one level's wiring into the prefab.

## Gateway

`Gateway` exposes `Open`, `Close`, and `Toggle`, driving the cached `AnimId.IsOpen` animator parameter. It is normally driven by a `PressurePlate`.

## Rotating platform

The platform rotates its body in `FixedUpdate` and can carry registered player/luggage riders around its pivot. `carryRiders` enables positional transport; `rotateRiders` additionally rotates rider orientation. It exposes `ReverseDirection`/`SetClockwise` for a `PressurePlate` to call; the plate owns that connection, so the platform keeps no reference back.

Rider identity uses the relevant component, with tags only as an optional fast path. Multiple collider contacts are counted per rider.

## One-way door

The door opens only for players approaching from the allowed local-space direction. It tracks accepted colliders and closes after `closeDelay` when the final player leaves; re-entry cancels the close. The animator parameter is hashed once.

## Pool hazard

Water force-drops a player, disables it, waits the configured delay, then calls `PlayerSpawner.MovePlayerToSpawn` and re-enables it. A fallback respawn point is used only when no spawner is available. Put a `LuggageSink` beneath water or void areas when luggage should be recycled too.

These tools are reusable logic prefabs. Keep connected objects, allowed direction, and placement as per-instance level data; apply shared collider/visual/animation fixes to the prefab deliberately.
