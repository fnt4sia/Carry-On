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

## The belt shader

**Graph:** `Assets/Shader/ConveyorBelt.shadergraph` (URP Lit) · **Materials:**
`Material/Machine/ConveyorBelt{,_XLeft,_XRight,_ZBack}.mat` — 44 renderers in `MainMenu`.

The stripes are painted, not animated UVs: the belt mesh's UVs are collapsed onto single palette
texels ([floor shaders](../services.md#colour-grade) hit the same wall), so the pattern is built
from **position** instead. The graph is three masks multiplied together and one `Lerp`:

```text
phase   = dot(worldPosition, BeltAxis) − Time · ScrollSpeed
stripe  = step(StripeWidth, frac(phase · StripeDensity))
deck    = 1 − step(0.1, abs(objectPosition.z + 0.25))     // the belt band, not the rails
facing  = step(0.5, worldNormal.y)                        // top surface only
BaseColor = lerp(Palette.Sample(uv0), StripeColor, stripe · deck · facing)
```

| Property | Value | Does |
|---|---|---|
| `Palette` | `carry on color palate` | sampled on **mesh UV0**, so the belt keeps the airport palette colours it had before the shader existed. Leave the UV slot unwired |
| `BeltAxis` | per material, see below | world-space travel direction the stripes scroll along |
| `ScrollSpeed` | `5` | metres per second — matches `Conveyor.moveSpeed` on the straight prefab, which is why the stripes look like they carry the bags |
| `StripeDensity` | `0.9` | stripes per metre, so one period is ~1.11 m |
| `StripeWidth` | `0.5` | fraction of the period left unpainted; `step` fires above it, so **larger = thinner stripe** |
| `StripeColor` | `(0.290, 0.125, 0.220)` | the dark stripe over the palette colour |

**Why four materials.** `BeltAxis` is world space, so a belt run needs the axis of the direction it
travels: `ConveyorBelt` `(0,0,1)`, `_ZBack` `(0,0,−1)`, `_XRight` `(1,0,0)`, `_XLeft` `(−1,0,0)`.
Pick the material that matches the run, or the stripes crawl sideways or backwards under the bags.
A new direction means a fifth material, not a scene override.

### Changing which way a belt runs

Two separate things decide direction, and **nothing links them**:

| What | Lives on | Change it by |
|---|---|---|
| Which way the **luggage** travels | `Conveyor` component, which steers along `transform.forward` (turns use `clockwise` around `turnPivot`) | rotating the piece in the scene — 180° on Y reverses a straight |
| Which way the **stripes** scroll | the belt material's `BeltAxis` | assigning the material whose axis matches the new facing |

> **The material is on the `Model` child, not on the conveyor root.** Selecting the piece and
> finding no material is the normal experience — expand it, select `Model`, and the Mesh Renderer's
> material slot is there. This is the single most common "where do I even change this" moment on
> the belt.

Rotate a piece and forget the material and the bags ride forwards while the deck scrolls backwards.
**The [level validator](../levels.md#level-validator) now checks this**: it reads `_BeltAxis` off
whatever material the piece actually has, compares it against the piece's forward, and errors if
they disagree by more than ~25°. It also warns when a piece carries no belt material at all, which
renders a deck that sits still while it carries luggage — three pieces in `MainMenu` are in exactly
that state (`FullsetConveyor/StraightConveyor (1)` and two under the Main Baggage Loop). Because
belts also live in menu scenes, this one check runs even where there is no `LevelContext`.

**The two masks are what keep the stripes on the deck.** `deck` is an *object*-space test: the belt
quad sits at object `z ≈ −0.26` on both the straight and the turn mesh (the rails are at `−0.68`),
so `abs(z + 0.25) < 0.1` selects the band and nothing else. `facing` throws away anything whose
world normal is not pointing up, which is what stops the side panels getting striped. Drop either
one and the whole piece — rails included — gets painted.

> Stripes cross a turn piece as straight bands, because `phase` is a plain dot product and knows
> nothing about the arc. Curving them needs polar coordinates around `turnPivot`, not a tweak.

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

- The archived `DesignScene` uses the legacy `ConveyorTemplate` rig — a container prefab nesting belt pieces,
  spawner, and sink — and reports **two deck heights about 5 cm apart**, i.e. a seam step. The
  sandbox therefore tests a slightly different belt than a real level would.
- Conveyor art is still placeholder. Because every belt is an instance of `Conveyor Straight` or
  `Conveyor Turn`, swapping the model later means editing those two prefabs once.
