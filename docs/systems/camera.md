# Camera

**Scripts:** `Game/Core/MultiplayerCamera.cs`, `WorldUIOverlayCamera.cs` — **Prefab:** `Assets/Prefab/Manager/Main Camera.prefab`

## Multiplayer follow

`MultiplayerCamera` discovers `PlayerInput` objects, sorts them by player index, removes destroyed references, and frames the active group.

- One player: center on that player.
- Several players: center on their world bounds.
- Height: interpolate between configured Y limits using group spread.
- Isometric offset: interpolate between minimum/maximum diagonal offsets as height changes.
- Motion: `SmoothDamp` toward the target pose.

Players persist between scenes, so the camera resolves the set for each stage rather than storing prefab references.

## World UI overlay

`WorldUIOverlayCamera` self-installs on `Camera.main`. It removes the `WorldUI` layer from the base camera, creates a child URP overlay camera that renders only `WorldUI`, and keeps projection settings synchronized.

The luggage timer prefab uses this layer so its world-space UI remains readable over level geometry. If layer 10 is removed/renamed, the system warns and world UI can be occluded.

Keep gameplay cameras on the manager prefab. Per-level camera framing tunables may be instance-specific when the level footprint genuinely differs; do not drag player transforms into the camera prefab.
