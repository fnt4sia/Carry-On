# World interactables and hazards

**Scripts:** `Game/World/Gateway.cs`, `GatewayPressurePlate.cs`, `RotatingPlatform.cs`, `RotatingPlatformPressurePlate.cs`, `OneWayDoor.cs`, `PoolHazard.cs`

## Gateway and pressure plate

`Gateway` exposes `Open`, `Close`, and `Toggle`, driving the cached `AnimId.IsOpen` animator parameter. A `GatewayPressurePlate` can control several gateways.

The plate counts valid contacts so multi-collider objects do not release it early. Toggle mode keeps the new door state after release; momentary mode explicitly opens while occupied and closes when the final contact leaves. Player/luggage filters determine what can trigger it. Plate tinting uses a `MaterialPropertyBlock`, avoiding per-instance material clones.

Connected gateway lists are per-level wiring and should remain scene-instance overrides.

## Rotating platform

The platform rotates its body in `FixedUpdate` and can carry registered player/luggage riders around its pivot. `carryRiders` enables positional transport; `rotateRiders` additionally rotates rider orientation. A linked pressure plate reverses direction on the first valid contact and resets only its visual on release.

Rider identity uses the relevant component, with tags only as an optional fast path. Multiple collider contacts are counted per rider.

## One-way door

The door opens only for players approaching from the allowed local-space direction. It tracks accepted colliders and closes after `closeDelay` when the final player leaves; re-entry cancels the close. The animator parameter is hashed once.

## Pool hazard

Water force-drops a player, disables it, waits the configured delay, then calls `PlayerSpawner.MovePlayerToSpawn` and re-enables it. A fallback respawn point is used only when no spawner is available. Put a `LuggageSink` beneath water or void areas when luggage should be recycled too.

These tools are reusable logic prefabs. Keep connected objects, allowed direction, and placement as per-instance level data; apply shared collider/visual/animation fixes to the prefab deliberately.
