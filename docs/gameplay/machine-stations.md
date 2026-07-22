# Machine stations

**Scripts:** `Game/Stations/MachineStation.cs`, `WashingMachine.cs`, `Wrapper.cs`

All stations share an explicit `Idle -> Processing -> Ready -> Idle` lifecycle.

| Station | Accepts | Completion |
|---|---|---|
| `WashingMachine` | Sticky | swaps to washed luggage and sets `IsWashed` |
| `Wrapper` | Fragile | swaps to wrapped luggage and sets `IsWrapped` |

Reusable prefabs are `WashingMachine.prefab` and `WrapperMachine.prefab`. Stage Tutorial and Stage 1 contain a washer only (they spawn no Fragile luggage); Stages 2–4 contain both. The [level validator](../development/level-validator.md) enforces that a level has the stations its luggage pool requires.

> A `Scanner` station existed until 2026-07-14 and was removed along with the bomb mechanic. Adding a third station type later means subclassing `MachineStation` and overriding `CanAccept` plus the completion step — the lifecycle below needs no changes.

## Placement and lifecycle

1. `PlayerGrab` finds a nearby station, verifies `CanAccept`, and calls `TryPlace`.
2. The station clears its output area, force-drops the held item, zeros its body, makes it kinematic, snaps it to the station, and pauses its timer.
3. Phase changes to `Processing`; progress updates from 0 to 1.
4. Completion runs the station operation and changes phase to `Ready`.
5. Output completion restores a dynamic rigidbody, resumes the timer, resets the animator, and returns the station to `Idle`.

While occupied, a station rejects another item. The luggage follows `sliderTransform` in `LateUpdate`, so animated doors and trays can move it safely.

## Animation or timed prototype

Production machines can set `animationDriven` and call:

- `AnimEvent_OnDoorClosed` when processing completes;
- `AnimEvent_OnOutputComplete` when the output can return to physics.

Prototype machines can disable animation-driven timing (or omit an animator) and use `processDuration` plus `readyDuration`. This lets art/animation be replaced without changing the gameplay state machine.

Animator parameters use cached `AnimId` hashes, including `AnimId.IsTriggered`.

## Feedback

There is currently **no station feedback visual**. A `StationFeedback` prefab (a billboarded PROCESSING/READY label and progress bar above each machine) existed until 2026-07-14 and was removed with the bomb/scanner work — it was prototype UI, not a requested feature.

The station still tracks its `Idle -> Processing -> Ready` phase internally, so any future feedback art can read that without changing the state machine.

## Output clearance and tuning

Before placement, an overlap box finds other free luggage in the output zone and shoves it away. Half extents, impulse, upward impulse, and randomness come from `Station_Default.asset`. Snap/slider transforms, animator, process timing, and replacement prefab remain station-specific internal references.
