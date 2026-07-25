# Change the character body

Swaps the body every joined player spawns as. One field, one drag.

## Steps

1. In the Project window open `Assets/Prefab/Manager/PlayerSystem.prefab`.
2. Select the root object → Inspector → **Player System** component.
3. Find the **Character Prefab** field (under the *Character* header).
4. Drag a character prefab from `Assets/Prefab/Character/` into it (e.g. `Bubble`).
5. Save the prefab (Ctrl/Cmd+S).

Press Play in `DesignScene` — both players now spawn as that body.

## What the field does

`PlayerSystem` copies this into the `PlayerInputManager.playerPrefab` at startup, before
any player joins (`Assets/Script/MainMenu/PlayerSystem.cs`). It overrides the manager's
own *Player Prefab* field, which Unity hides while Join Behavior = *Manual*. So you never
need Debug inspector mode or to flip the join behavior.

## Requirements for a new character prefab

The body must carry the same player components, or controls break:

- `PlayerInput`
- `PlayerMovement`
- `PlayerGrab`
- `Rigidbody`

`Ramp Agent Body` is the reference rig — duplicate it and reskin rather than building a
character from scratch.

## Limits

- One prefab for **all** players — no per-player bodies in the current setup. That would
  be a feature change (a spawn-time swap), not a config tweak.
