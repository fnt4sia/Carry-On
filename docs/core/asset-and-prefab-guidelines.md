# Asset and prefab guidelines

The project uses prefabs for shared structure and behavior, ScriptableObjects for shared or per-level data, and scene overrides only for placement and genuinely unique wiring. This is the answer to the common problem where applying one level's prefab instance breaks every other level.

## Data ownership

| Data kind | Store it in | Examples |
|---|---|---|
| Shared logic and child wiring | Base prefab | colliders, animator, HUD texts, machine children |
| Shared gameplay feel | Tuning ScriptableObject | player speed, grab joint, luggage physics, conveyor movement, station shove |
| Per-level rules/content | `LevelConfig` | timer, stars, luggage pool, waves, scoring, next level |
| Per-level config selection | scene `LevelContext` | Stage 2 points to `LevelConfig_Stage2` |
| Per-instance placement/identity | scene override | transform, gate number, intentional turn direction, connected gateways |
| Replaceable art | visual child/nested prefab | machine shell, luggage timer, loading screen |

## Safe prefab workflow

1. Open the prefab in Prefab Mode when changing shared behavior or internal references.
2. Apply only changes that should be identical everywhere.
3. Leave scene transforms and unique instance fields as overrides. In Unity's Overrides panel, apply individual intended properties instead of `Apply All` from a configured level instance.
4. If the same group of different values appears in many scenes, move it into a ScriptableObject instead of accumulating overrides.
5. Revert stale overrides after a prefab field moves to a config asset; otherwise old serialized values remain hidden on instances.

Do not add a serialized `LevelConfig` field back to `GameManager`, `LuggageSpawner`, stations, gates, or other reusable prefabs. The scene's one `LevelContext` owns that choice.

Use this scope ladder when deciding where a value belongs:

1. One placed object only: keep a narrow scene override.
2. Every object in one level: add the value to `LevelConfig` (or a deliberate level-profile asset if it is a separate mechanic family).
3. A reusable family, such as fast conveyors or a two-door gateway: use a tuning asset and/or named prefab variant.
4. Every instance everywhere: change the base prefab or shared script.

If several objects in one stage need the same non-rule settings, do not repeat those settings across scene instances. Introduce a typed profile selected once by the level, then let the reusable components read it.

## Existing tuning assets

`Assets/Config/Tuning/` contains:

- `Player_Default` -> `PlayerConfig`
- `Grab_Default` -> `GrabConfig`
- `Luggage_Default` -> `LuggageTuning`
- `Conveyor_Straight` / `Conveyor_Turn` -> `ConveyorTuning`
- `Station_Default` -> `StationTuning`

Create a new tuning asset only for a deliberate reusable variant, such as a fast conveyor family. Do not duplicate an asset merely to make one scene object unique unless that difference is intentional content.

## Base prefabs, variants, and nested visuals

Use a base prefab for the mechanic. Use a prefab variant when a family shares structure but has a stable intentional difference. Keep prototype art under a visual child or nested visual prefab so the model can be replaced without disturbing scripts, colliders, events, or config references.

Examples already following this split:

- `GameManager.prefab` owns round wiring and nests `GameHUD.prefab`.
- `WashingMachine.prefab` / `WrapperMachine.prefab` own station logic while their machine shells are replaceable.
- `GameHUD.prefab` and `Luggage Timer.prefab` are presentation layers.
- persistent runtime prefabs under `Assets/Resources/Runtime/` are variants of authoring prefabs under `Assets/Prefab/Manager/`.

When importing an FBX, preserve the artist-authored root rotation and scale. Recenter it through a child/local position or wrapper; do not normalize imported transforms by guesswork.

Several legacy character, luggage, gate, and gateway prefabs still lack this logic/visual split — their scripts and art sit on the same root. Normalize them only alongside deliberate art or pivot work, not as a standalone refactor, since moving a root is exactly what breaks authored FBX transforms. Note also that a stale override left on an instance after a field moves into a config asset is still **not** detected automatically; the [level validator](../development/level-validator.md) checks scene invariants, not override drift.

## Level scene checklist

Each gameplay scene should have exactly one `LevelContext`, a `GameManager` prefab instance, a spawner prefab instance, a `PlayerSpawner` with up to four points, a camera, gates, and only the stations required by that level's luggage pool. See [level authoring](../levels/level-authoring.md) for the complete checklist.
