# Movement and dash

**Script:** `Game/Player/PlayerMovement.cs` — **Tuning:** `Assets/Config/Tuning/Player_Default.asset`

Movement is camera-relative and position-driven. `Update` reads the player's production `Move` and `Dash` actions; `FixedUpdate` smooths velocity, clamps it against walls, moves the transform, and turns toward travel direction.

The shared `PlayerConfig` owns normal speed, rotation, carry rotation multiplier, smoothing, dash multiplier, duration, and cooldown. A player prefab stores one reference to that config rather than duplicated serialized defaults.

## Wall-slide sweep

Transform motion has no continuous collision sweep, so both the player rigidbody and a carried luggage body run `SweepTest` along the proposed delta. Static, kinematic, or heavier collisions remove only the into-wall component from displacement and velocity, preserving a slide. Triggers and lighter dynamic props do not block movement.

## Carry anchor delta

When carrying, the controller records `grabAnchor.position` before movement and rotation, then moves luggage by the anchor's full world delta. This contains both linear movement and the rotational arc, while the joint corrects residual pitch/roll.

## Dash

Dash temporarily multiplies normal movement speed and stays steerable. Cooldown and duration use scaled gameplay time. Animator and audio IDs use `AnimId.IsDashing` and `Sfx.PlayerDash` constants.

## Bubble VFX

Movement bubbles use a per-player object pool. Each active bubble moves, shrinks, and fades through a `MaterialPropertyBlock`, then returns to the pool. No material is cloned and no object is destroyed per emission. The bubble prefab remains a replaceable presentation asset.

Persisted players are inert outside a gameplay scene, detected through the active `GameManager`, and reacquire the scene camera after loads.
