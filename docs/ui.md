# UI

**Scripts:** `MainMenu/{MainMenuManager, InputModeManager, BoardMenuRow, UIPulse, PlayerSlotView}.cs`,
`ChooseStage/{MapMover, LevelNode, LevelInfoPopup}.cs`, `Game/UI/GameHUD.cs`

**Every UI element is authored in the scene or in a prefab — never built in code.** No
`new GameObject()`, no `AddComponent<Image>()`, no `Instantiate` for UI. The only exception is a
runtime copy of a prefab that already exists as a project asset, and even then that prefab is
authored, not generated. Lists use layout groups; alignment uses anchors and pivots, never pixel
offsets that die on an aspect-ratio change.

## Main menu

**Scene:** `Assets/Scenes/Menu/MainMenu.unity`

There is **no screen-space canvas and no camera movement**. One fixed `Main Camera` pose frames
both the departure board and the character lineup, and the entire menu is a world-space canvas
drawn onto the board's screen. The old `Canvas`, `StartView`, and `MenuView` objects were deleted
in the August 2026 board-menu rebuild, along with the per-player `PlayerUI.prefab` cards.

`DepartureBoard/BoardCanvas` is a World Space canvas parented to the board, sized to the dark
screen quad of `DepartureBoard.fbx` (27.6 × 16.0 world units) and floated just in front of it so
it clears the plane icon baked into the mesh:

| | |
|---|---|
| localPosition | `(0.05, 9.265, 0.125)` — board-local, screen plane is `x = 0.11` |
| localRotation | `(0, 90, 0)` — puts canvas `+Z` into the board, so `+X` reads screen-right |
| localScale | `0.01`, with `sizeDelta (3067, 1777)` |
| `worldCamera` | `Main Camera` — required, or the `GraphicRaycaster` can't take mouse clicks |

The board has two states, toggled by `MainMenuManager`. `Content/Title` is always on.

- **Before anyone joins** — `Content/JoinPrompt` shows `Press Space / (A) to Join`, breathing
  between alpha 0.3 and 1 on a 1.6 s cycle via a `CanvasGroup` + `UIPulse`. `UIPulse` runs on
  unscaled time so it keeps going at `timeScale` 0.
- **After the first join** — `Content/MenuRoot` reveals the column headers and the four rows.
  The free-device hint lives on the player list panel, not the board.

> **`DepartureBoard` is a prefab instance** (`Assets/Prefab/Decoration/DepartureBoard.prefab`),
> and `BoardCanvas` is inside it. Unity **silently refuses** `SetParent` on a child of a prefab
> instance — the console says *"Setting the parent of a transform which resides in a Prefab
> instance is not possible"* and the object simply stays put. To move or delete anything under
> `BoardCanvas`, edit the prefab asset; restructuring it from the scene will not work.

### The grid is painted, not drawn

**Do not add Image rules or dividers to this canvas.** Every line — the thick bar under the
title, the four row rules, the two column dividers — is already baked into
`Assets/Material/Individual Assets/Group 292.png`, the 644×407 texture on the screen quad. So is
the plane icon left of DEPARTURES (that one is a separate mesh quad, submesh 2). The UI is laid
out *onto* those lines.

The quad's UVs run `u 0→1` across board-local `z 15.46 → −15.21` and `v 0→1` across
`y 0.38 → 18.15`, which maps to canvas units as:

```text
canvasX = -1533.5 + u * 3067        canvasY = -888.5 + v * 1777
```

Measured landmarks, in canvas units — anchor to these:

| Landmark | Value |
|---|---|
| Grid box, left / right | `X −1402.5` / `1402.5` (so `Content` insets 131 each side) |
| Thick bar under title | `Y 392.9 … 423.5` |
| Row rules | `Y 170.3`, `−61.1`, `−296.9`, `−528.3` |
| Grid bottom (dividers end) | `Y −781.5` |
| Column dividers | `X −685.8` and `638.2` → fractions `0.25551` and `0.72752` |

Because the painted cells are not equal (231.4 / 235.8 / 231.4 / 253.2), the `Rows`
`VerticalLayoutGroup` runs at spacing 0 with `childForceExpandHeight` **off**, and each row
instance carries a `LayoutElement.preferredHeight` for its own cell. They sum to exactly 951.8.

The strip below the grid is unusable — the board's bezel geometry clips it — which is why the
join hint sits beside the title instead.

Joined characters are arranged in a centred row, frozen with kinematic rigidbodies, facing the
camera. Starting with zero players is rejected with the wrong-action SFX.

`InputModeManager` switches between pointer mode (mouse movement and clicks, nothing selected)
and navigation mode (gamepad activity, `Row_NewGame` selected). Keyboard players use the mouse
for menu UI; gamepad players navigate. The `LastJoinFrame` guard stops a join press from
immediately activating whatever row is selected.

### Board rows

Each row is an instance of `Assets/Prefab/UI/BoardMenuRow.prefab` under a `VerticalLayoutGroup`.
Flight code, destination, `Button.interactable`, and the `onClick` target are scene overrides:

| Row | Flight | Destination | Handler | Does |
|---|---|---|---|---|
| `Row_NewGame` | C0110 | NEW GAME | `OnClickNewGame` | loads stage select |
| `Row_LoadGame` | C0111 | LOAD GAME | `OnClickLoadGame` | nothing yet |
| `Row_Settings` | C0112 | SETTINGS | `OnClickSettings` | nothing yet |
| `Row_Exit` | C0113 | EXIT | `OnClickExit` | quits |

All four are `interactable` and look identical — Load Game and Settings highlight and click like
any other row, their handlers are just empty bodies waiting for a destination. Nothing reads
`DELAYED` any more; `BoardMenuRow.lockedStatus` is dormant until some row is disabled again.

Selection feedback: the row's own `Image` is the `Button`'s ColorTint target, and
highlighted / selected fill the painted cell with **bronze** `(0.455, 0.302, 0.125)` — the same
tone as the title text, chosen to sit in the environment rather than glare out of it. Pure scene
data, no code. `BoardMenuRow` adds what a ColorBlock can't reach: all three texts flip to cream
`(0.96, 0.91, 0.82)` so they read on the bronze, the cream `SelectionMarker` notch appears, and
STATUS flips `ON TIME → BOARDING`.

Idle text colours are read from the components at `Awake`, never hard-coded — so recolouring a
row stays a prefab or scene edit. The corollary: **if you preview a selected row by hand-editing
its text colours in the editor, put them back**, or that dark preview colour becomes the row's
idle colour and the text vanishes against the board.

Navigation is left on **Automatic** deliberately. Explicit navigation would happily land on a
locked row — `Selectable.Navigate` only checks `IsActive()`, not `IsInteractable()` — whereas
Automatic filters non-interactable entries, so gamepad up/down steps NEW GAME ↔ EXIT and starts
routing through LOAD GAME and SETTINGS the moment they are enabled.

New Game unfreezes persisted players and calls `SceneLoader.LoadStageSelect`. Device ownership is
in [player](mechanics/player.md#joining).

`PlayerSpawnTransform` is the lineup anchor — move that object to reposition the characters.

### Player list

**Scene:** root `PlayerListCanvas` · **Prefab:** `Assets/Prefab/UI/PlayerSlot.prefab`

The only screen-space UI in the lobby: a Screen Space - Overlay canvas anchored bottom-right,
inactive until the first join, then showing four avatars — white for a joined slot, grey for a
free one. `MainMenuManager.playerSlots` holds the four `PlayerSlotView`s in P1–P4 order and
`RefreshPlayerList()` tints them off `joinedPlayers.Count`.

`PlayerListPanel/JoinHint` sits along the bottom-left of the same panel and lists the keyboard
halves and gamepads still free. Head, body and the hint are all anchored in **fractions of their
parent**, so resizing the panel rescales everything — the avatar Images keep `preserveAspect`
on, which letterboxes them inside their slot rather than squashing them. Content is inset past
the 28 px 9-slice border; anything closer than that draws over the frame.

It deliberately has **no `GraphicRaycaster`** — it is display only and must not swallow clicks
meant for the board.

The three sprites in `Assets/UI/` are named misleadingly; check the silhouettes, not the names:

| File | Size | Actually is |
|---|---|---|
| `Multiplayer1.png` | 41×40 | the **head** — rounded shape with two eye holes |
| `Multiplayer2.png` | 289×172 | the panel background |
| `Multiplayer3.png` | 49×30 | the **body** — wide shoulders, arch cut out below |

Both head and body are white silhouettes on transparent, drawn to be tinted. The body is a
sibling *before* the head so the shoulders render behind the face.

### Logo

Scene root `LogoCanvas` — a **Screen Space - Overlay** canvas holding
`Assets/UI/CarryOnLogo.png`, anchored to the left edge (`anchoredPosition (463, 110)`,
`710 × 262`, sprite aspect 2.713). `sortingOrder -10` keeps it under the player list.

**It must not be a World Space canvas.** It started as one and the outlines of scene objects
behind it drew straight over the artwork. The cause is render order, not parenting:

1. `URP-HighFidelity-Renderer` runs a `FullScreenPassRendererFeature` with the
   `OutlinePostProcessing` material at injection point **`AfterRenderingPostProcessing`** — the
   last thing in the frame. That pass is where the game's whole outlined look comes from.
2. A World Space canvas is ordinary transparent geometry (`UI/Default`, render queue 3000) drawn
   during the camera's normal loop, well before that pass.
3. `UI/Default` has **`ZWrite Off`**, so the logo never appears in the depth/normals buffers.

So the outline pass edge-detects only the geometry *behind* the logo and paints those edges over
the finished image. Overlay canvases are composited to the backbuffer after the whole SRP loop,
renderer features included, so they are immune.

If the logo ever needs to be diegetic (occluded by the terminal, catching the world's light),
World Space is the right mode but the outline feature has to be dealt with too — the artifact is
not fixable by moving, reparenting, or re-sorting the canvas.

### Panel sprite

`Multiplayer2` is imported with a **28 px 9-slice border** and the panel Image is `Sliced` with
`preserveAspect` **off**, so the panel resizes freely without distorting the rounded corners.
Leave `preserveAspect` off here — on a `Simple` Image it letterboxes the sprite to its native
1.68 aspect inside the rect, which looks exactly like "the panel refuses to get wider". Keep it
**on** for the head and body, which do need their proportions.

## Stage select

**Scene:** `Assets/Scenes/Menu/ChooseStage.unity`

An airplane token moves over `LevelNode` instances. Parking inside a node's detection radius
shows its card; Confirm loads an unlocked level, Back returns to the lobby.

`MapMover` reads `Map/Move`, `Map/Confirm`, and `Map/Back` from the shared input asset. Input is
shared, so any joined device can drive the token. Movement is constant-speed and snaps to face
direction. Persisted player GameObjects are deactivated while the map is open and reactivated
before either transition, so the target scene's `PlayerSpawner` can position them.

Each `LevelNode` references one `LevelConfig` — display strings, scene name, default unlock, save
key, and next-stage relationship all come from that asset. The node asks `ProgressionService` for
unlocked state and best stars, and owns only its locked/unlocked visuals, refreshing them on
`ProgressChanged`.

Confirming a locked node plays the wrong-action SFX; confirming an invalid config logs an error.
Loading is delegated to `SceneLoader` and guarded against double submission.

One reusable `LevelInfoPopup` card follows the currently detected node. It fills title and
description from the config, slides and fades in, and hides when the token leaves. Unlocked cards
show the description and best saved stars; locked cards append a locked label and explain that
the previous stage must be completed. Re-showing the same node is ignored so the animation
doesn't restart every frame.

> Because every stage config currently names a deleted scene, confirming a stage node will fail
> the `SceneLoader` Build Settings check. See [levels](levels.md#dangling-configs).

## Game HUD

**Prefab:** `Assets/Prefab/UI/GameHUD.prefab`, nested inside `GameManager.prefab`

`GameHUD` is presentation only. Every panel, text, image, and button reference is wired once
inside the prefab; it subscribes to the scene `GameManager` and stores no level rules.

| `GameManager` event | HUD response |
|---|---|
| `ScoreChanged` | update score text |
| `TimeChanged` | update `m:ss` timer |
| `CountdownChanged` | dim and show 3–2–1 |
| `RoundStarted` | reveal and animate timer and score |
| `PauseChanged` | show pause panel, select Resume |
| `RoundEnded(GameResult)` | play the result sequence |

The result sequence runs on unscaled time, because the round ends with `Time.timeScale = 0`. It
shows real total and per-player delivery counts from `GameResult`, reveals earned stars, stamps
approval, and selects Next Stage. P1–P4 rows are all prefab-wired.

Buttons call the `SceneLoader` API. Next uses `GameManager.Config.nextLevel` when present and
otherwise returns to stage select.

To redesign the HUD, edit or variant `GameHUD.prefab`. Don't push its child references back onto
`GameManager` or wire each level scene separately.
