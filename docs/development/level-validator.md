# Level validator

**Script:** `Assets/Script/Editor/LevelValidator.cs` (assembly `CarryOn.Editor`, editor-only)

An automated check for the things that are easy to forget when authoring a level. It replaces "remember to wire it" with a console error you cannot miss.

## How to run it

| Menu | What it does |
|---|---|
| **Carry On ▸ Validate Open Scene** (`Ctrl/Cmd + Shift + V`) | validates the scene you are looking at |
| **Carry On ▸ Validate All Build Scenes** | opens every enabled build scene in turn, validates it, restores your scene |

Every finding is logged with the offending object as its **context**, so clicking the console entry selects it in the hierarchy. Scenes without a `LevelContext` (MainMenu, ChooseStage) are skipped automatically.

Errors mean the level is broken. Warnings mean it will run but something is probably wrong.

## What it checks

**Level wiring**
- Exactly one `LevelContext`, and it has a `LevelConfig`.
- The config's `sceneName` matches the scene it is actually used in (catches a config pointed at the wrong stage).
- The config has at least one luggage prefab, no empty slots, and every entry has a `Luggage` component.
- The scene has a `GameManager`, a `LuggageSpawner`, a `LuggageSink`, a `PlayerSpawner` with spawn points, and at least one `Gate`.
- Gate numbers are unique (destination routing depends on it).

**Stations match the content the level actually spawns** — the check that prevents an unwinnable level:
- Spawns `Sticky` → a `WashingMachine` must exist.
- Spawns `Fragile` → a `Wrapper` must exist.
- Each station has its `StationTuning`, `snapTransform`, `sliderTransform`, and (when `animationDriven`) an `Animator`. A station with no animator never finishes processing and jams its slot forever.
- Washer/Wrapper without a replacement prefab → warning (it silently falls back to an in-place swap).

**Conveyor seam rules** — see [../gameplay/conveyors.md](../gameplay/conveyors.md):
- Every `Conveyor` has a `ConveyorTuning`, or it will not steer at all.
- A `Turn` piece has a `turnPivot`, or it steers straight through the corner.
- Pieces are **uniformly scaled** — a non-uniform scale changes the deck height and breaks the seam.
- Every deck collider carries the `BeltSurface` physic material (friction otherwise fights the velocity steering).
- All pieces in the scene sit at the **same deck height** (`position.y + 4.0 × scale.y`). Different heights mean a step at the join that luggage catches on.

## Known finding

`DesignScene` currently reports **two deck heights** (`y=0.23` on 5 pieces, `y=0.18` on 2) — a ~5 cm step in the legacy `ConveyorTemplate` rig. All five build scenes are clean.

## Extending it

Add a check whenever you find yourself saying "I forgot to…". The rule of thumb: if a mistake can be described numerically or as a missing reference, it belongs here. If it needs eyes (does the route *feel* good, is the camera framing nice), it does not — that stays manual QA.
