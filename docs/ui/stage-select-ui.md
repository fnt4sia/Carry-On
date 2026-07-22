# Stage-select UI

**Scripts:** `ChooseStage/LevelInfoPopup.cs`, `LevelNode.cs` — **Scene:** `Assets/Scenes/Menu/ChooseStage.unity`

One reusable popup card follows the currently detected `LevelNode`. It fills title and description from the node's `LevelConfig`, slides and fades in, and hides when the map token leaves.

Unlocked cards show the configured description and best saved stars. Locked cards append a locked label and explain that the previous stage must be completed. Re-showing the same node is ignored so the animation does not restart each frame.

Each `Level Node.prefab` instance owns references to an unlocked beacon and locked visual. `LevelNode.Refresh` switches them in response to `ProgressionService.ProgressChanged`.

Map controls and loading behavior are documented in [stage select](../levels/stage-select.md); persistence is in [save and progression](../systems/save-and-progression.md).
