# UI

**Scripts:** `MainMenu/{MainMenuManager, InputModeManager, BoardMenuRow, UIPulse, CameraFocus, TeamPanel}.cs`,
`ChooseStage/{MapController, MapPlane, LevelNode, LevelTicket}.cs`, `Game/UI/GameHUD.cs`

**Every UI element is authored in the scene or in a prefab — never built in code.** No
`new GameObject()`, no `AddComponent<Image>()`, no `Instantiate` for UI. The only exception is a
runtime copy of a prefab that already exists as a project asset, and even then that prefab is
authored, not generated. Lists use layout groups; alignment uses anchors and pivots, never pixel
offsets that die on an aspect-ratio change.

## Main menu

**Scene:** `Assets/Scenes/Menu/MainMenu.unity`

The authored `Main Camera` pose frames both the menu banner and the character lineup; it only
leaves that pose to lean in on a panel ([views](#views-and-the-camera-lean)). The entire menu is a
world-space canvas drawn onto the banner's poster face. The one screen-space element is the
[team panel](#team-panel) at the bottom of the screen, on its own overlay canvas `LobbyHUD`. The
old `Canvas`, `StartView`, and `MenuView` objects were deleted in the August 2026 board-menu
rebuild, along with the per-player `PlayerUI.prefab` cards.

> **The menu moved off `DepartureBoard` in August 2026.** The board carried the first version of
> this menu; it is gone from the scene and `DepartureBoard.prefab` is referenced by nothing. The
> layout below is a portrait re-fit of the same design, not a new one.

### The banner canvas

Scene root **`MenuBanner`** — the second `StandingBanner` copy, **unpacked** from
`StandingBanner.prefab` so its menu can't be wiped by a *Revert All* on a decoration prefab. The
other `StandingBanner` under `MenuEnvironment/Interior/Decor` is still plain decor.

The canvas is sized to the banner's printed poster, which had to be measured off the mesh — the
model's own bounds include the base plate and the frame lip, so they are the wrong rect:

| Mesh plane (banner-local) | `x` | Extent |
|---|---|---|
| Base plate | `−0.2760` | bottom only, 4 verts |
| **Frame lip** | `−0.1070` | `Y −0.763…0.953`, `Z ±0.610` |
| **Poster face** | `−0.0890` | `Y −0.742…0.932`, `Z ±0.588` — 1044 verts |

The poster is **portrait**: `1.1755 × 1.6736` local, and the banner's own scale is `4`, so
`4.702 × 6.695` world. Canvas settings that follow from that:

| | |
|---|---|
| localPosition | `(−0.094, 0.0952, 0)` — `x` sits 0.005 proud of the poster but *behind* the frame lip, so the menu reads as printed into the banner rather than floating off it; `y` is the poster's own centre, which is **not** 0 |
| localRotation | `(0, 90, 0)` — puts canvas `+Z` onto the banner's `−X`, the face the lobby camera looks at |
| localScale | `0.001`, with `sizeDelta (1175, 1674)` → `4.700 × 6.696` world |
| `worldCamera` | `Main Camera` — required, or the `GraphicRaycaster` can't take mouse clicks |

`Backdrop` is a plain dark `(0.055, 0.075, 0.115)` Image at **full alpha**, inset 25 px so the
poster's frame still shows around it. It has to be opaque: the printed artwork underneath is a
bright blue-and-teal poster, and even at alpha 0.94 it bled through badly enough to hurt the text.

`Content` insets 70 px horizontally and 55 vertically → a `1035 × 1564` working area.

The banner has two states, toggled by `MainMenuManager`. `Content/Title` is always on.

- **Before anyone joins** — `Content/JoinPrompt` shows `Press Space / (A) to Join`, breathing
  between alpha 0.3 and 1 on a 1.6 s cycle via a `CanvasGroup` + `UIPulse`. `UIPulse` runs on
  unscaled time so it keeps going at `timeScale` 0.
- **After the first join** — `Content/MenuRoot` reveals the column headers and the four rows.
  `JoinPrompt` and `MenuRoot` deliberately **share one rect** (`1330` tall at `y −234`): they are
  two faces of the same slot, so moving one means moving the other.

There is **no join hint** — it was cut deliberately in August 2026, and `RefreshJoinHint` went
with it. `PlayerSystem.FreeGamepadCount()` is now unused but kept; it is what a future hint would
need.

### Views and the camera lean

`MainMenuManager.ShowView` is the single place that decides what the banner shows. Four states,
all on the one canvas — opening a panel is only ever `SetActive` plus a camera move:

| View | Shown | Camera |
|---|---|---|
| `JoinGate` | `JoinPrompt` | home |
| `Menu` | `MenuRoot` | home |
| `Saves` | `SavesPanel` | leaned in |
| `Settings` | `SettingsPanel` | leaned in |

`SavesPanel` and `SettingsPanel` occupy **exactly the rect `MenuRoot` uses** (`1330` tall at
`y −234`), so `Title` stays put across every view. Players can still join while a panel is open.

`CameraFocus` (on `Main Camera`) swings between the authored lobby pose and `MenuFocusPose`. The
home pose is captured at `Awake`, never typed in twice; the close-up is the scene object, so
**re-frame the zoom by moving `MenuFocusPose`, not by editing code.** It was placed by measuring,
not by eye:

```text
distance = (posterHeight / fill) * 0.5 / tan(fov/2)
         = (7.533 / 1.05) * 0.5 / tan(30°)  =  6.213
```

The canvas faces world **+Z**, so the pose is `(−66.250, 1.098, 72.334)` with **zero rotation** —
straight down the poster's normal from its own centre. `fill` is **1.05**: the poster overruns the
screen top and bottom by 5%, which costs nothing (its own `Content` inset is 3.3% a side) and
leaves no strip of environment above or below it. `Content` lands at viewport `y 0.009…0.991`.

> **Zooming cannot hide the side environment, and that is geometry, not tuning.** The poster is
> portrait, `5.288 × 7.533`, so once its height fills a 16:9 screen its width can only cover
> `5.288 / (7.533 × 16/9) ≈ 39%`. At `fill 1.05` it covers 41%; the remaining ~59% is terminal
> either side, and a narrower FOV does not help — pulling the camera back to compensate keeps the
> same ratio. The ways out are **dressing what shows either side**, a **wider board**, or
> **dimming the world** behind the canvas during the lean (a full-screen Image on `LobbyHUD` faded
> by the focus blend). The dressing is done — see [below](#what-the-zoom-frames) — the other two
> are not.

> **Re-measure after any banner change.** This was re-placed in September 2026: the banner went
> from scale 4 to 4.5 (poster `6.696` → `7.533` tall) and the old pose was left behind at yaw 10°
> and too close, clipping the top of the poster off the screen. `posterHeight` is the height of
> `MenuCanvas`'s **world corners**, not a number to guess from the Inspector.

State is one `0…1` blend driven by `MoveTowards`, not a tween to a destination: interrupting a
move part-way reverses it from where it is, and the camera can never end up off its two poses.
It runs on **unscaled** time so the lean still plays if the lobby is ever paused.

> **Verified:** both end poses are exact, and every view/handler transition. **Not verified:** the
> intermediate frames of the lean — an unfocused Editor doesn't run the player loop, so the whole
> 0.55 s move collapses into one enormous-`deltaTime` frame. Eyeball the motion with the Game view
> focused.

### What the zoom frames

Since the poster can only cover ~41% of a 16:9 screen, the remaining band either side of it **is**
the shot. The right band already had midground — the escalator at `43` units and the platform at
`51` — but the left band was floor, then nothing until `WallBelakang` **200 units away**, which
read as a flat pink slab. `MenuEnvironment/Interior/Decor/BannerLeftDressing` fills it, all of it
instances of existing decoration prefabs at the scales the rest of the lobby already uses:

| Prop | Position | Scale |
|---|---|---|
| `StackedChairs` | `(−78.0, −3.80, 101.0)`, yaw 90 | `1.1` — the seat rows run across the view |
| `TrolleyStack1` | `(−77.0, −2.86, 94.0)`, yaw 200 | `2.2` |
| `IndoorTree` | `(−82.5, −1.43, 99.0)` | `0.45` |
| `StandingSignage` | `(−81.5, 6.48, 103.0)`, yaw 250 | `2.0` |
| `HousePlant2` | `(−78.5, −3.80, 90.5)` | `1.4` |

**The banner hides more of the floor than it looks like it does, and that is what makes placement
here unintuitive.** The poster's left edge is at world `x −68.89`, the focus camera at
`x −66.25, z 72.334`, the poster at `z 78.97` — so the shadow it casts widens with depth:

```text
hidden if   x  >  −66.25 − 2.64 * (z − 72.334) / 6.64
```

At `z 94` everything right of `x −74.9` is behind the board; at `z 101`, right of `x −77.9`. A prop
dropped at `x −72` reads fine in the Scene view and is invisible in the shot. Props also grow fast
as they come forward — the first pass put the trolleys at `z 85` and they swallowed the whole
bottom-left corner — so the pocket that works is roughly **`z 90…105`, `x −77…−84`**.

Every prop is floor-snapped by its renderer bounds, not by its pivot, because these prefabs' pivots
disagree with each other (`StandingSignage` and `IndoorTree` both pivot *above* their base). The
floor under the pocket is `y −3.80`.

This dressing barely shows at the home pose — the pocket sits just outside the left edge of the
lobby framing — so it costs the main shot nothing.

### Save and settings panels

Both are built from the same `BoardMenuRow.prefab` as the main menu, so the save list is a
departures list too. Rows are wired in the scene: each slot row passes its **slot number as the
button's int argument** to `OnClickSlot`, and every panel's `Row_Back` calls `OnClickBack`.

New Game and Load Game open the *same* `SavesPanel` — four slot rows plus Back. The only
difference is `savesTitle` text and one bool:

| | New Game | Load Game |
|---|---|---|
| Empty slot | pressable, reads `EMPTY` | **not** pressable, reads `NO DATA` |
| Filled slot | pressable — **overwrites** it | pressable, loads it |
| All slots empty | all four pressable | nothing pressable; Back is the only way out |

What a slot actually stores, and what it deliberately does not, is in
[services](services.md#save-and-progression) — read that before building anything on top of it.

Locking is `Button.interactable`, set **before** `SetContent` so the refresh picks idle vs locked
wording off it. That is the whole reason `BoardMenuRow.lockedStatus` exists — no new code was
needed for the disabled state, just `EMPTY` / `NO DATA` authored on the slot rows.

Gamepad navigation must never start on a locked row, so `ShowView` asks
`FirstSelectableSaveRow()` for the default instead of a fixed field: first interactable slot,
falling back to `savesBackRow` when every slot is locked.

Slot rows are the one place row wording is not authored — `BoardMenuRow.SetContent` fills them
from `ProgressionService.ReadSlotSummaries()` on every open, so a save written last round shows
up. The column assignment is forced by width, not taste: **`"SLOT 1"` measures 242 px and the
FLIGHT column is 224**, so the slot name lives in DESTINATION (483 px) and FLIGHT carries a short
`SV01`-style code mirroring the menu's `C0110`. Worst case measured: `12 STARS` at 204/255.

> **`Awake` does not run on an inactive GameObject.** `BoardMenuRow` caches its idle text colours
> once, and these rows live inside panels that start switched off — so filling the save list
> before the panel was ever shown read those colours as `default(Color)`, transparent black, and
> painted the text **invisible until hovered** (hover uses `activeTextColor`, which was fine).
> Caching is now lazy via `EnsureCached()`, called from `Awake`, `OnEnable` **and** `SetContent`,
> so call order can't bite again. Anything else that touches a row before first activation must
> go through a method that calls it.

`SettingsPanel` is deliberately a title, a placeholder line and a back row — the panel and its
plumbing exist so adding a real control is a scene edit.

Joined characters each take an authored lobby slot, frozen with kinematic rigidbodies, facing the
camera. Starting with zero players is rejected with the wrong-action SFX.

`InputModeManager` switches between pointer mode (mouse movement and clicks, nothing selected)
and navigation mode (gamepad activity, `Row_NewGame` selected). Keyboard players use the mouse
for menu UI; gamepad players navigate. The `LastJoinFrame` guard stops a join press from
immediately activating whatever row is selected.

### Board rows

Each row is an instance of `Assets/Prefab/UI/BoardMenuRow.prefab` under a `VerticalLayoutGroup`
(spacing 0, `childForceExpandHeight` off, `LayoutElement.preferredHeight 235` per row → 940 for
four). Flight code, destination, `Button.interactable`, and the `onClick` target are scene
overrides:

| Row | Flight | Destination | Handler | Does |
|---|---|---|---|---|
| `Row_NewGame` | C0110 | NEW GAME | `OnClickNewGame` | opens the save list, wipe-on-pick |
| `Row_LoadGame` | C0111 | LOAD GAME | `OnClickLoadGame` | opens the save list, load-on-pick |
| `Row_Settings` | C0112 | SETTINGS | `OnClickSettings` | opens the settings panel |
| `Row_Exit` | C0113 | EXIT | `OnClickExit` | quits |

All four are `interactable` and look identical. Only Exit acts directly; the other three open a
panel. Nothing reads `DELAYED` any more; `BoardMenuRow.lockedStatus` is dormant until some row is
disabled again.

Every handler starts with the `ConsumedByJoin()` guard so the button press that *joined* a player
can't also activate whatever row happened to be selected on that same frame.

Selection feedback: the row's own `Image` is the `Button`'s ColorTint target, and
highlighted / selected fill the row with **bronze** `(0.455, 0.302, 0.125)` — the same tone as
the title text, chosen to sit in the environment rather than glare out of it. Pure scene data, no
code. `BoardMenuRow` adds what a ColorBlock can't reach: all three texts flip to cream
`(0.96, 0.91, 0.82)` so they read on the bronze, the cream `SelectionMarker` notch appears, and
STATUS flips `ON TIME → BOARDING`.

**The highlight cannot be previewed by hand in the editor.** `Selectable` pushes `normalColor`
(white at **alpha 0** — rows are transparent at rest) onto the target's `CanvasRenderer` on
enable, and the CanvasRenderer colour is a separate channel from `Image.color`, so setting
`Image.color` does nothing visible. To eyeball a selected row, drive
`image.canvasRenderer.SetColor(...)` instead — and put it back afterwards.

Idle text colours are read from the components at `Awake`, never hard-coded — so recolouring a
row stays a prefab or scene edit. The corollary: **if you preview a selected row by hand-editing
its text colours in the editor, put them back**, or that dark preview colour becomes the row's
idle colour and the text vanishes against the backdrop.

#### Portrait re-fit

The row prefab was retuned in August 2026 when the menu moved from the wide board (2827 px rows)
to the portrait banner (1035 px rows). The three columns use **fractional anchors**, so they
rescale with the row on their own; what did *not* survive the move were the absolute insets and
the font sizes, which were sized for a row nearly 3× wider.

| Column | Anchors | Font | Widest string | Needs / has |
|---|---|---|---|---|
| `FlightCode` | `0 … 0.24` | 64 | `C0110` | 202 / 224 |
| `Destination` | `0.24 … 0.73` | 72 | `LOAD GAME` | 441 / 483 |
| `Status` | `0.73 … 1` | 44 | `BOARDING` | 234 / 255 |

All three now inset `12 px` a side (was 55 / 22), and `SelectionMarker` is `14 × −24` at `x 10`.
`Status` is the tight one — it is sized for `BOARDING`, the *selected* wording, which is longer
than the `ON TIME` sitting there at rest. At the board's old font 46 it overflowed its band by
10 px. If you re-word a status, re-check it against the band, not against what the row shows idle.

> Editing this prefab also changes the four rows still inside `DepartureBoard.prefab`, which now
> render at portrait sizes on a landscape board. That board is retired, so this is deliberate.

Navigation is left on **Automatic** deliberately. Explicit navigation would happily land on a
locked row — `Selectable.Navigate` only checks `IsActive()`, not `IsInteractable()` — whereas
Automatic filters non-interactable entries, so gamepad up/down steps NEW GAME ↔ EXIT and starts
routing through LOAD GAME and SETTINGS the moment they are enabled.

Picking a save slot unfreezes persisted players and calls `SceneLoader.LoadStageSelect` — it also
refuses, with the wrong-action SFX, if no player has joined, since the stage would have no
characters. Device ownership is
in [player](mechanics/player.md#joining).

### Standing the lobby characters

**The lineup is authored, not computed.** `PlayerSpawnTransform` holds four empties,
`LobbySlot_1…4`, wired into `MainMenuManager.lobbySlots` in join order. `UpdatePositions` copies
slot *i*'s position **and rotation** onto the *i*-th joined player and does nothing else — move a
slot object to move a character, turn it to turn one. There are no spacing, band-width or scale
numbers left on the component; the centred-row solver, `spacing`, `bandWidth`, `lobbyScale`,
`feetPivotOffset`, `spawnCenter` and `lobbyCamera` all went with it in September 2026, because the
generated row read as awkward and was easier to place by hand.

Slots are seeded on an **arc at a constant distance from the lobby camera** (radius `16.54` from
the camera's pivot, bearing `40.3°`, at `±6°` and `±18°`), each facing the camera:

| Slot | Position | Yaw |
|---|---|---|
| `LobbySlot_1` | `(−61.59, −3.65, 76.08)` | `202.3` |
| `LobbySlot_2` | `(−58.55, −3.65, 74.44)` | `214.3` |
| `LobbySlot_3` | `(−55.91, −3.65, 72.20)` | `226.3` |
| `LobbySlot_4` | `(−53.79, −3.65, 69.47)` | `238.3` |

Equal camera distance is the point: the old row ran diagonally away from the camera, so the fourth
character rendered visibly smaller than the first and the shoulders overlapped. On the arc all four
read the same size with a clear gap between them.

**Slots fill left to right in join order, so one player stands on the leftmost slot, not in the
middle.** That is the price of manual placement; re-order the array if a solo player should stand
somewhere else.

`y` is `−3.65`, on the floor: the character models' pivots **are** their soles (Annie's `MainBody`
spans `y 0.0000 … 2.5563`), so a slot sits at floor height, not hip height. The floor under the
lineup is `Floor_Ground.002` at `y −3.70`. Re-check this if a character prefab is ever replaced —
a wrong pivot floats or sinks every lobby character at once. Nothing scales the characters in the
lobby; they stand at prefab scale, ~`5.3` world units tall.

**Player pin.** The `1P/2P` pin over each head is the `Player Indicator` child on the character
prefab (`PlayerIndicator.cs`). Its height comes from the character's **renderer bounds, not the
capsule** — all three bodies share one capsule that stops at the skull ([player](mechanics/player.md#all-three-bodies-are-one-rig)),
so hanging the pin off the collider would bury it in Annie's hat and Bun Jovi's ears. Reading
the silhouette keeps one `hoverHeight` (`0.2`) correct on every body. In the lobby it never
hides; in a gameplay scene (detected by
`GameManager.Instance`) it shrinks away `gameplayShowSeconds` (3 s) after each scene load, over
`shrinkDuration` (0.35 s) — by then everyone knows which body is theirs, and it comes back on
returning to the lobby. The pin billboards by copying the camera's rotation (screen-parallel),
not by aiming at the camera position — aiming skewed pins near the screen edge, which read as a
stretched sprite. Note the pin draws through the `WorldUI` overlay camera, so **it is invisible
in Unity-MCP camera-render screenshots** (the URP overlay stack is skipped); use
`ScreenCapture.CaptureScreenshot` to see it.

> **Removed from the scene in the August 2026 cleanup:** the bottom-right `PlayerListCanvas`
> avatar strip, the retired `DepartureBoard` instance, and a stray `MeshRenderer` with no
> `MeshFilter` sitting on `PlayerSpawnTransform`. `LogoCanvas` had already gone. The join hint
> that used to live on the player-list panel now sits at the bottom of the banner canvas.
>
> **Scene-only — the assets are still in the project on purpose.** `DepartureBoard.prefab` is
> referenced by nothing and is kept for re-use. Don't treat "no references" here as dead weight to
> prune. (`Assets/UI/MainMenu/Multiplayer1-3.png` were in the same boat until the
> [team panel](#team-panel) put them back to work in September 2026.)
> (`PlayerSlot.prefab` had already gone; its orphaned `PlayerSlotView.cs` was deleted in the
> September 2026 cleanup.)

### Team panel

A four-seat strip in the screen's **bottom-right corner**: one head-and-body figure per seat, lit
white when a player holds it and dim grey when it is free, with a line under each seat naming the
device that holds it or the button that would take it. Seats fill left to right in join order.

```text
LobbyHUD                 Canvas, Screen Space - Overlay, sort 10, CanvasScaler 1920x1080 match 0.5
  TeamPanel              Multiplayer2.png at native 289x172, anchored bottom-right, (-16, +12); TeamPanel.cs
    Slots                HorizontalLayoutGroup, spacing 5 (57 px seats -> 62 px pitch)
      Slot_1..Slot_4     TeamSlot.prefab (57 x 100); TeamSlot.cs
                           Head  Multiplayer1 41x40
                           Body  Multiplayer3 49x30
                           Tag   TMP 13, CanvasGroup + UIPulse (disabled), one line, 57x16
```

| Seat | Figure | Tag |
|---|---|---|
| taken | lit `joinedColor` | device: `WASD` · `ARROW` · `PAD 1` · `PAD 1 L` / `PAD 1 R` |
| taken, whole pad, a seat still free | lit | device **+ `(Y) SPLIT`** in prompt colour at 85% size, on a second line |
| next free seat | dim `emptyColor` | the key alone — `(A)` / `SPACE` / `R-SHIFT` — pulsing |
| free, not next | dim | blank |

**Even margins are measured, not eyeballed.** The seat block is `40 + 7 + 30 + 7 + 16 = 100` tall,
sat at `y −32` inside the panel's dark inner area (`y 13…149`), which renders as **19 px above the
heads and 19 px below the tag** at 1920x1080. The `−32` is one pixel past centring the rect,
because a caps-only line leaves ~2 px of empty rect under its baseline. Re-measure off a render
after any font or size change; the split hint's second line deliberately eats into the bottom
margin (8 px left under it) and is the one case that is not even.

- **Shown only once someone has joined.** `TeamPanel` is saved **inactive**; `MainMenuManager.
  RefreshTeamPanel` switches it on and refreshes it on every join, and once in `Start` so a
  return to the lobby with players already joined shows it straight away. `TeamPanel` also
  refreshes on `InputSystem.onDeviceChange`, because plugging a pad in changes what the free seat
  should ask for with nobody touching anything.
- **The scripts only tint and set text.** Every rect, sprite and layout group is authored.
  `TeamPanel` decides what each seat says; `TeamSlot` draws it, tinting `joinedColor` /
  `emptyColor` (white / `0.41` grey, from the design mock) and switching its `UIPulse` on only for
  the seat that is asking to be filled — recolour on the components, not in code. The slot art is
  white for exactly this reason; leave the prefab's own Image colours white.
- **A seat pitch is 62 px (57 + 5 spacing), and that is the whole budget for a tag.** At font 13
  the widest label allowed is ~51 px, which is why the join prompt is the bare key with no `JOIN`
  after it, why the keyboard-right seat reads `ARROW` and not `ARROWS` (59 px — it touched its
  neighbour), and why the split hint is `<size=85%>`. Measure a new label with
  `TMP_Text.GetPreferredValues` before using it; the tag deliberately does not wrap or clip, so an
  over-long one silently runs into the next seat.
- **Button names follow the Xbox layout** (`A` to join, `Y` to split) since that is what PC games
  label generically. They are plain text in `TeamPanel`; swap them for glyph sprites, chosen per
  connected pad, when the art exists.
- **Everything is at the sprites' native pixel size**, which is also 1:1 with the design mock
  (measured to within ~8%). The panel sprite is not 9-sliced; it doesn't need to be at native size.
  If the panel ever has to grow, give `Multiplayer2` a sprite border first (≈32 px keeps its
  rounded corners) and switch the Image to *Sliced*.
- **Overlay, not World Space**, for the reason in the next section. The canvas has no
  `GraphicRaycaster` — the panel is display-only, and without one it can never swallow a click
  meant for the banner.
- **Placement was checked against the lineup** at 2 and 4 players; in the bottom-right corner the
  panel is clear of the characters entirely. It was bottom-centre first, where it sat right under
  their feet.
- `Multiplayer1` and `Multiplayer3` were imported as *Multiple* sprite mode with no rects sliced,
  i.e. they held **no sprite at all**. Both are *Single* now.
- `ButtonPrompt.png` (a generated 64 px white disc) is left in the project unused: it was the
  circle in the `MANAGE TEAM` row this panel started with, and it is the obvious base for a real
  button glyph.

### Overlay vs world space, and the outline pass

The lobby's team panel is a **Screen Space - Overlay** canvas, as the old logo canvas was before
it, and that mode is not a style choice. The logo started as World Space and the outlines of scene
objects behind it drew straight over the artwork. The cause is render order, not parenting:

1. `URP-HighFidelity-Renderer` runs a `FullScreenPassRendererFeature` with the
   `OutlinePostProcessing` material at injection point **`AfterRenderingPostProcessing`** — the
   last thing in the frame. That pass is where the game's whole outlined look comes from.
2. A World Space canvas is ordinary transparent geometry (`UI/Default`, render queue 3000) drawn
   during the camera's normal loop, well before that pass.
3. `UI/Default` has **`ZWrite Off`**, so the logo never appears in the depth/normals buffers.

So the outline pass edge-detects only the geometry *behind* the logo and paints those edges over
the finished image. Overlay canvases are composited to the backbuffer after the whole SRP loop,
renderer features included, so they are immune.

If anything flat ever needs to sit *over* the world rather than in it, Overlay is the right mode;
in World Space the outline feature has to be dealt with too, and the artifact is not fixable by
moving, reparenting, or re-sorting the canvas.

**`MenuCanvas` is unaffected**, and that is not luck — it is diegetic. It hangs on the banner in
world space precisely so the terminal's outlines and lighting apply to it; there is no artwork
underneath for stray edges to spoil, only the `Backdrop` fill.

### Decorative world canvases

`MenuCanvas` is not the only world-space canvas in the lobby. The airport signs
(`WallSignage2`, `StandingSignage2`) and the `LogoTV` on the pillar above the banner each carry
their own canvas as a child of their **decoration prefab**, with a scrolling ticker or a flicker on
it. (The `WallSignage` ticker that used to hang there was swapped for `LogoTV` in September 2026.)
They are display-only —
no `GraphicRaycaster`, nothing selectable — and they are documented with the props, not here: see
[hazards and props](mechanics/hazards-and-props.md#signage-text). The signs copy this section's canvas
convention (`localRotation (0, 90, 0)`, ConstantPixelSize scaler, `LiberationSans SDF`), so change
one and check the other.

## Stage select

**Scene:** `Assets/Scenes/Menu/ChooseStage.unity`

`MapController` spawns one `MapPlane` token per joined player over a flat map of `LevelNode`s.
The `LevelTicket` card appears only once **every** plane is parked within `nodeRadius` of the same
node; Confirm then loads that node's level and Back returns to the lobby. The map camera is a
`MultiplayerCamera` framing the planes.

Persisted players are deactivated while the map is open, so their own `PlayerInput` can't drive
anything. Each plane instead gets a runtime clone of the shared input asset, masked to that
player's control scheme and paired to their devices — which is what lets two halves of one
keyboard fly two planes. Confirm and Back stay on the shared asset so any device can press them.
`MapPlane` movement is deliberately unsmoothed: full speed the frame the stick moves, zero the
frame it stops.

Each `LevelNode` references one `LevelConfig` — display strings, scene name, default unlock, save
key, and next-stage relationship all come from that asset. The node asks `ProgressionService` for
unlocked state and best stars, and owns only its locked/unlocked visuals, refreshing them on
`ProgressChanged`. `LevelTicket` fills its boarding-pass labels from the same config; the prefab
owns every label and the presenter only fills and fades them.

Confirming a locked node plays the wrong-action SFX; a node with no loadable config logs an error.
Loading is delegated to `SceneLoader`, which refuses scenes that aren't in Build Settings.

> Nodes 3 and 4 still point at the archived `LevelConfig_Stage3/4` and stay locked. See
> [levels](levels.md#config--scene-mapping).

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

The result sequence runs on unscaled time, because the round ends with `Time.timeScale = 0`.
See [the stage end card](#the-stage-end-card) below.

### Timer and score plaques

Both in-round readouts are the same widget built twice, from `Assets/UI/Gameplay/`:

```text
Timer  (bottom-left, anchoredPosition 100,40)     Score  (bottom-right, −40,40)
├── Image  MoneyBar, 228 × 110                    ├── Image  MoneyBar, 228 × 110
├── Icon   TimeIcon, 118.7 × 124                  ├── Icon   MoneyIcon, 117.6 × 124
│   └── Fill   red radial, 56 × 56                └── Text   score, 150 × 60 at x +30
└── Text Timer  m:ss, 150 × 60 at x +30
```

The icon is anchored to the bar's **left edge with pivot `0.5`**, so half of it hangs outside the
plaque — that overlap is the look, and it is why the icon is 124 tall against a 110 bar. The text
sits at `x +30` to clear the icon's inner half; at the plaque's 228 width a 150-wide text box is
the widest that still fits inside the bar.

> **The timer's x is 100, not 40.** Its icon hangs 59 px left of the container, so at the score
> plaque's mirrored inset of 40 the stopwatch would run off the bottom-left corner of the screen.
> 100 puts the icon's left edge at ~41, which is what actually mirrors the score side.

`GameHUD.gameTimerFill` drives `Timer/Icon/Fill` from `OnTimeChanged`, as
`timeRemaining / LevelConfig.gameTime`. It is a plain drain in one authored colour with **no
warning/danger thresholds**, using the built-in `Knob` sprite, `Filled` / `Radial360`, origin Top,
counter-clockwise.

Buttons call the `SceneLoader` API. Next uses `GameManager.Config.nextLevel` when present and
otherwise returns to stage select.

### The stage end card

**Art:** `Assets/UI/StageEnd/`. Authored under `GameHUD/Stage End`.

The card replaced the old `Visa` panel in September 2026. `Visa` and its delivery-count rows are
gone, and `Assets/UI/OldAsset/Visa.png` with them; the card reads **score**, not deliveries.

```text
Stage End                       1000 × 880 at (0, 20)      the object that slides in
├── Level Completed             TMP, top-left of the root
├── Window                      PopupWindow, 1000 × 708 at (0, 42)
│   ├── Departure Icon / Departure Title / Flight Label / Flight Code
│   ├── Level Image             LevelImageFrame, 434 × 328 at (−214, 44)
│   │   └── Preview             LevelConfig.previewImage; blank white when unset
│   ├── Stage Tag               LevelInfoBar, rotated −8°, holds displayName
│   ├── Score Label + Stars     GridLayoutGroup, 3 × (110 × 150)
│   │   └── Star N/Empty/Filled gold star, switched on per earned tier
│   ├── Score Row / Time Row    TimeScoreBar, 434 × 54, each with a CanvasGroup
│   └── Players                 GridLayoutGroup, 2 cols, cell 434 × 70, spacing 24/17
│       └── Player Row 1–4      PlayerScoreBar + CanvasGroup
├── Approved                    stamp, rotated −12°, over the card's bottom-right corner
└── Next                        button under the card, calls LoadNextStage
```

`Next` reuses `TimeScoreBar` so it reads as part of the card rather than as the old blue
`Button.png`. Because the sprite is 844 × 110 and the button is 300 × 72, the Image is **Sliced**
— `TimeScoreBar.png` carries a 30 px border for exactly this, which is a shade over its 26 px
corner radius. The score and time rows stay `Simple`; they scale close enough to uniform that
their corners never distorted. The bar is dark navy, so a ColorTint highlight can only ever
darken it: idle sits at 0.72 grey and highlighted/selected return to full white. The label is the
card's gold `(0.949, 0.761, 0.290)`.

Level-derived text (`displayName`, `flightCode`, `previewImage`, round length, star thresholds)
is filled in `SyncCurrentState` at `Start`. Only the score and the player rows are filled from
`GameResult` when the round ends.

The sequence: Time's Up scale-in → card slides down from `+Screen.height` → Score Row → Time Row
→ one player row at a time → earned stars one at a time with `Sfx.Star` → `Sfx.Stamp` and the
Approved stamp if at least one star → Next, selected for gamepad.

**Rows fade, they don't pop.** `FillPlayerRows` switches on one row per joined player *before*
the reveal starts and leaves each row's `CanvasGroup` at alpha 0. If reveal used `SetActive`
instead, the `GridLayoutGroup` would re-centre the block on every row and the whole list would
jump. Player count comes from `PlayerInput.all` (clamped to 1–4), and the name is the character
prefab's own name with `(Clone)` stripped — Annie, Bun Jovi, Scannor.

Star tiers are **three**, matching `LevelConfig.CalculateStars`. Each `Star N` is an
`EmptyStar(DarkVer)` with an inactive `StarCompleted` child; earning the tier switches the child
on, so the swap stays a scene decision rather than two sprite fields on the script.

The card is a fixed size, so a one-player round leaves the lower half of the window empty. That
is deliberate — the window does not resize to the player count.

To redesign the HUD, edit or variant `GameHUD.prefab`. Don't push its child references back onto
`GameManager` or wire each level scene separately.
