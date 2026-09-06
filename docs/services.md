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

On a level load, menu music fades out; `GameManager` starts gameplay music after the countdown and
fades it back out over 1.25 s in `EndRound`, so the result card plays over the Time's Up sting
instead of the gameplay loop. Menu and stage-select scenes play menu music. Fades use unscaled
time, so pause and the result screen don't freeze them — which is the whole reason the round-end
fade works at all, since `EndRound` sets `timeScale` to 0 on the same frame.

## Camera

**Scripts:** `Game/Core/ArenaCamera.cs`, `MultiplayerCamera.cs`, `WorldUIOverlayCamera.cs`
**Prefab:** `Assets/Prefab/Manager/Main Camera.prefab`

There are two camera behaviours, and which one a scene uses is a **scene override on the Main
Camera prefab instance** — the prefab itself still carries `MultiplayerCamera`.

### ArenaCamera — the gameplay camera (redesign)

`ArenaCamera` is the Overcooked-style shot: the whole playfield is in frame at all times, and the
camera never follows anyone and never zooms. It reads the pose authored in the scene on `Awake`
and only adds a slow drift on top — two sine axes on deliberately unequal periods, plus a tiny
roll — so a held frame doesn't read as a frozen image.

Drift is meant to be *below* conscious notice: at the default 0.35 units and ~34 units of viewing
distance it moves the picture about 1.5% of screen width. If you can see it as camera movement it
is too strong. Zero either amplitude to switch that half off.

**Only `Level1` uses it.** Because the shot is fixed, every spawn point, the belt and the gate must
all sit inside the frustum — moving any of them means re-checking the framing. `RebaseToCurrentPose`
exists for moving the camera at runtime; nothing calls it yet.

### MultiplayerCamera — stage select, and the not-yet-redesigned levels

`MultiplayerCamera` discovers `PlayerInput` objects, sorts them by index, drops destroyed
references, and frames the active group:

- one player → centre on them;
- several → centre on their world bounds;
- height interpolates between configured Y limits using group spread;
- the isometric offset interpolates between min and max diagonal offsets as height changes;
- motion is `SmoothDamp` toward the target pose.

Players persist between scenes, so the camera resolves its set per stage rather than storing
prefab references.

`MapController` in stage select drives it through `SetTargets`, framing the map planes instead of
players. **That is why the script still exists** — don't delete it when converting the remaining
levels to `ArenaCamera`.

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

Stores JSON at `Application.persistentDataPath/carry-on-save-{slot}.json`, using list-backed
serializable records compatible with `JsonUtility`.

### What is actually saved

**Per level, and nothing else.** One record per level played:

```json
{"version":1,"levels":[
  {"levelId":"stage-1","bestStars":3,"bestScore":95,"unlocked":true},
  {"levelId":"stage-2","bestStars":0,"bestScore":0,"unlocked":true}]}
```

`levelId` is the key. Stars and score are kept at their **maximum**, never overwritten downward.
`LevelConfig.unlockedByDefault` is honoured before any record exists.

**Nothing else in the game persists at all.** There is no `PlayerPrefs` anywhere in the project.
Specifically *not* saved, all of which matters for what you're about to build:

| Not saved | Consequence |
|---|---|
| Player count, who joined, device pairing | the lobby re-joins from scratch every launch |
| **Per-player scores and deliveries** | `GameResult` carries `PlayerScores` / `PlayerDeliveries`, but `RecordResult` **ignores them** — only the team totals survive |
| Character selection | nothing to restore; every player is `Annie` |
| Any setting | the Settings panel is a placeholder; there is no audio/graphics save |
| Mid-round state | saving happens at round end only; quitting mid-round loses the round |
| `ActiveSlot` itself | resets to 1 on relaunch — see below |

### Save slots

`ProgressionService.SlotCount` is **4**, one file each. Only the active slot is held in memory;
`ActiveSlot` (default 1) is what every read and write goes through.

| Member | Does |
|---|---|
| `ActiveSlot` | slot every read/write targets |
| `UseSlot(n)` | switch to a slot and read it in — a missing file just means empty progress |
| `StartNewGame(n)` | switch to a slot and wipe it, writing immediately so the slot exists from then on |
| `SlotHasData(n)` *(static)* | does a file exist for that slot |
| `ReadSlotSummaries()` *(static)* | one `SlotSummary` per slot, in slot order, read **off disk** |

`ReadSlotSummaries` deliberately reads every slot file rather than the loaded one — the lobby's
save list has to show all four, not just the one in memory. A slot whose JSON won't parse is
reported as **occupied**, not empty: offering it as empty would let New Game silently overwrite
something a player might still want back.

### Lifetime, and why `ActiveSlot` survives a scene load

Exactly one `ProgressionService` ever exists:

1. it is placed in **no scene** — `Bootstrap` is a `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`
   that creates it before the first scene loads, so it is present in builds and in every scene;
2. `SingletonBehaviour` keeps the **first** instance and destroys later duplicates, so a second one
   can never take over and reset `ActiveSlot` to 1;
3. `PersistAcrossScenes` puts it in `DontDestroyOnLoad`.

So the slot chosen in the lobby is still the slot written at the end of a round, two scene loads
later. Verified: instance ID unchanged after `LoadStageSelect()`, `ActiveSlot` still 3, exactly one
live instance.

> **Entering a stage without going through the lobby writes to slot 1.** Pressing Play directly in
> `DesignScene` never calls `UseSlot`/`StartNewGame`, so `ActiveSlot` is still its default. Harmless
> for sandbox work — just don't read slot 1 as "the player's real save" when debugging.

### When it writes

Only three things write to disk: `RecordResult`, `StartNewGame`, `ResetProgress`. There is no
save-on-quit and no autosave. At round end `GameManager` calls `RecordResult`, which:

1. marks the completed level unlocked;
2. keeps the maximum stars and score;
3. unlocks `LevelConfig.nextLevel` when present;
4. saves and raises `ProgressChanged`.

Writes go to a `.tmp` file that is then swapped in with `File.Replace`, so a crash mid-write cannot
leave a half-written save that `Load()` would reject and replace with empty progress.

`ResetProgress` wipes **the active slot** — expose it from an Options UI only if that's the product
behaviour you want.

### For stage select

`LevelNode` reads `IsUnlocked` and `GetBestStars` from the active slot and refreshes on
`ProgressChanged`, so a node's lock and star count follow the chosen save with no extra wiring.

Unlocking is driven by `LevelConfig.nextLevel`, not by an index — finishing `stage-1` unlocks
`stage-2` because Stage 1's config points at Stage 2's. Current `levelId`s are all unique
(`design-sandbox`, `stage-1`…`stage-4`, `tutorial`), which is what keeps slots coherent.

> Unlocking `stage-2` currently leads to a **missing scene** — Stage 2–4 and Tutorial still name
> scenes deleted in the July 2026 consolidation. See [levels](levels.md). The save layer is fine;
> the scenes are not there yet.

Don't change a shipped `levelId` casually: the old record stops matching and the player silently
loses progress. Corrupt or unreadable JSON falls back to a fresh in-memory save with a warning.

> **No migration was written.** The pre-slot save was `carry-on-save.json` with no suffix, and
> nothing looks for that name any more — an existing one is ignored, not imported. Fine while the
> game is unreleased; if that changes, read the old file into slot 1 before this ships.

### If you add per-player data

The shape to extend is `SaveData` in `ProgressionService`. Two constraints, both from `JsonUtility`:
it will not serialise a `Dictionary`, which is why `levels` is a `List`; and it needs `[Serializable]`
on any nested class. Bump `version` when the shape changes — nothing reads it yet, but it is the
only hook a future reader has for telling old files from new.
