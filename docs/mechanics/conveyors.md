# Conveyors

**Script:** `Game/World/Conveyor.cs`
**Prefabs:** `Prefab/Environment/Conveyor Straight`, `Conveyor Turn`, `FullsetConveyor`, `ConveyorDoorOut`

## Movement model

A conveyor never owns or parents luggage. Each physics step it steers a free dynamic luggage
rigidbody's *horizontal* velocity toward the belt direction and leaves vertical velocity alone.
Grabbed, in-station, and kinematic bags stay tracked but aren't moved; inactive ones are dropped
from the candidate cache.

Candidates cache their `Luggage`, `Rigidbody`, surface collider, and trigger contacts. A
downward ray must hit a collider belonging to *this* conveyor before steering starts, so flying
luggage and bags on the neighbouring belt are never dragged.

- A **straight** belt follows its flattened `transform.forward` — rotate the instance to set
  travel direction. Light lateral correction keeps cargo centred for the next seam.
- A **turn** follows the tangent around its `turnPivot`. `clockwise` reverses it, and radial
  correction pulls luggage toward `centerRadius`.

A PD-style angular correction also stands bags upright and faces them along travel.
`OnDrawGizmosSelected` shows direction samples.

## Tuning

Move speed, acceleration, upright gain, centering gain, center radius, and surface-check margin
are serialized on the `Conveyor` component, set on the `Conveyor Straight` and `Conveyor Turn`
prefabs — the turn runs faster and pulls harder to the centreline. Don't copy these into scene
overrides; make a prefab variant when a belt family should genuinely behave differently.

## Why the trigger is a plain box

Each piece carries **two colliders with different jobs**, and they are not meant to match:

- **BoxCollider, `isTrigger = 1`** — a coarse *candidate net*. It only answers "is there luggage
  near this belt worth considering?" and feeds the candidate cache.
- **MeshCollider, solid, `BeltSurface` material** — the *actual deck*, generated from the art, so
  it follows the curve exactly.

Precision comes from `IsOnSurface`, which casts a ray straight down from the bag and steers only
if the collider beneath it belongs to this conveyor. So the trigger box **does not need to match
a curved mesh** — false positives inside it are rejected by the ray, and overlapping boxes at a
seam are harmless because only the belt physically underneath wins.

Practical consequence: make trigger boxes **generous**, especially on turns, where the arc bulges
outside a tight box. A box that's too small creates a dead zone where luggage sits on the deck
and is never enrolled. Never hand-build curved trigger volumes.

If a turn *feels* wrong — cargo cutting the corner, clipping the rails — that is never the
collider. It's `centerRadius`, `centeringGain`, or `turnPivot` on the `Conveyor Turn` prefab.

## Authoring rules

- The deck needs `Assets/Material/Map/BeltSurface.physicMaterial`.
- The trigger volume enrols luggage; the solid deck is what the surface ray finds. Both must be
  descendants of the conveyor root.
- Mating pieces need compatible **uniform** scale and the same deck height. A non-uniform scale
  changes deck height and breaks the seam.
- Keep `turnPivot` wired to the prefab's own child. Per-instance root rotation may vary, and
  `clockwise` only when deliberately reversing that same curve. A different radius or pivot
  belongs in a named variant.
- Align stations, spawn points, and receiving surfaces to the actual exit velocity, not just to
  the visible mesh.

The [level validator](../levels.md#level-validator) checks move speed, turn pivot, uniform scale,
belt material, and a single deck height per scene. Everything about how a route *feels* stays
manual QA.

## Known state

- `DesignScene` uses the legacy `ConveyorTemplate` rig — a container prefab nesting belt pieces,
  spawner, and sink — and reports **two deck heights about 5 cm apart**, i.e. a seam step. The
  sandbox therefore tests a slightly different belt than a real level would.
- Conveyor art is still placeholder. Because every belt is an instance of `Conveyor Straight` or
  `Conveyor Turn`, swapping the model later means editing those two prefabs once.
