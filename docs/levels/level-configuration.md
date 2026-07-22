# Level configuration

**Scripts:** `Game/Core/LevelConfig.cs`, `LevelContext.cs` — **Assets:** `Assets/Config/LevelConfig_*.asset`

`LevelConfig` is the source of level-wide rules and content. Each gameplay scene has exactly one `LevelContext` selecting its asset. Reusable prefabs read that context; `GameManager` and `LuggageSpawner` do not serialize their own copies. The scene still owns geometry, placement, object identity, and unique connections.

## Fields

| Group | Fields |
|---|---|
| Identity | stable `levelId`, `sceneName`, `displayName`, `description` |
| Progression | `unlockedByDefault`, `nextLevel` |
| Round | `gameTime`, three star score thresholds |
| Content | `luggagePrefabs`, `luggageLifetime` |
| Waves | `waveDelay`, `luggagePerWave`, `intraWaveInterval` |
| Scoring | correct, missing-process, expiry, and wrong-gate values |

`levelId` is a save key. Do not rename it after release without a save migration. `OnValidate` enforces positive timings and ordered star thresholds. `CalculateStars` has no test coverage — check star thresholds by hand when you change them.

Behavior is defined by each luggage prefab; there is no behavior list on `LevelConfig`. Change `luggagePrefabs` to control which processing stations a level needs.

## Current balance curve

| Level | Round | Stars | Lifetime | Delay | Count | Interval |
|---|---:|---|---:|---:|---:|---:|
| Tutorial | 180s | 20 / 40 / 60 | 45s | 12s | 3 | 2.5s |
| Stage 1 | 120s | 30 / 60 / 90 | 28s | 11s | 4 | 2.0s |
| Stage 2 | 120s | 40 / 80 / 120 | 24s | 9s | 5 | 1.7s |
| Stage 3 | 120s | 50 / 100 / 150 | 22s | 8s | 5 | 1.4s |
| Stage 4 | 120s | 60 / 120 / 180 | 19s | 7s | 6 | 1.1s |
| Design Sandbox | 999s | 10 / 30 / 60 | 30s | 10s | 5 | 3.0s |

Tutorial and Stage 1 use Normal/Sticky content; Stages 2–4 add Fragile luggage, then tighten timing and wave size. All current configs use +10 correct, -5 missing, -5 expiry, and -5 wrong-gate scoring.

Stage 1 and Tutorial are unlocked by default; later nodes are unlocked through each completed config's `nextLevel` chain.

## Editing rule

Change level-specific pace or content on the `LevelConfig` asset. Change shared feel on a tuning asset. Change only placement/identity on a scene instance. See [asset and prefab guidelines](../core/asset-and-prefab-guidelines.md).
