# Services

The four persistent services plus the camera rig. All of them bootstrap before any scene loads,
so they never appear in a hierarchy — to edit one, open its prefab under
`Assets/Resources/Runtime/`. Never place a per-level copy of any of them.

## Audio

**Scripts:** `Game/Core/AudioManager.cs`, `Sfx.cs` — **Prefab:** `Resources/Runtime/AudioManager.prefab`

`AudioManager` builds a case-insensitive clip dictionary, one music source, and a small one-shot
SFX source pool. Retained looping effects get dedicated sources, so a busy one-shot can't steal
them.

| Method | Use |
|---|---|
| `PlaySFX(id, volume)` | one-shot on an available source |
| `PlayLoopingSFX(id, volume)` | returns a source for charged or looped effects |
| `StopSFX(source)` | releases a loop |
| `PlayMusic(id)` | cross-fades to a registered clip |
| `StopMusic(duration)` | unscaled fade out |
| `PlayMenuMusic` / `PlayGameplayMusic` | named scene modes |

If every one-shot source is busy, source 0 is reused. A missing clip ID warns once instead of
failing silently.

Gameplay code uses constants from `Sfx`, never raw strings: Start, Time's Up, Star, Stamp, Score,
Wrong, Button Select, dash, throw buildup and release, and the three ground/window luggage
collision tiers. `Sfx.LuggageCollision` builds the tiered collision ID in one place. Menu and
game music names are serialized service fields, because they select registered clips rather than
one-shot IDs.

When adding audio, register the clip on the runtime prefab and add or reuse a constant in
`Sfx.cs`. Don't scatter a new literal through callers.

On a level load, menu music fades out; `GameManager` starts gameplay music after the countdown.
Menu and stage-select scenes play menu music. Fades use unscaled time, so pause and the result
screen don't freeze them.

## Camera

**Scripts:** `Game/Core/MultiplayerCamera.cs`, `WorldUIOverlayCamera.cs`
**Prefab:** `Assets/Prefab/Manager/Main Camera.prefab`

`MultiplayerCamera` discovers `PlayerInput` objects, sorts them by index, drops destroyed
references, and frames the active group:

- one player → centre on them;
- several → centre on their world bounds;
- height interpolates between configured Y limits using group spread;
- the isometric offset interpolates between min and max diagonal offsets as height changes;
- motion is `SmoothDamp` toward the target pose.

Players persist between scenes, so the camera resolves its set per stage rather than storing
prefab references.

`WorldUIOverlayCamera` self-installs on `Camera.main`. It removes the `WorldUI` layer from the
base camera, creates a child URP overlay camera that renders only `WorldUI`, and keeps projection
synchronised. The luggage timer uses this so its world-space UI stays readable over level
geometry. If layer 10 is removed or renamed, the system warns and world UI can be occluded.

Per-level framing tunables may legitimately differ when a level's footprint does. Never drag
player transforms into the camera prefab.

## Scene loading

**Scripts:** `Game/Services/SceneLoader.cs`, `Game/Core/SceneNames.cs`
**Prefab:** `Resources/Runtime/SceneLoader.prefab`

`SceneLoader` is the only application-level transition path. It rejects concurrent loads,
validates the target against Build Settings, and runs an async transition:

```text
activate loading canvas -> unscaled fade in
  -> LoadSceneAsync with activation held
  -> update progress -> activate at 90%
  -> reset timeScale -> unscaled fade out -> hide canvas
```

Static helpers load the lobby and stage select through names centralised in `SceneNames`. Menu,
map, HUD, and round buttons all delegate here — never add a direct synchronous
`SceneManager.LoadScene`.

The runtime prefab supplies a swappable loading-screen visual. If it or its references are
unavailable during direct testing, the service builds a minimal overlay fallback so mechanics
stay testable without presentation assets.

Fades use `Time.unscaledDeltaTime`, so transitions work from pause and from the time-frozen
result screen. `Time.timeScale` is restored after activation.

## Save and progression

**Script:** `Game/Services/ProgressionService.cs`

Stores JSON at `Application.persistentDataPath/carry-on-save.json`, using list-backed
serializable records compatible with `JsonUtility`. Each record holds a stable `levelId`, best
stars, best score, and unlocked state. `LevelConfig.unlockedByDefault` is honoured before any
record exists.

At round end, `RecordResult`:

1. marks the completed level unlocked;
2. keeps the maximum stars and score;
3. unlocks `LevelConfig.nextLevel` when present;
4. saves and raises `ProgressChanged`.

`LevelNode` reads unlocked state and best stars from here and refreshes on `ProgressChanged`.
`ResetProgress` writes a fresh save — expose it from an Options UI only if that's the product
behaviour you want.

Corrupt or unreadable JSON falls back to a fresh in-memory save with a warning. Don't change a
shipped `levelId` casually: the old record stops matching and the player silently loses progress.
