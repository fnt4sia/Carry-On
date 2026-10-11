# Main menu

**Scene:** `Assets/Scenes/Menu/MainMenu.unity` · **Scripts:** `MainMenu/{MainMenuManager, InputModeManager,
BoardMenuRow, UIPulse, CameraFocus}.cs`

The menu half of the MainMenu scene: the diegetic banner, its views and the camera lean, the save
and settings panels, and the board rows. The players' half — where joined characters stand and the
team panel — is in [lobby](lobby.md). Why the banner is world space while the team panel is an
overlay is in [look](../look.md#overlay-vs-world-space-and-the-outline-pass).

The authored `Main Camera` pose frames both the menu banner and the character lineup; it only
leaves that pose to lean in on a panel ([views](#views-and-the-camera-lean)). The entire menu is a
world-space canvas drawn onto the banner's poster face. The one screen-space element is the
[team panel](lobby.md#team-panel) at the bottom of the screen, on its own overlay canvas `LobbyHUD`. The
old `Canvas`, `StartView`, and `MenuView` objects were deleted in the August 2026 board-menu
rebuild, along with the per-player `PlayerUI.prefab` cards.

> **The menu moved off `DepartureBoard` in August 2026.** The board carried the first version of
> this menu; it is gone from the scene and `DepartureBoard.prefab` is referenced by nothing. The
> layout below is a portrait re-fit of the same design, not a new one.

## The banner canvas
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
(HDR), which lands near white (`(217, 231, 242)` measured) with a hint of bloom. Both measurements
predate 8 Oct 2026, when Carstenz turned the shared filter from blue `#E6EFFF` to pink `#FFEFF6` —
re-measure before tuning text against them.

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

## Views and the camera lean
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

## What the zoom frames
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

## Save and settings panels
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
[services](../services/save.md) — read that before building anything on top of it.

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

## Board rows
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

### Portrait re-fit
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
in [player](../mechanics/player.md#joining).

## Decorative world canvases
`MenuCanvas` is not the only world-space canvas in the lobby. The airport signs
(`WallSignage2`, `StandingSignage2`) and the `LogoTV` on the pillar above the banner each carry
their own canvas as a child of their **decoration prefab**, with a scrolling ticker or a flicker on
it. (The `WallSignage` ticker that used to hang there was swapped for `LogoTV` in September 2026.)
They are display-only —
no `GraphicRaycaster`, nothing selectable — and they are documented with the props, not here: see
[hazards and props](../decoration.md#signage-text). The signs copy this section's canvas
convention (`localRotation (0, 90, 0)`, ConstantPixelSize scaler, `LiberationSans SDF`), so change
one and check the other.
