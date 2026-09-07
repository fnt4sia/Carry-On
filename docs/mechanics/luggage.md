# Luggage

**Scripts:** `Game/Luggage/{Luggage, LuggageBehaviorType, LuggageColor, LuggageSpawner, LuggageSink, LuggageTimerDisplay}.cs`
**Prefabs:** the nine under `Assets/Prefab/Luggage/`

Luggage is a dynamic rigidbody; a station docks it kinematically while processing. The `Luggage`
component owns flight colour, behaviour, processing flags, expiry, grabber attribution, pooling
identity, and cached physics references.

Physics and audio values are serialized on the `Luggage` component of each luggage prefab.
Per-level lifetime and spawn content come from the active `LevelConfig`.

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

## Behaviour types

`LuggageBehaviorType` is a plain enum — one bag, one initial behaviour.

| Type | Rule |
|---|---|
| `Normal` | deliver directly |
| `Sticky` | must be washed; cannot be voluntarily dropped or thrown |
| `Fragile` | must be wrapped; breaks above the configured collision impulse |

Behaviour comes entirely from the prefab the spawner picked — there is no behaviour list on
`LevelConfig`. `initialBehaviorType` survives washing and wrapping, and is what
`RequiresWashing` / `RequiresWrapping` read. To change which stations a level needs, change its
`luggagePrefabs`.

> A `Bomb` type and a `Scanner` station existed until 2026-07-14. Both were removed — never a
> requested mechanic, to be redesigned with their own model later. No code refers to them.

## Washing and wrapping

| Flag | Set by | Result |
|---|---|---|
| `IsWashed` | `WashingMachine` | swaps the sticky object for the washed prefab |
| `IsWrapped` | `Wrapper` | swaps the fragile object for the wrapped prefab |

Processing replaces the object, so state has to survive the swap. `CopyRuntimeStateFrom`
preserves lifetime, original behaviour, delivery and station state, processing flags, last
grabber, and destination gate; `ReplaceWithPrefab` restores linear and angular velocity
afterwards. **Any new runtime field that must outlive processing has to be added to that
migration** — nothing catches an omission.

Fragile collision behaviour is per-prefab: `fragileBreakThreshold` and
`fragileGrabImmunityDuration`. Picking a fragile bag up grants that short immunity so carry
alignment can't shatter it in your hands.

## Wrapping

`MarkWrapped()` wraps a bag **in place**: it sets `IsWrapped` and switches on the `wrapVisual`
child. The bag object is never replaced, so its flight colour survives the machine — a wrapped red
bag is still red.

That matters because the older `ConvertToWrapped(prefab)` swaps the bag for a single colourless
wrapped prefab, which would throw the colour away. `Wrapper.wrapsAnyLuggage` picks between them:
on it wraps anything unwrapped in place, off it keeps the legacy Fragile-only prefab swap.

The shell is `Wrap Shell` on `Normal Luggage.prefab` — the same case mesh at 1.07 scale with a
translucent `LuggageWrap.mat`, shadows off, inactive by default. All three colour variants inherit
it. `Initialize` re-applies the visual, so a pooled bag never comes back still wearing it.

## Lifetime (opt-in)

**`LevelConfig.luggageLifetime = 0` disables the countdown entirely, and that is how the
flight-manifest levels play.** The pressure belongs to the gate's departing flight, not to every
individual bag rotting on the floor. Level1 runs at 0.

Where a lifetime *is* set, the countdown runs only while the bag is free: it pauses in a station
and stops on delivery or expiry. Expiry applies `LevelConfig.scoreTimerExpired` through
`RoundScoreContext` and destroys the bag.

Scene-placed luggage initialises on `Start` from the active spawner or level lifetime, falling
back to 30 seconds with a warning when no level context exists.

## Spawn pacing

`LuggageSpawner` reads `LevelContext.CurrentConfig` and drops **one bag every `spawnInterval`
seconds** — the belt runs flat, and there are no waves.

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

## Tutorial luggage (menus)

`Luggage.isTutorialLuggage` turns a bag into scenery: the lifetime countdown never runs, the
timer readout stays hidden, fragile bags don't shatter, and no trail or impact smoke plays. The
bag is otherwise a normal physics bag that belts push and players can grab.

A scene with no `LevelContext` — `MainMenu` is the only one today — has no config to read, so the
spawner falls back to its own serialized fields instead of erroring out: `fallbackLuggagePrefabs`
on a flat `fallbackSpawnInterval`, every bag flagged tutorial, capped at `fallbackMaxActive`
riding at once (the menu belt is a closed loop, so one snagged bag would otherwise let the count
climb forever). The flag is set *before* `Initialize` because `Initialize` refreshes the timer
readout, which reads it.

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
`Tutorial` authors every bag in the scene and has no spawner at all, so without that distinction
every delivery there logged a false alarm.

`LuggageSink` recycles anything that falls out of the play area. Put one under every water or
void volume. `Tutorial` has neither a sink nor a spawner, so its rooms recover fallen bags
themselves — see [levels](../levels.md#tutorial).

Gameplay triggers resolve bags through `Luggage.TryGetFromCollider` — the component is the
identity. Physics layers narrow queries; tags are never the source of truth.

## Timer UI

Every luggage prefab nests `Assets/Prefab/UI/Luggage Timer.prefab`. `LuggageTimerDisplay` is a
thin world-space presenter:

- a radial `Image.fillAmount` reads `LifetimeNormalized`;
- warning and danger colours apply at the configured thresholds;
- it billboards to `Camera.main` and sits on the `WorldUI` layer.

Where `luggageLifetime` is 0 the readout hides itself, because the visibility test requires at
least one second remaining. Nothing extra is needed to switch it off.

**There is no seconds readout.** The dial is the whole signal — colour and sweep, no number.
`Time` and the plain `Face` disc were deleted from the prefab in August 2026 and `timeText` is
deliberately left empty; `LuggageTimerDisplay` still null-checks it, so re-adding a text object
and dragging it back in is all it takes to bring the number back.

The body is now `Assets/UI/InGame/TimeIcon.png` — a stopwatch whose cream face is **not** centred
in the image (the crown pushes it down). The dial is aligned to that face, not to the sprite:

| | |
|---|---|
| `Frame` | `TimeIcon`, `preserveAspect`, `153.2 × 160` (the sprite is 634 × 662) |
| `Fill` | `72 × 72` at `(1.93, −7.73)` — the face is 297 px across, centred at `(325, 363)` of 634 × 662 |
| `Gate` | pushed down to `y −95` to clear the taller icon |

Re-derive those two numbers from the sprite if the art changes; don't eyeball them.

The whole readout hides while the bag is delivered, expired, **or inside a station**. The
in-station case exists because the countdown is frozen there — a visible dial that never moves
reads as a bug. It comes back the moment `SetInStation(false)` runs, which is the same instant
[`PlayerGrab`](player.md) will let the bag be picked up again.

The art lives in the prefab and can be replaced without touching `Luggage` or generating meshes
at runtime. See [services](../services.md#camera) for the overlay camera that makes `WorldUI`
render on top.

## Collision audio

Collision speed selects one of three ground or window/glass clips, throttled by a cooldown
serialized on the luggage prefab. IDs come from `Sfx.LuggageCollision`, which builds the tiered
ID in one place; `AudioManager` warns once when an ID isn't registered.

**Two floors, not one.** `minimumCollisionAudioSpeed` (1.5 m/s) still picks the tier, but speed
alone counted a *glancing* contact as a hard hit — a bag sliding along a wall or shuffling
against another bag keeps its full relative velocity while barely pushing on anything, so the
clatter never stopped. `minimumCollisionImpulse` (60) is the second gate and the one that
actually quiets things: it is the force that landed, not how fast the surfaces were passing.
Luggage is 30 kg under −15 gravity, so 60 ≈ a 2 m/s head-on hit, a settling nudge is nearer 3,
and the fragile break at 300 is a 10 m/s slam. Neither floor stamps the cooldown, so a rejected
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
