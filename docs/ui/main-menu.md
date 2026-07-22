# Main menu

**Scripts:** `MainMenu/MainMenuManager.cs`, `InputModeManager.cs` — **Scene:** `Assets/Scenes/Menu/MainMenu.unity`

The lobby begins with a join prompt. The first successful join reveals the menu and eases the camera from the start view to the lineup view. Returning with persisted players skips the intro and immediately rebuilds the lineup.

Joined characters are arranged as a centered row, frozen with kinematic rigidbodies, colored by player index, and paired with one `PlayerUI.prefab` card each. A shared join card lists only currently available keyboard halves and gamepads. Starting with zero players is rejected with the wrong-action SFX; the Options handler remains a placeholder.

`InputModeManager` switches between:

- pointer mode for mouse movement/clicks, with no selected UI object;
- navigation mode for gamepad activity, with a default selected button.

Keyboard players use the mouse for menu UI. Gamepad players use navigation. The `LastJoinFrame` guard prevents the join press from immediately activating a selected button.

Start unfreezes persisted players and uses `SceneLoader.LoadStageSelect`. Player/device ownership is documented in [join and spawn](../player/join-and-spawn.md).
