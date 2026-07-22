# Level authoring

Use this workflow for a new gameplay level or when cleaning an existing one.

## Required scene structure

1. Create a `LevelConfig` with a unique stable `levelId`, exact Build Settings scene name, display copy, balance, luggage pool, unlock state, and `nextLevel`.
2. Add one root `Level Context` and assign only that config.
3. Add a fresh `GameManager.prefab` instance. Its nested `GameHUD` already owns all UI references.
4. Add a fresh `Spawner.prefab` instance. It reads the level context.
5. Add `PlayerSpawner` and author four ordered spawn points.
6. Add camera, delivery gates, sink/void coverage, conveyors, and the stations required by the config's luggage pool.
7. Add the scene to Build Settings before routing a node or `nextLevel` to it.

Do not place per-level `AudioManager`, `PlayerSystem`, `SceneLoader`, or `ProgressionService` objects. Persistent services bootstrap automatically.

## Station/content matching

- Sticky in the pool requires a washer.
- Fragile requires a wrapper.
- With multiple gates, every gate number must be unique and readable.

The current stage arrangement is documented in [machine stations](../gameplay/machine-stations.md).

## Prefab overrides

Keep scene overrides for transform, gate number, intentional turn direction, spawn points, and connected interactables. A turn's internal pivot stays wired in its prefab; a different curve uses a named variant. Do not override shared tuning values or add a level config to reusable prefabs. Apply a single intended property from Unity's Overrides panel; avoid applying every scene override from a configured instance.

If a prefab update leaves an old property override, compare/revert it explicitly. See [asset and prefab guidelines](../core/asset-and-prefab-guidelines.md).

## Conveyor and route QA

- Verify deck height and uniform scale at every seam.
- Confirm `BeltSurface.physicMaterial` is on each solid deck.
- Confirm triggers cover the visible route and exit velocity lands on the next surface.
- Run Normal, Sticky, and Fragile luggage through applicable routes.
- Check station output clearance and that a processed bag can rejoin the route.
- Check sink volumes recycle missed luggage without catching valid deliveries.

## Play Mode acceptance

- zero missing-script/reference errors on load;
- correct level name/timer/pool and one active HUD;
- 1–4 real players spawn at distinct points;
- pause/resume restores time scale;
- each required station accepts a bag, processes it, and releases a grabbable result;
- score and real per-player delivery counts update;
- results persist stars/unlock and Next Stage loads asynchronously.

Use [DesignScene](../development/design-scene-testing.md) for mechanic iteration, then validate the actual authored scene because its geometry and overrides are unique.
