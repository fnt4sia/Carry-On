# Luggage

**Scripts:** `Game/Luggage/Luggage.cs`, `LuggageBehaviorType.cs`, `LuggageSpawner.cs`, `LuggageSink.cs`, `LuggageTimerDisplay.cs`

Luggage is normally a dynamic rigidbody; a station temporarily docks it kinematically while processing. The component owns behavior, processing flags, expiry, grabber attribution, destination, pooling identity, and cached physics references. Shared physics/audio values come from `LuggageTuning`; per-level lifetime and spawn content come from the active `LevelConfig`.

## Behavior types

`LuggageBehaviorType` is a plain enum, so one luggage has one initial behavior.

| Type | Rule |
|---|---|
| `Normal` | deliver directly |
| `Sticky` | must be washed; cannot be voluntarily dropped or thrown |
| `Fragile` | must be wrapped; breaks above the configured collision impulse |

Behavior comes entirely from the selected prefab: the spawner picks a prefab from the level's pool and uses that prefab's own `behaviorType`. `initialBehaviorType` survives washing/wrapping and determines `RequiresWashing` and `RequiresWrapping`.

> A `Bomb` type and a `Scanner` station previously existed. Both were removed on 2026-07-14 — they were never a requested mechanic and will be redesigned with their own model later. Nothing in the code refers to them now.

## Processing and runtime migration

| Flag | Set by | Result |
|---|---|---|
| `IsWashed` | `WashingMachine` | replaces the sticky object with the washed prefab |
| `IsWrapped` | `Wrapper` | replaces the fragile object with the wrapped prefab |

`CopyRuntimeStateFrom` preserves lifetime, original behavior, delivery/station state, processing flags, last grabber, and destination gate during a prefab swap. `ReplaceWithPrefab` restores linear/angular velocity after the state copy. Any new runtime field that must survive processing must be added to this migration.

## Lifetime and hazards

Lifetime counts down only while the luggage is free. It pauses in a station and stops after delivery/expiry. Expiry applies `LevelConfig.scoreTimerExpired` through `RoundScoreContext` and destroys the luggage.

Scene-placed luggage is safe: on `Start` it initializes from the active spawner/level lifetime, with a 30-second fallback and a warning if no level context exists.

Fragile collision behavior comes from `Assets/Config/Tuning/Luggage_Default.asset`. Picking up fragile luggage grants the configured short immunity so carry alignment does not break it.

## Timer prefab

Every luggage prefab nests `Assets/Prefab/UI/Luggage Timer.prefab`. `LuggageTimerDisplay` is a thin world-space UI presenter:

- a radial `Image.fillAmount` reads `LifetimeNormalized`;
- text shows remaining whole seconds;
- warning and danger colors apply at the configured thresholds;
- multi-gate luggage shows its destination number;
- the UI billboards to `Camera.main` and uses the `WorldUI` layer.

The art lives in the prefab and can be replaced without changing `Luggage` or generating meshes at runtime. See [camera](../systems/camera.md) for the overlay stack.

## Waves

`LuggageSpawner` reads only `LevelContext.CurrentConfig`. It waits `waveDelay`, then spawns `luggagePerWave` items at `intraWaveInterval`, picking a random prefab from the pool for each. With more than one gate, each item receives a random active gate number.

All pacing and prefab pools are documented in [level configuration](../levels/level-configuration.md).

## Pooling and identity

The spawner pools by `sourcePrefab`. `RentLuggage` restores transform/activation; `Initialize` resets all mutable state. `ReturnLuggage` is an instance method and returns `false` if an object lacks a pool key, allowing `Luggage.DestroyLuggage` to log and destroy safely.

Gameplay triggers call `Luggage.TryGetFromCollider`; the component is the logic identity. Physics layers may narrow queries, but tags are not the source of truth.

`LuggageSink` recycles luggage that falls out of the play area.

## Collision audio

Collision speed selects one of three ground or window/glass clips, throttled by the tuning cooldown. Clip IDs come from `Sfx.LuggageCollision`, and `AudioManager` warns once when an ID is unregistered. See [audio](../systems/audio.md).
