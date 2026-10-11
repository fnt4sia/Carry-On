# Save and progression

**Script:** `Game/Services/ProgressionService.cs`

Stores JSON at `Application.persistentDataPath/carry-on-save-{slot}.json`, using list-backed
serializable records compatible with `JsonUtility`.

## What is actually saved
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

## Save slots
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

## Lifetime, and why `ActiveSlot` survives a scene load
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
> `Level1` or `Level2` never calls `UseSlot`/`StartNewGame`, so `ActiveSlot` is still its default.
> Harmless for testing — just don't read slot 1 as "the player's real save" when debugging.

## When it writes
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

## For stage select
`LevelNode` reads `IsUnlocked` and `GetBestStars` from the active slot and refreshes on
`ProgressChanged`, so a node's lock and star count follow the chosen save with no extra wiring.

Unlocking is driven by `LevelConfig.nextLevel`, not by an index — finishing `stage-1` unlocks
`stage-2` because Stage 1's config points at Stage 2's. Current `levelId`s are all unique
(`design-sandbox`, `stage-1`…`stage-4`, `tutorial`), which is what keeps slots coherent.

> The chain stops at `stage-2`: its `nextLevel` is empty while Level3 is archived. A save from
> before the archive may already hold `stage-3`/`stage-4` unlocked; clicking those nodes logs
> `Scene 'Level3' is not enabled in Build Settings.` and does nothing. See
> [levels](../scenes/levels.md#config--scene-mapping).

Don't change a shipped `levelId` casually: the old record stops matching and the player silently
loses progress. Corrupt or unreadable JSON falls back to a fresh in-memory save with a warning.

> **No migration was written.** The pre-slot save was `carry-on-save.json` with no suffix, and
> nothing looks for that name any more — an existing one is ignored, not imported. Fine while the
> game is unreleased; if that changes, read the old file into slot 1 before this ships.

## If you add per-player data
The shape to extend is `SaveData` in `ProgressionService`. Two constraints, both from `JsonUtility`:
it will not serialise a `Dictionary`, which is why `levels` is a `List`; and it needs `[Serializable]`
on any nested class. Bump `version` when the shape changes — nothing reads it yet, but it is the
only hook a future reader has for telling old files from new.
