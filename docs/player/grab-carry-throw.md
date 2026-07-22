# Grab, carry, and throw

**Scripts:** `Game/Player/PlayerGrab.cs`, `JointBreakHandler.cs` — **Tuning:** `Assets/Config/Tuning/Grab_Default.asset`

One Grab action handles pickup, hold-to-charge throw, and quick drop. UseStation places a held luggage in a compatible machine.

## Detection and ownership

`Physics.OverlapSphereNonAlloc` searches the configured luggage layer around `grabPoint` using one shared 32-collider buffer; a ray to the rigidbody center rejects targets hidden by blocking geometry. Logic then resolves the nearest valid `Luggage` component and skips in-station items.

Pickup force-drops existing grabbers before attaching, so one player holds an item at a time. `lastGrabber` persists after release for delivery attribution. `GetPlayerIndex` reads the attached `PlayerInput.playerIndex`, not a serialized clone value.

## Carry setup

Child mesh bounds determine a connection point on the luggage back face. A `ConfigurableJoint` is created immediately, then yaw aligns over the configured duration. Arc selection avoids rotating the luggage through the player's body. A runtime bridge collider spans player and luggage so the carrier cannot walk through the held item; collisions with the held item itself are ignored until release.

Joint limits, springs, dampers, maximum forces, projection, and break thresholds all come from `GrabConfig`. `JointBreakHandler` calls a clean forced drop if Unity breaks the joint.

## Throw and drop

- Releasing before the configured minimum hold drops the item.
- Holding longer interpolates forward and upward impulse through the configured ranges.
- The arrow child shows charge, and a looping buildup SFX stops on release.
- Sticky luggage cannot be voluntarily dropped or thrown; station placement, stealing, hazards, and joint breaks can force release.

Animator and audio calls use cached `AnimId`/`Sfx` IDs.

## Station use

UseStation reuses the same non-allocating overlap buffer, finds a `MachineStation` that is free and accepts the held luggage, and calls `TryPlace`. Placement force-releases the player before the station takes over. See [machine stations](../gameplay/machine-stations.md).

`OnDrawGizmosSelected` visualizes the grab radius and clear/blocked lines, which is useful when tuning layers and geometry.
