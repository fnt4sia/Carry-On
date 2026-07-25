# Tune a level

Everything per-level — timer, spawn waves, scoring, star goals, which luggage appears —
lives in one `LevelConfig` asset. The scene's `LevelContext` object picks which one is
active, so editing an asset only affects the levels that use it.

## Which asset does a scene use?

Open the scene → select the `LevelContext` object → its **Level Config** field names the
asset. `DesignScene` uses `Assets/Config/LevelConfig_Design.asset`. Stages use
`LevelConfig_Stage1..4`.

## Fields — `LevelConfig` (`Assets/Script/Game/Core/LevelConfig.cs`)

**Identity & progression**
| Field | What it does |
|---|---|
| Level Id | Stable save key. **Don't change after shipping** — it's how progress/stars are stored. |
| Scene Name | Scene as listed in Build Settings. |
| Display Name / Description | Shown in stage select. |
| Unlocked By Default | Playable without beating a prior level. |
| Next Level | The `LevelConfig` unlocked on completion. |

**Timer & stars**
| Field | Default | What it does |
|---|---|---|
| Game Time | 120 | Round length in seconds. |
| Star 1 / 2 / 3 Score | 30 / 60 / 90 | Score needed for each star (must ascend). |

**Spawner**
| Field | Default | What it does |
|---|---|---|
| Luggage Prefabs | — | Pool that can spawn. Each prefab defines its own behavior (wash/wrap/etc). Add a type by adding its prefab here. |
| Luggage Lifetime | 20 | Seconds before an undelivered bag expires. |
| Wave Delay | 10 | Seconds between waves. |
| Luggage Per Wave | 5 | Bags per wave. |
| Intra Wave Interval | 1.5 | Seconds between bags inside a wave. |

**Scoring** — ⚠️ see warning below.
| Field | Default | What it does |
|---|---|---|
| Score Correct Delivery | 10 | Points for a correct, fully-processed delivery. |
| Score Missing Process | -5 | Penalty for delivering an unwashed/unwrapped bag. |
| Score Timer Expired | -5 | Penalty when a bag expires. |
| Score Wrong Gate Delivery | -5 | Penalty for the wrong numbered gate (multi-gate levels). |

## ⚠️ Scoring has no safety net

A wrong scoring number is **invisible during playtesting** — the game just feels unfair,
nothing errors. After changing any scoring/star field, re-check the math by hand: play a
round, tally expected vs actual score. Star thresholds auto-clamp to ascend (1 ≤ 2 ≤ 3).

## Add a new luggage type

1. Make (or duplicate) a luggage prefab under `Assets/Prefab/` with its behavior set.
2. Add it to the level's **Luggage Prefabs** list.
3. Play the level and confirm it spawns and routes/processes correctly.

No code needed — the spawner pulls from this list.
