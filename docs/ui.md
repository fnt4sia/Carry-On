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

Whatever sits behind the text has to be opaque: the printed artwork underneath is a bright
blue-and-teal poster, and even at alpha 0.94 it bled through badly enough to hurt the text.

**Board screen (test since 6 Oct 2026, Fitra's ask).** The text now sits on a CRT screen: a quad,
`MenuBanner/Screen`, the canvas's exact size (`1.175 × 1.674` banner-local), at `x −0.0915` —
between the poster (`−0.089`) and the text (`−0.094`) — wearing `Material/World/Board Screen.mat`,
the TV's `TVScreen` shader graph tuned darker (screen `(0.045, 0.16, 0.22)`, 64 scanlines) so the
text keeps its contrast (≈7.5 : 1 measured). It's a mesh, not a UI `Image`, because the shader graph
is an URP Unlit graph, not a Canvas one; the text and its mouse input stay on the canvas untouched.
`Backdrop` — the old plain dark `(0.055, 0.075, 0.115)` Image, inset 25 px — is **switched off, not
deleted**: to undo the test, turn `Backdrop` back on and `Screen` off.

**Text weight follows importance** (6 Oct 2026). Post-processing greys white text — the Global
Volume's blue colour filter and Neutral tonemapping turned the authored `#F0F5FF` into
`(186, 198, 224)` on screen — so the things you act on use
`Font/LiberationSans SDF - Board Bright.mat`, the font's material with its face colour at `1.35`
(HDR), which lands near white (`(217, 231, 242)` measured) with a hint of bloom.

| Text | Weight | Colour |
|---|---|---|
| destinations (`NEW GAME`, `SLOT 1`, `BACK`…) | bold, Board Bright | `#F0F5FF` |
| join prompt | regular, keys bold via `<b>`, Board Bright | `#FFD638` |
| panel titles | regular | `#FFD638` at 80 % |
| flight codes, status | regular | `#DBB238` / `#B8CCEB` at 60 % |
| column headers, settings placeholder | regular | `#859EC7` at 55 % |
| `DEPARTURES` | regular | `#FFD638` at 35 % — decoration |

Row text styling lives on `Prefab/UI/BoardMenuRow.prefab`; the scene's ten rows carry **no** colour
or style overrides (63 redundant ones were reverted), so restyle the prefab. The picked row is
separate: a brown bar `(0.455, 0.302, 0.125)` from the Button's `selectedColor` with cream
`activeTextColor` `(0.96, 0.91, 0.82)`, authored on the rows.

`Content` insets 70 px horizontally and 55 vertically → a `1035 × 1564` working area.

The banner has two states, toggled by `MainMenuManager`. `Content/Title` is always on.

- **Before anyone joins** — `Content/JoinPrompt` shows `Press Space / (A) to Join`, breathing
  between alpha 0.55 and 1 on a 1.6 s cycle via a `CanvasGroup` + `UIPulse` (the floor was 0.3
  until 6 Oct 2026 — too faint half the time). `UIPulse` runs on unscaled time so it keeps going
  at `timeScale` 0.
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
a wrong pivot floats or sinks every lobby character at once. Apart from the join pop below, nothing
scales the characters in the lobby; they stand at prefab scale, ~`5.3` world units tall.

**Join bubbles.** Each `LobbySlot_n` carries a `JoinBubbles.prefab` child
(`Assets/Prefab/Character/`). On join, `MainMenuManager.PlayJoinBubbles` plays the slot's burst and
`PopIn` grows the character from zero to its own scale over `joinPopDuration` (0.35 s, ease-out-back,
peaks ~110%) so it reads as bursting out of the bubbles. A slot with no burst still pops. The burst
is two mesh-particle systems on the `Cloud.001` bubble mesh — a 14-puff cloud around the body and a
14-puff fizz rising from the feet — using `Airplane1`, the **opaque** puff material `WalkSmoke`
already uses. Don't switch it to the transparent `Bubble Colour`: the outline pass works off the
depth texture, which transparent puffs don't write, so the character's outlines draw *through* the
bubbles as a grey ghost figure. Each burst is tinted per player through a MaterialPropertyBlock,
`joinBubbleTint` (0.5) of the way from the material colour to `PlayerIndicator.CurrentColor`.
`onPlayerJoined` fires from `PlayerInput.OnEnable`, **before** the pin's `Awake`, so `CurrentColor`
resolves its owner lazily — before that fix every burst came out white. Returning to the lobby with
players already joined replays the pop for each of them.

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
underneath for stray edges to spoil, only the flat `Screen` quad (or the `Backdrop` fill).

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

An Overcooked-style island map (Carstenz's 3D archipelago, which replaced the old flat map on
30 Sep 2026). `MapController` spawns one `MapPlane` per joined player — up to four — and each
player flies their own. Level nodes sit on the airport islands. There is **no Confirm press**:
boarding together is the confirmation.

Every node's **landing ring** on the sea shows where that stage stands (Fitra's spec, 5 Oct 2026):

| Stage | Ring | Badge | Planes |
|---|---|---|---|
| completed — finished at least once | yellow | yes | land, can board (replay) |
| current — unlocked, not yet completed | white, scaling up and down (`pulseAmount` 6 %, `pulsePeriod` 1.6 s) | yes | land, can board |
| locked — not reached yet | grey | none | fly straight over: no landing, ticket, sound or slot |

"Completed" is `ProgressionService.IsCompleted`: a `completed` flag (save version 3) set by
`RecordResult` — the same moment the next stage unlocks, stars or not. Unlocking alone creates a
save entry too, so an entry existing doesn't mean completed. Saves written before version 3 read
every level as not completed until it's finished again.

On an unlocked node:

| Planes on it | What the player sees |
|---|---|
| none | badge shows best stars |
| some | badge shows one slot per plane, lit in that player's colour if their plane is here, grey if not; those planes touch down on the island |
| all, node boardable | the same, while the ring and the badge border fill over `boardingSeconds` (2 s) — gold on a white ring, white on a yellow one, so the fill shows on both |
| ring full | the group is committed: the `LevelTicket` boarding pass pops up over the node, every plane's stick and Back are ignored, and `ticketSeconds` (3 s) later the level loads |

If any plane leaves before the ring is full, it drains at the fill rate instead of snapping to
empty. A node is boardable when it is unlocked **and** its scene is in Build Settings; the group
arriving on an unlocked one whose scene can't load plays the wrong-action SFX, logs an error, and
the ring never fills. Back returns to the lobby.

**The boarding pass is the transition into the level** (Fitra, 5 Oct 2026): it doesn't show while
planes gather or the ring fills — only once the ring is full. `MapController.Board` then locks the
planes (`MapPlane.Locked`), calls `LevelTicket.Show`, waits `ticketSeconds`, and hands over to
`SceneLoader.Load`, whose own fade and progress bar take it from there (measured: ring full at
2.0 s, ticket on the same frame, Level 1 active at 5.5 s). The card **pops**: a damped spring from
nothing to full size — 1.25× at 0.18 s, 0.94× at 0.36 s, settled at `popSeconds` (0.45 s);
`popOvershoot` (0.25) sets the first bounce. It sits at `worldOffset` `(0, 12, 30)` from the node.
If the load can't start (another load already running), the card fades out and the map hands
control back. The card and the badges sit on the WorldUI layer, so the overlay camera draws them
on top and the outline pass never draws over them.

**Being "on" a node** means inside its landing ring on the flat — `LevelNode.RingCenter` and
`RingRadius` are read straight off the `Landing Ring` canvas (half its width × its scale), so
**scaling or moving the ring is how you resize a stage's area**; there is no separate radius to
keep in step. Select `Landing Ring` under a `Map Node` and scale it uniformly (scale 0.065 =
26 m across; the canvas is 400 units wide). Edit the `Map Node` prefab to change every island, or
override one node in the scene for an island of another shape; the `MapController` gizmo draws
each ring's area. Node pivots are the islands' own pivots (y 5.2, on the terminal, about 4 m north
of the runway), but the airport island's footprint is centred about 3 m south of that, so the
prefab's ring sits at local `(0, -2.9, -2.96)`: sea level, on the island's centre. At 26 m the
ring's band lies just outside the shore foam — any smaller and the white ring vanishes into it.
A landed plane settles at the pivot's height plus `landingClearance` (3.6 m, half the plane
model), wherever it is inside the ring — which can put it through the terminal and control tower.

`Plane Spawn` (-34, 17.5, -64) must stay outside every ring, or the group boards Level 1 two
seconds after the map opens. It's also far enough south that the plane doesn't *look* parked on
Node 1: at 45° a plane cruising 15.2 m above the sea appears about 15 m up the screen from the
spot below it, which is the spot the rings measure. Its height is the cruise height: 17.5 m keeps
the plane's belly (3.6 m below its pivot) just over the tallest control tower (13.3 m). Planes
cruised at 24 m while the cloud sea existed; it was lowered on 5 Oct 2026 to shrink that offset.

**Camera.** The map runs the levels' `ArenaFollowCamera` (the Moving Out rig), handed the planes
through `SetTargets`: rotation `(45, 0, 0)`, FOV 50, pulled back along its own view axis from 75 m
with the group together (about 124 m of map across the frame) to 140 m when they spread out;
margin 14. Fitra asked for 45° and a Moving Out-wide shot on 1 Oct 2026, replacing the tighter
60° / FOV 40 / 50 m first pass. Yaw stays 0: the runways run east–west and the water glints are
world-x dashes, so both stay horizontal only at yaw 0, stick-up is north, and the nodes read left to
right. At 45° the top of the frame shows the mainland terrain, and with the group far north (or
spread out to the 140 m zoom) the terrain's north edge (z 113) and the camera's flat background
colour beyond it — the cloud sea used to cover that until it was removed on 5 Oct 2026.

The rig only writes position, so it can be tuned live: in Play mode, change the Main Camera's
rotation, FOV or the rig's distances and the shot follows on the next frame. Copy the values back
after exiting Play mode. The authored pose is the rig's first frame at `Plane Spawn`. Shadows
reach 150 m on the High Fidelity URP asset; Balanced and Performant stop at 50 m, so at this zoom
they lose the plane and cloud shadows.

Persisted players are deactivated while the map is open, so their own `PlayerInput` can't drive
anything. Each plane instead gets a runtime clone of the shared input asset, masked to that
player's control scheme and paired to their devices — which is what lets two halves of one
keyboard fly two planes. Back stays on the shared asset so any device can press it. The plane
takes its colour from the player's `PlayerIndicator.CurrentColor` before the player is hidden.
`MapPlane` moves with `PlayerMovement`'s feel — the same `movementLerpSpeed` (0.15) and
`rotationSpeed` (4), converted from per-physics-step to per-frame — and its bubble trail is a copy
of the characters' `WalkSmoke` particle system on a `Trail` child at the tail. Stick-up follows the
camera's flattened **up** vector, not its forward, so a camera pitched past vertical can't invert it.

**Whose plane is whose.** The `Map Plane` prefab's model is Carstenz's `plane1` (`Model/Plane`,
nested `Prefab/StageSelect/plane1`, nose +Z; `Model` at scale 0.18, 3× the FBX — it was 0.12 until
5 Oct 2026, when a blue plane on the blue sea was too hard to find). `MapPlane` paints its
`paintedParts` — the body, wings and tail fin, the renderers on the FBX's `Material.001` — in the
player's colour with a property block, and fills in `Player Pin` (4.4 m up, clear of the plane's
top): the characters' `Prefab/UI/Player Indicator` nested with its `PlayerIndicator` component
removed (that one follows a `PlayerInput`), on the WorldUI layer and turned to the camera every
frame, labelled `1P`–`4P` in the same colour. The 1P colour is almost exactly the deep sea's blue,
so a player-coloured ring on the water would vanish for 1P.

`Model/Plane` carries a white **QuickOutline** (`Outline`, OutlineAll, width 4 — screen-space, so it
reads at any zoom). It sits on `Plane`, not `Model`, so the bubble trail's particle renderer stays
out of it. QuickOutline bakes smoothed normals into the meshes at `Awake`, which is why
`plane1.fbx` imports with **Read/Write enabled** — without it every plane logs "Not allowed to
access vertices" and Error Pause stops play. It costs about 64 ms per plane at map load, two extra
draws per renderer (72 materials on the plane instead of 24), and the mesh data kept in memory.

The **propeller** (`Circle.004`, three blades and the spinner) spins at `propellerSpeed` (900°/s;
much faster and three blades strobe at 60 fps) about `Model/Propeller Hub` — a marker at the
propeller's centre, forward along the nose. The propeller's own pivot isn't on its axis, so it
can't simply rotate in place. `plane1` is heavy — about 313k triangles a plane, so four planes are
over a million triangles; the game's own `Pesawat1` / `Pesawat2` are 19k / 14k.

Opened directly in the editor with nobody joined, the map spawns one plane on the unmasked shared
asset, in `soloPlaneColor` (1P blue). To test several planes, start in `MainMenu`, join, then go to
stage select — `DebugAutoJoin` players joined inside a level aren't persisted and die on the scene
change. Virtual gamepads (`InputSystem.AddDevice<Gamepad>()`) joined as `Gamepad` cover 3P and 4P.

Each `LevelNode` references one `LevelConfig` — display strings, scene name, default unlock, save
key, and next-stage relationship all come from that asset. The node asks `ProgressionService` for
unlocked state and best stars, refreshing on `ProgressChanged`, and draws what `MapController`
drives. The `Map Node` prefab owns all of it:

| Child | Role |
|---|---|
| `Badge` | world-space canvas 9 m above and 16 m behind the node, on the WorldUI layer, turned to the camera every frame: `Stars`, `Players` (four slots), radial `BorderFill`. Hidden on a locked node |
| `Landing Ring` | a flat world-space canvas on the sea, always shown; its size and position are the stage's area. `Pulse` holds `Ring` (the `LandingRing` sprite, tinted by stage state) and `Ring Fill` (the same sprite, radial-filled with boarding); `LevelNode` scales `Pulse` while the stage is current, so the fill pulses with the ring and the area never changes |
| `Route` | the dashed route from this stage to the next (below) |

**Level islands.** Every stage on the map is one prefab instance that carries everything — the
island art, the badge, the landing ring and the route out — so a stage is placed, moved and
re-ordered as one object (5 Oct 2026, Fitra's ask). The five templates in
`Prefab/StageSelect/Level Islands/` are **Prefab Variants of `Map Node`**, each adding the islands
of one cluster that was already on the map. The `LevelNode` sits on the variant's root, so picking
the stage is one field on the object you dragged in; badge, ring and route changes go on
`Map Node` and reach all five.

| Template | Islands (offsets from the airport island's pivot) |
|---|---|
| `Level Island - Solo` | `Island_airport` alone |
| `Level Island - Rock` | + one `Island_Rock` to the south-east |
| `Level Island - Lighthouse` | + `Island_LightHouse` to the south-west |
| `Level Island - Lighthouse Bay` | + `Island_LightHouse` to the west, two `Island_Rock` to the south |
| `Level Island - Twin Rocks` | + two `Island_Rock` to the east, `Island_LightHouse` to the north |

`Island_airport` sits at local `(0, -0.14, 0)`, rotated 180°; the root is the node pivot (y 5.2).
`Island_Rock` (`Prefab/StageSelect/`) is the `island.fbx` islet as a prefab — the scene used to
hold nine loose copies of the FBX. To add a stage: drag a template under `Level Islands`, set its
`Level`, and set the previous stage's `LevelConfig.nextLevel` to it if it isn't already. To
re-order, swap the `Level` on two islands; the routes follow.

**Routes.** Each `Map Node`'s `Route` child draws one dashed route: a `LineRenderer` (Tile texture
mode, world-space, flat on the sea, 2.6 m wide) with `Material/World/Route Dash.mat` on
`Shader/RouteDash.shader`, laid out by `MapRoute`. **The route finds its own end**: it runs from
its node to whichever node holds that level's `nextLevel`, so the routes always match the real
unlock chain, and a stage with no next level (or one that isn't on the map) draws nothing — which
is why there is no 2 → 3 route while Stage 2 doesn't unlock the archived Stage 3. It runs a cubic
curve from 7.5 m off one node to 7.5 m off the next, measured from the node pivots, bent by its
own `bendStart` / `bendEnd`: the same sign curves it one way, opposite signs make an S. The bends
are per-instance overrides on each island's `Route` (Level 1: 12 / 4, Level 2: −8 / 8, Level 3:
−10 / −4, picked to miss the lighthouse islets) — re-tune them after moving an island. The shader
draws each dash from the UVs as a rounded capsule in metres with a navy outline, fades the ends,
and marches the dashes slowly toward the next stage. `MapRoute` runs in the editor too, so a route
follows an island as it's dragged (the editor only ticks it while the Scene view repaints).

### The map environment

| Piece | What it is |
|---|---|
| `Terrain` | Unity Terrain, `Terrain/ChooseStage TerrainData.asset` (400 × 400 m, heightmap 1025): a coastline framing the sea and mesa islets where the old volcano and rock mountains stood; the sea floor is about −5.8 m. The airport and lighthouse islands have **no** shelves in it any more — they carry their own (`IslandShallows`, below). Terraced in 2.4 m steps so it matches the islands' flat tiers; layers are flat pink-sand / lavender-grass / violet-cliff textures in `Texture/Terrain/` (game palette since 6 Oct 2026). Generated once over Unity MCP — sculpt it by hand from here |
| `Water` | `Material/World/Water.mat` on `Shader/StylizedWater.shader`, a 400 m plane. `WaterSeabed` feeds it the terrain's live heightmap and every island's shelf each frame, so shallows, foam and shore waves follow any sculpting or moved island with no bake |
| `Clouds` | the cloud prefabs as **shadow-only** casters 45–55 m up, moved and wrapped by `CloudShadowDrift`; the sun is at `(50, 330)`, so shadows fall up and to the left on screen |
| `Global Volume` | the same `Settings/PostProcessing Profile` as MainMenu and the levels |

**The map's palette is the game's** (6 Oct 2026, Fitra's call after a palette comparison showed the
map had no purple or pink and was 14 % green — the main reason it read as another game). Everything
below was recoloured from the old sea-blue / grass-green / khaki:

| Piece | Colour | Lives in |
|---|---|---|
| deep sea / shallows / foam | indigo `#3B44B0` / periwinkle `#9A9CF0` (alpha 0.7) / pink-white `#FFF0F6` | `Material/World/Water.mat` |
| beaches, land, cliffs (terrain) | dusty pink `#EBB8C0`, lavender `#9A86D8`, violet `#6A58B8` | each Terrain Layer's **Color Tint** (below) over the neutral `Texture/Terrain/Terrain_{Sand,Grass,Rock}.png` |
| island grass / island cliffs | light lavender `#C2ADEE` / violet `#7A66C4` | `Model/Environment/StageSelect/Materials/Island {Grass,Rock}.mat` |
| tree leaves (accent, like the menu's palms) | teal `#3FAE96` / `#23807A` | `…/Materials/Tree Leaves {Light,Dark}.mat` |

**Terrain colours are tints, so they're editable by hand** (6 Oct 2026, Fitra's choice). The three
terrain textures are neutral near-white greys (mean 250) that only carry the faint noise; each
layer's colour is its **Color Tint**: select `Environment/Terrain` → *Paint Terrain* → *Paint
Texture* → pick a layer → *Color Tint* under Diffuse (it's `TerrainLayer.diffuseRemapMax`, stored on
the `Assets/Terrain/ChooseStage {Sand,Grass,Rock}.terrainlayer` assets). **The swatch renders
lighter than it looks**: URP multiplies the linear texture by the tint's raw value, and the picker
shows that value as if it were sRGB — so tune by eye in the Scene view, or type the tint from the
table (tint = the colour's linear value ÷ 0.956, the neutral texture's linear level):

| Layer | Game palette (in use): renders / tint | Old green palette: renders / tint |
|---|---|---|
| Sand | `#EBB8C0` / `#DE808D` | `#F5D8A3` / `#F4B762` |
| Grass | `#9A86D8` / `#5640B7` | `#7FBE57` / `#398919` |
| Rock | `#6A58B8` / `#261A80` | `#E4A986` / `#CF6A40` |

`Terrain_{Sand,Grass,Rock}_Green.png` are the old coloured originals, restored from git for the
comparison; with tints in charge they're no longer needed (use one only with its layer's tint set to
white, or the two colours multiply).

Buildings, runways (`AIRPORT.003` palette), the lighthouses' red and white, the rings and the routes
kept their colours. The island and tree materials were embedded in `island.fbx`,
`lighthouse 1.fbx` and `LP_TREE_PACK.fbx`; each FBX's import now **remaps** them to the external
materials above (`lushgrass` → Island Grass and `island` → Island Rock in both island FBXs,
`Material.006` / `.014` → the leaves). The island prefabs had stored direct references to the old
embedded leaf materials, which went empty on reimport — they were refilled from the FBX's own slot
order. If a future FBX remap leaves pink/missing material, look for the same thing.

Planes cruise at the `Plane Spawn` height, **17.5 m**, clear of the terrain rim (10.7 m) and the
control towers (13.3 m). The cloud-sea fog of war that covered every locked region (1 Oct 2026)
was removed on 5 Oct 2026 at Fitra's request, with its script, shaders, materials and meshes —
stage progress now lives on the landing rings.

The water is written to avoid the outline pass's ghosting: that pass draws edges from the depth
and normals textures, so anything under a surface that doesn't write them (the old water) had its
silhouette traced across the sea. `StylizedWater` sits at `Geometry+450` — inside the opaque range
the depth-normals prepass renders — with its own `DepthOnly` / `DepthNormals` passes writing one
flat up-normal, while its colour pass still alpha-blends so the shallows show the sand below. Since
the depth texture then holds the water itself, depth-based colour comes from the terrain heightmap
instead.

**Island shallows move with the islands** (5 Oct 2026, Fitra's ask). Each island prefab
(`Island_airport`, `Island_LightHouse`, `Island_Rock`) has an `IslandShallows`: a shelf centre
(`centerOffset`, the footprint's middle — 2.96 m off the airport's pivot), a flat `platformRadius`
just under the surface (airport 8.2 m, islets 4.8 m) and a `slopeWidth` down to the sea floor
(6 / 6.5 m). `WaterSeabed` hands every enabled one to the water each frame (in the editor too) and
`StylizedWater` takes the higher of the terrain and the shelves, so an island dragged anywhere
brings its foam rim, turquoise shallows and shore waves with it, and shelves that overlap merge
into one. The platform sits `_ShelfDepth` (0.26 m) under the surface — inside the foam depth, so
it reads as a white rim — and the edge wobbles so no shelf is a clean circle. The water takes 32
shelves at most (26 now) and warns once if there are more; raise `MaxIslands` in `WaterSeabed` and
`MAX_ISLAND_SHALLOWS` in the shader together. The old terrain-sculpted platforms were flattened to
the sea floor the same day; the mesas' and coast's own shores were left alone.

The scene's roots, as cleaned up on 5 Oct 2026:

| Root | Holds |
|---|---|
| `Main Camera`, `Directional Light`, `Global Volume` | the shot, the sun (`(50, 330)`), post-processing |
| `Map System` | `MapController` |
| `Plane Spawn`, `Level Ticket` | where the planes start; the boarding pass |
| `Level Islands` | `Level 1 Island` … `Level 4 Island`: Lighthouse Bay, Solo, Rock and Solo templates on the four northern airport islands, about 34 m apart, left to right across the top of the bay |
| `Decorations` | everything without a stage: `Island Group 1`–`6` (each airport island with no node, plus its own islets, pivoted on the airport island so a group moves as one) and three loose lighthouse islets |
| `Environment` | `Terrain`, `Water`, `Clouds` (the shadow casters) |

(A move of the nodes to the spread-out southern islands was planned on 1 Oct 2026 but never
applied.) `mapCenter` / `mapHalfExtents` cover every island, so planes can fly anywhere on the map.
The islands and props come from `Prefab/StageSelect/` with their FBX files in
`Model/Environment/StageSelect/`. The clean-up deleted scene objects nobody could see — the
inactive `Map Board` sprite of the old flat map, Carstenz's `plane1`–`plane4` model options parked
under the sea (two `plane1` copies alone were over 600k triangles) and the `Corals` — but not their
prefabs or models.

`plane1.fbx` and `could2.fbx` came out of Blender with the scene's camera and lights, and
`plane1` used to bring its `Camera` into the map: a full-screen Base camera at depth 0 that drew
after the Main Camera (−1) and covered it with a fixed view of the sea. Both models now import with
**Cameras and Lights unticked** (1 Oct 2026). If the map ever shows a still view that ignores the
planes, list `Camera.allCameras` in Play mode. Carstenz's hand-made
`Shader/Water.shadergraph` is no longer used. `Prefab/StageSelect/vfx_Tornado_01` isn't placed
anywhere, and its particle material was never committed, so it renders magenta.

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
