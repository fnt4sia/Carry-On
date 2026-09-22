# Luggage

**Scripts:** `Game/Luggage/{Luggage, LuggageColor, LuggageSpawner, LuggageSink, LuggageVisualEffects}.cs`
**Prefabs:** the nine under `Assets/Prefab/Luggage/`

Luggage is a dynamic rigidbody; a station docks it kinematically while processing. The `Luggage`
component owns flight colour, the wrapped flag, grabber attribution, pooling identity, and cached
physics references. **Bags never expire** — the pressure is the gate's departing flight.

Physics and audio values are serialized on the `Luggage` component of each luggage prefab.
Per-level spawn content comes from the active `LevelConfig`.

## Colour

`Luggage.color` (`LuggageColor`: Red / Blue / Green / Yellow) is the bag's identity, and colour is
the whole destination rule — a gate's flight asks for N bags of a colour and *any* bag of that
colour fills a slot. Nothing is assigned to a particular gate; see
[delivery and scoring](delivery-and-scoring.md).

Colour lives on the prefab. `Luggage Red/Green/Yellow.prefab` are **variants** of
`Normal Luggage.prefab` that override exactly two things: the `color` field and the renderer's
material. Edit the base prefab and all of them inherit it — never fork them into full copies.

`Luggage Blue.prefab` and `LuggageBlue.mat` still exist but are **used by nothing**: Level1 runs
red / green / yellow only. Blue is unusable as a tint anyway — see below.

**The colour materials must not tint `Luggage.png`.** That atlas is a saturated orange, and
`_BaseColor` multiplies against it, so a yellow tint came out orange, green came out olive, and
blue came out muddy brown (the texture has almost no blue channel to multiply). The colour
materials therefore use **`LuggageTintable.png`** instead — the same atlas run through luminance
and stretched so the case body is pure white while straps and hardware stay as darker detail. The
tint then *is* the colour. `NormalLuggage.mat` still uses the original orange atlas.

If you add a colour, give it a `LuggageTintable`-based material; tinting the orange atlas will
quietly produce a colour nobody can name.

## Removed in September 2026

Sticky and Fragile luggage, the washer, and per-bag lifetime (countdown, expiry penalty, and the
dial over each bag) are gone from the code. The redesign replaced them: wrapping became a manifest
axis ("red, wrapped") and the pressure moved from each bag to the departing flight.

What was kept on purpose, for a future luggage design:

- `Sticky Luggage`, `Fragile Luggage` and `Wrapped Luggage.prefab` — kept for their models. They
  now behave as plain bags and nothing spawns them (the archived configs still list Sticky and
  Fragile).
- `Luggage Timer.prefab` — the dial art, with no script on it. No luggage prefab nests it.
- `WashingStation.prefab` — see [stations](stations.md).

The old behaviour is in git at `bc233e5`, the last commit before the redesign.

## Wrapping

`MarkWrapped()` wraps a bag **in place**: it sets `IsWrapped` and switches on the `wrapVisual`
child. The bag object is never replaced, so its flight colour survives the machine — a wrapped red
bag is still red.

The shell is `Wrap Shell` on `Normal Luggage.prefab` — the same case mesh at 1.07 scale with a
translucent `LuggageWrap.mat`, shadows off, inactive by default. All three colour variants inherit
it. `Initialize` re-applies the visual, so a pooled bag never comes back still wearing it.

## Spawn pacing

`LuggageSpawner` reads `LevelContext.CurrentConfig` and drops **one bag every `spawnInterval`
seconds** — the belt runs flat, and there are no waves.

**The first bag drops on the spawner's first frame, not one interval later.** Both loops spawn and
*then* wait; they used to wait first, which opened every round on an empty belt. The wait is scaled
time and the countdown runs at `timeScale 0`, so a leading yield spent the whole countdown frozen
and then charged another `spawnInterval` after "GO". Now the opening bag is already sitting at the
spawn head while the countdown plays and rides off the moment time starts.

**Colour order is a strict round robin, not a random draw.** The spawner walks `luggagePrefabs`
in order and wraps — red, green, yellow, red, … — so every colour is guaranteed to arrive on a
fixed cycle and a flight can never become unfillable because a colour refused to show up. Losing
is on the players, not the dice. Reordering the list reorders the belt. Pacing values are
documented in [levels](../levels.md).

**A belt needs a `LuggageSink` on it.** Bags never expire, so without one the belt fills and the
spawner stalls. Level1's `LuggageSinker` sits across the loop's west run; a bag reaching it is
returned to the pool and re-rented as the next spawn.

Place a sink so it actually *straddles the lane*: bags ride at about y = 1.2, so a trigger whose
box starts above that lets nearly everything pass underneath, and one narrower than the belt
gauge only clips the occasional bag. Both were true of Level1's sink at first, and the symptom is
a belt that slowly creeps to the cap and then stops spawning.

**`maxActiveLuggage` is a safety valve, not a pacing knob.** It exists for the case where nothing
is draining — bags abandoned on the floor — and it pauses the belt rather than burying the arena.
**The spawner never destroys a live bag to make room.** Set it *above* the belt's natural
in-flight count or it throttles normal flow: Level1 settles at ~19 bags (bags travel nearly a full
lap before reaching the sink) against a cap of 28.

## Menu luggage

`Luggage.isTutorialLuggage` turns a bag into scenery: no trail or impact smoke plays. The bag is
otherwise a normal physics bag that belts push and players can grab. (The name predates the
redesign; the menu is its only user now.)

A scene with no `LevelContext` — `MainMenu` is the only one today — has no config to read, so the
spawner falls back to its own serialized fields instead of erroring out: `fallbackLuggagePrefabs`
on a flat `fallbackSpawnInterval`, every bag flagged scenery, capped at `fallbackMaxActive`
riding at once (the menu belt is a closed loop, so one snagged bag would otherwise let the count
climb forever). MainMenu's list is `Normal Luggage` only.

`LuggageVisualEffects` applies the silencing in `Update`, not `Awake` — the spawner assigns the
flag after `Awake` has already run. It also clears `playOnAwake` on the three `HitSmoke`
systems, which otherwise puff once every time a pooled bag is re-enabled.

## Pooling

The spawner pools by `sourcePrefab`. `RentLuggage` restores transform and activation;
`Initialize` resets every mutable field. `ReturnLuggage` is an instance method and returns
`false` when an object has no pool key, which lets `Luggage.DestroyLuggage` log and destroy it
safely rather than leaking.

A bag that never came from a pool has no `sourcePrefab`, so `ReturnLuggage` refuses it and
`DestroyLuggage` destroys it. That is the normal end for scene-placed luggage and is **not**
warned about — only a bag that *did* carry a pool key and still failed to return logs a warning.

`LuggageSink` recycles anything that falls out of the play area. Put one under every water or
void volume.

Gameplay triggers resolve bags through `Luggage.TryGetFromCollider` — the component is the
identity. Physics layers narrow queries; tags are never the source of truth.

## Collision audio

Collision speed selects one of three ground or window/glass clips, throttled by a cooldown
serialized on the luggage prefab. IDs come from `Sfx.LuggageCollision`, which builds the tiered
ID in one place; `AudioManager` warns once when an ID isn't registered.

**Two floors, not one.** `minimumCollisionAudioSpeed` (1.5 m/s) still picks the tier, but speed
alone counted a *glancing* contact as a hard hit — a bag sliding along a wall or shuffling
against another bag keeps its full relative velocity while barely pushing on anything, so the
clatter never stopped. `minimumCollisionImpulse` (60) is the second gate and the one that
actually quiets things: it is the force that landed, not how fast the surfaces were passing.
Luggage is 30 kg under −15 gravity, so 60 ≈ a 2 m/s head-on hit and a settling nudge is nearer
3. Neither floor stamps the cooldown, so a rejected
tap can never mute a real impact a moment later.

**Conveyors are silent.** A bag rattles against the deck the whole way down a belt, so every
seam and bounce fired a clip and a running level turned into constant clatter. `PlayCollisionAudio`
drops any collision whose collider resolves to a `Conveyor` through `GetComponentInParent`. The
test runs **before** the cooldown, so a muted belt hit never eats the cooldown a real impact
needs, and it only silences the belt — bag-on-bag, bag-on-player, and the sliding-door `Panel`
colliders on `FullsetConveyor` all still play while riding it.

The one thing it also silences is a bag slammed into the *side* of a belt: the deck
`MeshCollider` is generated from the whole model, rails included, so nothing distinguishes a
side rail from the deck by collider. If that ever matters, gate on an upward contact normal
instead of muting the collider outright.
