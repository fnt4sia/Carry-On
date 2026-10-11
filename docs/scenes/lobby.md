# Lobby

**Scene:** `Assets/Scenes/Menu/MainMenu.unity` · **Scripts:** `MainMenu/{MainMenuManager, TeamPanel, TeamSlot}.cs`,
`Game/Player/PlayerIndicator.cs`

The players' half of the MainMenu scene: where joined characters stand, their join bubbles and
pins, and the team panel in the bottom-right corner. The menu banner is in [main menu](main-menu.md);
joining itself — devices, splitting a pad — is in [player](../mechanics/player.md#joining).

## Standing the lobby characters
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
capsule** — all four bodies share one capsule that stops at the skull ([player](../mechanics/player.md#all-four-bodies-are-one-rig)),
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

## Team panel
A four-seat strip in the screen's **bottom-right corner**: one head-and-body figure per seat, lit
white when a player holds it and dim grey when it is free, with a line under each seat naming the
device that holds it or the button that would take it. Seats fill left to right in join order.

```text
LobbyHUD                 Canvas, Screen Space - Overlay, sort 10, CanvasScaler 1920x1080 match 0.5
  TeamPanel              Multiplayer2.png 9-sliced (32 px border) at 346x172, anchored bottom-right, (-16, +12); TeamPanel.cs
    Slots                HorizontalLayoutGroup, spacing 16 (57 px seats -> 73 px pitch), 276 wide
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
heads and 19 px below the tag** at 1920x1080. Sideways it matches: the panel is sized so the
276 px seat block has 19 px either side of it inside the 16 px frame (`276 + 2 × (19 + 16) = 346`). The `−32` is one pixel past centring the rect,
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
- **Seats are 16 px apart** (9 Oct 2026, Fitra: the figures sat too close at the old 5 px). The
  pitch is now 73 px, and that is the whole budget for a tag. The labels were fitted to the old
  62 px pitch, where ~51 px was the widest that cleared its neighbour at font 13 — which is why the
  join prompt is the bare key with no `JOIN` after it, why the keyboard-right seat reads `ARROW`
  and not `ARROWS` (59 px, which touched its neighbour then), and why the split hint is
  `<size=85%>`; there is room for slightly longer ones now. Measure a new label with
  `TMP_Text.GetPreferredValues` before using it; the tag deliberately does not wrap or clip, so an
  over-long one silently runs into the next seat.
- **Button names follow the Xbox layout** (`A` to join, `Y` to split) since that is what PC games
  label generically. They are plain text in `TeamPanel`; swap them for glyph sprites, chosen per
  connected pad, when the art exists.
- **The figures are at the sprites' native pixel size**, 1:1 with the design mock (measured to
  within ~8%). The panel isn't: `Multiplayer2` has a 32 px sprite border and the Image is *Sliced*,
  so it widened from its native 289 px to 346 without stretching its rounded corners or shadow.
  Resize it by width, not scale.
- **Overlay, not World Space**, for the reason in [look](../look.md#overlay-vs-world-space-and-the-outline-pass). The canvas has no
  `GraphicRaycaster` — the panel is display-only, and without one it can never swallow a click
  meant for the banner.
- **Placement was checked against the lineup** at 2 and 4 players; in the bottom-right corner the
  panel is clear of the characters entirely. It was bottom-centre first, where it sat right under
  their feet. Re-checked after the widening: the fourth character ends 87 px left of the panel at
  1920x1080.
- `Multiplayer1` and `Multiplayer3` were imported as *Multiple* sprite mode with no rects sliced,
  i.e. they held **no sprite at all**. Both are *Single* now.
- `ButtonPrompt.png` (a generated 64 px white disc) is left in the project unused: it was the
  circle in the `MANAGE TEAM` row this panel started with, and it is the obvious base for a real
  button glyph.
