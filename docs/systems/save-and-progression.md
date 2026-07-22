# Save and progression

**Script:** `Game/Services/ProgressionService.cs`

`ProgressionService` is a persistent bootstrapped service. It stores JSON at `Application.persistentDataPath/carry-on-save.json` using list-backed serializable records compatible with `JsonUtility`.

Each level record contains stable `levelId`, best stars, best score, and unlocked state. `LevelConfig.unlockedByDefault` is honored even before a record exists.

At round end, `RecordResult`:

1. marks the completed level unlocked;
2. preserves the maximum stars and score;
3. unlocks `LevelConfig.nextLevel` when present;
4. saves and raises `ProgressChanged`.

`LevelNode` reads unlocked state and best stars from this service and refreshes its visuals when progress changes. `ResetProgress` creates and writes a fresh save; expose it from an Options UI only when that product behavior is desired.

Do not change a shipped `levelId` casually: the old save record would no longer match. A rename requires explicit migration/version handling. Corrupt or unreadable JSON falls back to a fresh in-memory save with a warning.
