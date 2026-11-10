# Conveyors

**Script:** `Game/World/Conveyor.cs`
**Prefabs:** `Prefab/Environment/Conveyor Straight`, `Conveyor Turn`, `FullsetConveyor`, `ConveyorDoorOut`
**Models:** `Model/Environment/Essential/NewStraightConveyor.fbx` (mesh `Conveyor08`) and
`NewCurvedConveyor.fbx` (mesh `Conveyor08.002`), on each prefab's `Model` child since 15 Oct 2026

## Movement model

A conveyor never owns or parents luggage. Each physics step it steers a free dynamic luggage
rigidbody's *horizontal* velocity toward the belt direction and leaves vertical velocity alone.
Grabbed, in-station, and kinematic bags stay tracked but aren't moved; inactive ones are dropped
from the candidate cache.

Candidates cache their `Luggage`, `Rigidbody`, surface collider, and trigger contacts. A
downward ray must hit a collider belonging to *this* conveyor before steering starts, so flying
luggage and bags on the neighbouring belt are never dragged.

- A **straight** belt follows its flattened `transform.forward` — rotate the instance to set
  travel direction. It leaves a bag wherever it sits across the deck and however it is turned
  (19 Oct 2026). It used to centre every bag and face it along the belt, which delivered them in
  one identical row that dropped off the end as a line instead of a pile.
- A **turn** follows the tangent around its `turnPivot`. `clockwise` reverses it, and radial
  correction pulls luggage toward `centerRadius`. That radius is in world units: a turn scaled
  uniformly needs it scaled too (Level1's belts run at 0.6, so its turns use 1.734).

A PD-style angular correction stands bags upright. A turn also faces them along its arc, so they
clear the rails through the curve; a straight keeps each bag's own heading.
`OnDrawGizmosSelected` shows direction samples.

## Tuning

Move speed, acceleration, upright gain, centering gain, center radius, and surface-check margin
are serialized on the `Conveyor` component, set on the `Conveyor Straight` and `Conveyor Turn`
prefabs — the turn runs faster and pulls harder to the centreline. Don't copy these into scene
overrides; make a prefab variant when a belt family should genuinely behave differently.

## The belt shader

**Graph:** `Assets/Shader/ConveyorBelt.shadergraph` (URP Lit) · **Materials:**
`Material/Machine/ConveyorBelt{,_XLeft,_XRight,_ZBack}.mat`. Both prefabs default to
`ConveyorBelt` (`+z`), so an unrotated piece scrolls correctly out of the box; a rotated one needs
the matching material as an override.

The stripes are painted, not animated UVs: the belt mesh's UVs are collapsed onto single palette
texels ([floor shaders](../scenes/levels.md#floors) hit the same wall), so the pattern is built
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
| `Palette` | `carry on color palate3` | sampled on **mesh UV0** — the palette the conveyor FBX files ship with, so the belt keeps the model's own colours. Leave the UV slot unwired |
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
**The [level validator](../scenes/levels.md#level-validator) now checks this**: it reads `_BeltAxis` off
whatever material the piece actually has, compares it against the piece's forward, and errors if
they disagree by more than ~25°. It also warns when a piece carries no belt material at all, which
renders a deck that sits still while it carries luggage. Because belts also live in menu scenes,
this one check runs even where there is no `LevelContext`.

> The check compares against `transform.forward` for turns too, but a `clockwise` turn's bags
> leave along `−transform.right`. `MainMenu`'s two `FullsetConveyor` turns use the material that
> matches their exit, so the validator reports them as its only two errors there.

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

## Turn geometry

The curve's deck is a quarter disc around its inner corner plus a short straight lead at each end,
and the two leads differ: measured from the corner where the two travel lines cross, the **A
side** (the `−x` port in prefab space) is 2.895 m long and the **B side** (`+z` port) is 3.384 m.
The old turn's were 3.506 / 3.375, so the A side is 0.61 m shorter than it used to be.

| Setting | Value | Why |
|---|---|---|
| `Model` rotation | `(90, 270, 0)` | X 90 is the FBX's own axis conversion, Y 270 puts the A port on `−x` and B on `+z` like the old turn |
| `Model` position | `(1.2481, −0.01, −2.764)` | lines the travel lines up with the old turn's, so no layout had to move sideways; `−0.01` see below |
| `TurnPivot` | `(−1.699, −1.56, 1.79)` | centre of the arc tangent to both travel lines |
| `centerRadius` | `2.89` | that arc's radius — it starts exactly at the A port and ends 0.49 m inside the B port |

The pivot is **not** the mesh's inner corner. `Conveyor` steers along a pure arc with no straight
segments, so a pivot on the corner (radius 1.69) points bags 45° into the outer rail at the
leads. The 2.89 arc is the largest that fits the short A lead; the bag passes 1.2 m from the inner
corner at mid-turn.

**The model sits 1 cm below the straight deck on purpose.** The new curve's deck and rails have
exactly the straight's cross-section, so wherever a straight overlaps a turn the faces are
coplanar and the two belts' stripes z-fight. One centimetre lower lets the straight win every
overlap; the old turn sat 1 cm low too, so luggage already rode that step.

**A seam may step down or stay flush along the direction of travel, never up.** The deck is
frictionless and a bag is a flat-bottomed box, so a lip taller than PhysX's 1 cm contact offset
stops it dead. The belt keeps pushing it into the lip, and every bag behind it queues. In
MainMenu's `FullsetConveyor`, the two turns and the top connector sat at local y +0.03 / +0.02
while the legs sat at 0. That made a 2 cm step **up** where the left leg meets the first turn. On
20 Oct 2026 a Play-mode probe saw every bag jam there and none reach the sink in 80 s. Raising the
left leg's ten straights to +0.03 (1 cm above the turn, as above) cleared it: 0 stuck and 21 sink
arrivals in 60 s. The standard layout's step up at a turn's exit stopped no bag in Level1's
probe, where it is 0.6 cm (scale 0.6). A full 1 cm one is untested.

**Pieces mate flush, not overlapped.** A layout built around the old turn has a ~0.3–0.4 m gap at
the A port wherever it used to overlap by less than 0.6 m. On 15 Oct 2026 the U-ends of the Main
Baggage Loop and of MainMenu's `FullsetConveyor` were slid 0.3–0.4 m to close theirs (each turn
pair moves with the connector between them; the other turn's B overlap grows instead).

## Authoring rules

- The deck needs `Assets/Material/Physics/BeltSurface.physicMaterial`.
- The trigger volume enrols luggage; the solid deck is what the surface ray finds. Both must be
  descendants of the conveyor root.
- Check a route's seams with a height profile along its travel line (a ray down every 0.3 m):
  no step may go up. A piece's transform height isn't its deck height, because the turn's
  `Model` sits 1 cm low.
- Mating pieces need compatible **uniform** scale and the same deck height. A non-uniform scale
  changes deck height and breaks the seam. The one exception: a straight may be widened along its
  own X, since its height and length (what mates) stay the same. Level1's feed is 1.5× wide.
  Never stretch a turn: its arc stops matching the mesh.
- Keep `turnPivot` wired to the prefab's own child. Per-instance root rotation may vary, and
  `clockwise` only when deliberately reversing that same curve. A different radius or pivot
  belongs in a named variant.
- Align stations, spawn points, and receiving surfaces to the actual exit velocity, not just to
  the visible mesh.
- **A wall collider across a belt's door must block only the Player layer** (`excludeLayers` =
  everything else). Excluding just `Luggage` and `GrabbedLuggage` is not enough: the
  `BaggageDoorCurtain` strips are on `Default`. A strip that can't swing back into the wall jams
  every bag going *in* through that door, while bags coming *out* push the strips into the room
  and never show the problem.

The [level validator](../scenes/levels.md#level-validator) checks move speed, turn pivot, uniform scale (straights may be widened),
belt material, and a single deck height per scene. Everything about how a route *feels* stays
manual QA.

## Known state

- The archived `DesignScene` uses the legacy `ConveyorTemplate` rig — a container prefab nesting belt pieces,
  spawner, and sink — and reports **two deck heights about 5 cm apart**, i.e. a seam step. The
  sandbox therefore tests a slightly different belt than a real level would.
- **Not every belt is a prefab instance.** 21 are unpacked copies that ignore prefab edits: 15
  straights and 2 turns inside `FullsetConveyorLoop - Main Baggage Loop`, and
  `FullsetConveyor/Conveyor16–19` in `MainMenu`. The 15 Oct 2026 model swap set them by hand;
  the next swap has to as well, or they should be replaced with real instances first.
- `FullsetConveyor.prefab` (used only by the archived `DesignScene` and `Tutorial`) got the new
  turn but not the re-layout, so its A-port seams may gap.
- **Level1's loop is too narrow for a turned bag.** At scale 0.6 its rails are 1.97 m apart. An
  upright bag's 1.74 × 0.99 m footprint is wider than that between about 50° and 71° to the
  belt (2.00 m at worst). Since 19 Oct straights no longer face bags along the belt. So a bag that
  lands on the loop at such an angle, or is swung through one by a turn, wedges between the rails
  and backs the loop up. The 20 Oct probe caught it at the `Out Belt 7` → `Turn North` seam and on
  `In Belt 1`, and the jam then pushed bags over the rail onto the floor. Only players put bags on
  this loop (the gate's trapdoor drops into the void sink). **Not fixed yet.** The feed is 1.5×
  wide and doesn't have this problem. MainMenu's belts are at scale 1, about 3.3 m between rails, so
  they can't wedge.
- The new straight mesh is the old `Conveyor08` from `AirportBuildingTrial03.fbx`, re-exported —
  same size, same deck height, same palette texels — so straight-only layouts needed no changes.
