# Conveyors

**Script:** `Game/World/Conveyor.cs` — **Prefabs:** `Prefab/Environment/Conveyor Straight`, `Conveyor Turn`, `ConveyorTemplate`

## Movement model

A conveyor never owns or parents luggage. Each physics step it steers a free dynamic luggage rigidbody's horizontal velocity toward the belt direction while leaving vertical velocity alone. Grabbed, in-station, and kinematic luggage remains tracked but is not moved; inactive luggage is removed from the candidate cache.

Candidates cache their `Luggage`, `Rigidbody`, surface collider, and trigger contacts. A downward ray must hit a collider under the current conveyor before steering begins, so flying luggage and adjacent pieces are not affected.

## Straight and turn pieces

- A straight belt follows its flattened `transform.forward`; rotate the prefab instance to set travel direction. Light lateral correction keeps cargo centered for the next seam.
- A turn follows the tangent around its `turnPivot`. `clockwise` reverses it, and radial correction pulls luggage toward the configured center radius.

The belt also applies a PD-style angular correction so luggage stands upright and faces the travel direction. `OnDrawGizmosSelected` shows direction samples.

## Tuning

Shared values live in `ConveyorTuning`: movement speed, acceleration, upright gain, centering gain, center radius, and surface-check margin. Straight and turn prefabs reference `Assets/Config/Tuning/Conveyor_Straight.asset` and `Conveyor_Turn.asset`. Do not copy these values into level scene overrides; create a deliberate tuning variant when a belt family should behave differently.

## Authoring rules

- The deck needs `Assets/Material/Map/BeltSurface.physicMaterial`.
- The trigger volume enrolls luggage; the solid deck collider is what the surface ray detects. Both must be descendants of the conveyor root.
- Mating pieces must use compatible uniform scale and deck height; inspect seams in Scene view and Play Mode.
- Keep `turnPivot` wired to the prefab's internal child. Per-instance root rotation—and `clockwise` only when intentionally reversing that same curve—may vary. A different radius or pivot belongs in a named turn variant.
- Keep stations, spawn point, and receiving surfaces aligned with the actual exit velocity rather than only the visible mesh.

The stage scenes have been structurally migrated to reusable straight/turn prefab instances and tuning assets; the old direct conveyor components are gone. End-to-end traversal, route feel, and seam quality remain level-design QA whenever geometry or models change; use the checklist in [level authoring](../levels/level-authoring.md).

## Colliders: why the trigger is a plain box

Each piece carries **two colliders with different jobs**, and they are not meant to match each other:

- **BoxCollider, `isTrigger = 1`** — a coarse *candidate net*. It only answers "is there luggage near this belt worth considering?" and feeds the candidate cache.
- **MeshCollider, solid, `BeltSurface` material** — the *actual deck*, generated from the art, so it follows the curve exactly.

Precision comes from `IsOnSurface`, which casts a ray straight down from the luggage and steers only if the collider beneath it belongs to this conveyor. So the trigger box **does not need to match a curved mesh** — false positives inside it are rejected by the ray, and overlapping boxes at a seam are harmless because only the belt physically underneath wins.

The practical consequence: make trigger boxes **generous**, especially on turns, where the arc bulges outside a tight box. A box that is too small creates a dead zone where luggage sits on the deck but is never enrolled. Do not hand-build curved trigger volumes.

If a turn *feels* wrong — cargo cutting the corner or clipping the rails — that is never the collider. It is `centerRadius` / `centeringGain` / `turnPivot` in `ConveyorTuning`.

## Known state

- The five build scenes pass the [level validator](../development/level-validator.md) (uniform scale, one deck height, tuning and belt material present).
- `DesignScene` still uses the legacy `ConveyorTemplate` rig — a container prefab that nests belt pieces, spawner, and sink — and reports **two deck heights ~5 cm apart**, i.e. a seam step. The sandbox therefore tests a slightly different belt than the game ships.
- Conveyor art is still placeholder. Because every belt in every scene is an instance of `Conveyor Straight` / `Conveyor Turn`, replacing the model later means editing those two prefabs once, not touching the stages.
