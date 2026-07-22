# Stage select

**Scripts:** `ChooseStage/MapMover.cs`, `LevelNode.cs`, `LevelInfoPopup.cs` — **Scene:** `Assets/Scenes/Menu/ChooseStage.unity`

An airplane token moves over `LevelNode` instances. Parking within the detection radius presents the level card; Confirm loads an unlocked level, and Back returns to the lobby.

## Input and player state

`MapMover` reads `Map/Move`, `Map/Confirm`, and `Map/Back` from the shared `GameInput.inputactions` asset. Input is shared, so any joined device can drive the token. Movement has constant speed and snaps to face direction.

Persisted player GameObjects are deactivated while the map is open. Before either scene transition they are reactivated, allowing the target gameplay `PlayerSpawner` to position them.

## Nodes and progression

Each `LevelNode` references one `LevelConfig`. Display strings, scene name, default unlock, save key, and next-stage relationship all come from that asset. The node queries `ProgressionService` for unlocked state and best stars, and owns only its locked/unlocked visuals.

Confirming a locked node plays the wrong-action SFX. Confirming an invalid config logs an error. Valid loading is delegated to the asynchronous `SceneLoader`, guarded against double submission.

UI presentation is detailed in [stage-select UI](../ui/stage-select-ui.md); save behavior is in [save and progression](../systems/save-and-progression.md).
